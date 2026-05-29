using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BombClient
{
    internal sealed class ServerProfile
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "New Server";
        public string Host = "play.cubecraft.net";
        public int Port = 19132;
        public string Category = "Custom";
        public bool Favorite;
        public DateTime LastJoinedUtc = DateTime.MinValue;
        public string Notes = "";
        public string OverlayPreset = "current";
        public string KeybindPreset = "default";
        public string RecommendedPacks = "";

        public override string ToString()
        {
            string star = Favorite ? "* " : "";
            return star + Name + "  " + Host + ":" + Port.ToString();
        }
    }

    internal sealed class ServerStatusResult
    {
        public bool Online;
        public long? PingMs;
        public string Message = "Not checked";
    }

    internal static class ServerProfileStore
    {
        public static readonly string[] Categories = new string[] { "PvP", "SMP", "Practice", "Featured", "Custom" };

        public static string ProfilesPath
        {
            get { return Path.Combine(AppPaths.DataRoot, "server-profiles.ini"); }
        }

        public static List<ServerProfile> Load()
        {
            return Load(ProfilesPath);
        }

        public static List<ServerProfile> Load(string path)
        {
            if (!File.Exists(path))
                return DefaultProfiles();

            List<ServerProfile> profiles = new List<ServerProfile>();
            ServerProfile current = null;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (line.Equals("[server]", StringComparison.OrdinalIgnoreCase))
                {
                    current = new ServerProfile();
                    profiles.Add(current);
                    continue;
                }

                if (current == null)
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line.Substring(0, eq).Trim();
                string raw = line.Substring(eq + 1).Trim();
                string value = Decode(raw);
                if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    current.Id = value.Length == 0 ? current.Id : value;
                else if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                    current.Name = value.Length == 0 ? current.Name : value;
                else if (key.Equals("host", StringComparison.OrdinalIgnoreCase))
                    current.Host = value.Length == 0 ? current.Host : value;
                else if (key.Equals("port", StringComparison.OrdinalIgnoreCase))
                {
                    int port;
                    if (int.TryParse(raw, out port) && port > 0 && port < 65536)
                        current.Port = port;
                }
                else if (key.Equals("category", StringComparison.OrdinalIgnoreCase))
                    current.Category = value.Length == 0 ? "Custom" : value;
                else if (key.Equals("favorite", StringComparison.OrdinalIgnoreCase))
                    current.Favorite = ParseBool(raw, false);
                else if (key.Equals("lastJoinedUtc", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime parsed;
                    if (DateTime.TryParse(value, out parsed))
                        current.LastJoinedUtc = parsed.ToUniversalTime();
                }
                else if (key.Equals("notes", StringComparison.OrdinalIgnoreCase))
                    current.Notes = value;
                else if (key.Equals("overlayPreset", StringComparison.OrdinalIgnoreCase))
                    current.OverlayPreset = value;
                else if (key.Equals("keybindPreset", StringComparison.OrdinalIgnoreCase))
                    current.KeybindPreset = value;
                else if (key.Equals("recommendedPacks", StringComparison.OrdinalIgnoreCase))
                    current.RecommendedPacks = value;
            }

            if (profiles.Count == 0)
                return DefaultProfiles();
            return profiles;
        }

        public static void Save(List<ServerProfile> profiles)
        {
            Save(profiles, ProfilesPath);
        }

        public static void Save(List<ServerProfile> profiles, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            List<string> lines = new List<string>();
            lines.Add("# Bomb Client server profiles");
            foreach (ServerProfile profile in profiles)
            {
                lines.Add("[server]");
                lines.Add("id=" + Encode(profile.Id));
                lines.Add("name=" + Encode(profile.Name));
                lines.Add("host=" + Encode(profile.Host));
                lines.Add("port=" + profile.Port.ToString());
                lines.Add("category=" + Encode(profile.Category));
                lines.Add("favorite=" + profile.Favorite.ToString());
                lines.Add("lastJoinedUtc=" + Encode(profile.LastJoinedUtc == DateTime.MinValue ? "" : profile.LastJoinedUtc.ToUniversalTime().ToString("o")));
                lines.Add("notes=" + Encode(profile.Notes));
                lines.Add("overlayPreset=" + Encode(profile.OverlayPreset));
                lines.Add("keybindPreset=" + Encode(profile.KeybindPreset));
                lines.Add("recommendedPacks=" + Encode(profile.RecommendedPacks));
                lines.Add("");
            }
            File.WriteAllLines(path, lines.ToArray());
        }

        public static ServerStatusResult CheckStatus(ServerProfile profile)
        {
            ServerStatusResult result = new ServerStatusResult();
            long? ping = PingTools.Measure(profile.Host, profile.Port);
            result.PingMs = ping;
            result.Online = ping.HasValue;
            result.Message = ping.HasValue ? "Online, " + ping.Value.ToString() + " ms" : "Unavailable from safe ping";
            return result;
        }

        public static List<ServerProfile> DefaultProfiles()
        {
            List<ServerProfile> profiles = new List<ServerProfile>();
            profiles.Add(Create("CubeCraft", "play.cubecraft.net", "PvP", true));
            profiles.Add(Create("The Hive", "geo.hivebedrock.network", "Featured", true));
            profiles.Add(Create("NetherGames", "play.nethergames.org", "Practice", false));
            profiles.Add(Create("Lifeboat", "play.lbsg.net", "SMP", false));
            return profiles;
        }

        private static ServerProfile Create(string name, string host, string category, bool favorite)
        {
            ServerProfile profile = new ServerProfile();
            profile.Id = name.ToLowerInvariant().Replace(" ", "-");
            profile.Name = name;
            profile.Host = host;
            profile.Category = category;
            profile.Favorite = favorite;
            profile.Notes = "Safe public-server profile. Advanced data requires an opt-in Bomb Server Bridge.";
            return profile;
        }

        private static string Encode(string value)
        {
            if (value == null)
                return "";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string Decode(string value)
        {
            if (value.Length == 0)
                return "";
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return value;
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            if (bool.TryParse(value, out parsed))
                return parsed;
            return fallback;
        }
    }
}
