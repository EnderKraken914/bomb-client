using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BombClient
{
    internal sealed class ModuleSettingDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Kind;
        public readonly string DefaultValue;

        public ModuleSettingDefinition(string id, string displayName, string kind, string defaultValue)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            DefaultValue = defaultValue;
        }
    }

    internal sealed class ClientModuleDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string Category;
        public readonly bool DefaultEnabled;
        public readonly ModuleSettingDefinition[] Settings;
        public readonly Func<Form> RenderHook;
        public readonly Action UpdateHook;

        public ClientModuleDefinition(string id, string displayName, string description, string category, bool defaultEnabled, ModuleSettingDefinition[] settings)
            : this(id, displayName, description, category, defaultEnabled, settings, null, null)
        {
        }

        public ClientModuleDefinition(string id, string displayName, string description, string category, bool defaultEnabled, ModuleSettingDefinition[] settings, Func<Form> renderHook, Action updateHook)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Category = category;
            DefaultEnabled = defaultEnabled;
            Settings = settings ?? new ModuleSettingDefinition[0];
            RenderHook = renderHook;
            UpdateHook = updateHook;
        }
    }

    internal sealed class ClientModuleState
    {
        public string Id = "";
        public bool Enabled;
        public readonly Dictionary<string, string> Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class ClientModuleCatalog
    {
        private static ClientModuleDefinition[] all;

        public static ClientModuleDefinition[] All
        {
            get
            {
                if (all == null)
                    all = BuildCatalog();
                return all;
            }
        }

        public static string[] Categories
        {
            get
            {
                List<string> values = new List<string>();
                values.Add("All");
                foreach (ClientModuleDefinition module in All)
                {
                    if (!values.Contains(module.Category))
                        values.Add(module.Category);
                }
                return values.ToArray();
            }
        }

        public static ClientModuleDefinition Find(string id)
        {
            foreach (ClientModuleDefinition module in All)
            {
                if (string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase))
                    return module;
            }
            return null;
        }

        public static string OverlayIdFromModule(string moduleId)
        {
            if (moduleId != null && moduleId.StartsWith("overlay.", StringComparison.OrdinalIgnoreCase))
                return moduleId.Substring("overlay.".Length);
            return "";
        }

        private static ClientModuleDefinition[] BuildCatalog()
        {
            List<ClientModuleDefinition> modules = new List<ClientModuleDefinition>();
            foreach (OverlayDefinition overlay in OverlayCatalog.All)
            {
                modules.Add(new ClientModuleDefinition(
                    "overlay." + overlay.Id,
                    overlay.Name,
                    overlay.ShortText,
                    overlay.Id == "clientvisuals" ? "Visuals" : "Overlays",
                    overlay.DefaultEnabled,
                    new ModuleSettingDefinition[]
                    {
                        new ModuleSettingDefinition("size", "Size", "percent", "100"),
                        new ModuleSettingDefinition("opacity", "Opacity", "percent", "92")
                    }));
            }

            modules.Add(new ClientModuleDefinition("profiles.launcher", "Launch Profiles", "Release, Preview, and custom safe launch targets.", "Profiles", true, new ModuleSettingDefinition[0]));
            modules.Add(new ClientModuleDefinition("account.microsoft", "Microsoft Account", "Official Microsoft device-code sign-in profile display.", "Account", false, new ModuleSettingDefinition[0]));
            modules.Add(new ClientModuleDefinition("servers.profiles", "Server Profiles", "Saved Bedrock servers, favorites, notes, presets, and safe status checks.", "Servers", true, new ModuleSettingDefinition[0]));
            modules.Add(new ClientModuleDefinition("packs.manager", "Pack Manager", "Managed .mcpack, .mcaddon, and .zip imports without editing installed game files.", "Packs", true, new ModuleSettingDefinition[0]));
            modules.Add(new ClientModuleDefinition("bridge.foundation", "Bomb Server Bridge", "Opt-in cooperating-server data bridge with mock/test overlay data.", "Bridge", false, new ModuleSettingDefinition[]
            {
                new ModuleSettingDefinition("url", "Bridge URL", "text", "http://localhost:39132/bomb-client")
            }));
            modules.Add(new ClientModuleDefinition("updates.latest", "Latest Updates", "Installed version, latest release, changelog, and required update status.", "Updates", true, new ModuleSettingDefinition[0]));
            modules.Add(new ClientModuleDefinition("system.safeclient", "Safe External Client", "No injection, hooks, memory editing, packet manipulation, or Bedrock file edits.", "System", true, new ModuleSettingDefinition[0]));
            return modules.ToArray();
        }
    }

    internal static class ClientModuleConfigStore
    {
        public static string ConfigPath
        {
            get { return Path.Combine(AppPaths.DataRoot, "modules.ini"); }
        }

        public static Dictionary<string, ClientModuleState> Load()
        {
            return Load(ConfigPath);
        }

        public static Dictionary<string, ClientModuleState> Load(string path)
        {
            Dictionary<string, ClientModuleState> states = CreateDefaultStates();
            if (!File.Exists(path))
                return states;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key.StartsWith("module.", StringComparison.OrdinalIgnoreCase))
                {
                    string id = key.Substring("module.".Length);
                    ClientModuleState state = EnsureState(states, id);
                    state.Enabled = ParseBool(value, state.Enabled);
                }
                else if (key.StartsWith("setting.", StringComparison.OrdinalIgnoreCase))
                {
                    string remainder = key.Substring("setting.".Length);
                    int dot = remainder.IndexOf('.');
                    if (dot > 0)
                    {
                        string id = remainder.Substring(0, dot);
                        string settingId = remainder.Substring(dot + 1);
                        ClientModuleState state = EnsureState(states, id);
                        state.Settings[settingId] = value;
                    }
                }
            }
            return states;
        }

        public static void Save(Dictionary<string, ClientModuleState> states)
        {
            Save(states, ConfigPath);
        }

        public static void Save(Dictionary<string, ClientModuleState> states, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            List<string> lines = new List<string>();
            lines.Add("# Bomb Client 2.0 module state");
            foreach (ClientModuleDefinition module in ClientModuleCatalog.All)
            {
                ClientModuleState state = EnsureState(states, module.Id);
                lines.Add("module." + module.Id + "=" + state.Enabled.ToString());
                foreach (KeyValuePair<string, string> setting in state.Settings)
                    lines.Add("setting." + module.Id + "." + setting.Key + "=" + setting.Value);
            }
            File.WriteAllLines(path, lines.ToArray());
        }

        public static bool IsEnabled(Dictionary<string, ClientModuleState> states, ClientModuleDefinition module)
        {
            ClientModuleState state = EnsureState(states, module.Id);
            return state.Enabled;
        }

        public static void SetEnabled(Dictionary<string, ClientModuleState> states, ClientModuleDefinition module, bool enabled)
        {
            ClientModuleState state = EnsureState(states, module.Id);
            state.Enabled = enabled;
        }

        private static Dictionary<string, ClientModuleState> CreateDefaultStates()
        {
            Dictionary<string, ClientModuleState> states = new Dictionary<string, ClientModuleState>(StringComparer.OrdinalIgnoreCase);
            foreach (ClientModuleDefinition module in ClientModuleCatalog.All)
            {
                ClientModuleState state = new ClientModuleState();
                state.Id = module.Id;
                state.Enabled = module.DefaultEnabled;
                foreach (ModuleSettingDefinition setting in module.Settings)
                    state.Settings[setting.Id] = setting.DefaultValue;
                states[module.Id] = state;
            }
            return states;
        }

        private static ClientModuleState EnsureState(Dictionary<string, ClientModuleState> states, string id)
        {
            ClientModuleState state;
            if (!states.TryGetValue(id, out state))
            {
                ClientModuleDefinition module = ClientModuleCatalog.Find(id);
                state = new ClientModuleState();
                state.Id = id;
                state.Enabled = module == null ? false : module.DefaultEnabled;
                if (module != null)
                {
                    foreach (ModuleSettingDefinition setting in module.Settings)
                        state.Settings[setting.Id] = setting.DefaultValue;
                }
                states[id] = state;
            }
            return state;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            if (bool.TryParse(value, out parsed))
                return parsed;
            if (value == "1" || value.Equals("on", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;
            if (value == "0" || value.Equals("off", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;
            return fallback;
        }
    }
}
