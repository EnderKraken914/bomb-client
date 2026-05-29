using System;
using System.Collections.Generic;
using System.IO;

namespace BombClient
{
    internal sealed class ManagedPack
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Pack";
        public string SourcePath = "";
        public string StoredPath = "";
        public string Kind = "zip";
        public bool Enabled = true;
        public DateTime ImportedUtc = DateTime.UtcNow;

        public override string ToString()
        {
            return (Enabled ? "[ON] " : "[OFF] ") + Name + " (" + Kind + ")";
        }
    }

    internal static class ManagedPackStore
    {
        public static string ManagedRoot
        {
            get { return Path.Combine(AppPaths.DataRoot, "ManagedPacks"); }
        }

        public static string IndexPath
        {
            get { return Path.Combine(AppPaths.DataRoot, "managed-packs.ini"); }
        }

        public static List<ManagedPack> Load()
        {
            return Load(IndexPath);
        }

        public static List<ManagedPack> Load(string path)
        {
            List<ManagedPack> packs = new List<ManagedPack>();
            if (!File.Exists(path))
                return packs;

            ManagedPack current = null;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (line.Equals("[pack]", StringComparison.OrdinalIgnoreCase))
                {
                    current = new ManagedPack();
                    packs.Add(current);
                    continue;
                }
                if (current == null)
                    continue;

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    current.Id = value.Length == 0 ? current.Id : value;
                else if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                    current.Name = value;
                else if (key.Equals("source", StringComparison.OrdinalIgnoreCase))
                    current.SourcePath = value;
                else if (key.Equals("stored", StringComparison.OrdinalIgnoreCase))
                    current.StoredPath = value;
                else if (key.Equals("kind", StringComparison.OrdinalIgnoreCase))
                    current.Kind = value;
                else if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                {
                    bool enabled;
                    if (bool.TryParse(value, out enabled))
                        current.Enabled = enabled;
                }
                else if (key.Equals("importedUtc", StringComparison.OrdinalIgnoreCase))
                {
                    DateTime imported;
                    if (DateTime.TryParse(value, out imported))
                        current.ImportedUtc = imported.ToUniversalTime();
                }
            }
            return packs;
        }

        public static void Save(List<ManagedPack> packs)
        {
            Save(packs, IndexPath);
        }

        public static void Save(List<ManagedPack> packs, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            List<string> lines = new List<string>();
            lines.Add("# Bomb Client managed packs");
            foreach (ManagedPack pack in packs)
            {
                lines.Add("[pack]");
                lines.Add("id=" + pack.Id);
                lines.Add("name=" + pack.Name);
                lines.Add("source=" + pack.SourcePath);
                lines.Add("stored=" + pack.StoredPath);
                lines.Add("kind=" + pack.Kind);
                lines.Add("enabled=" + pack.Enabled.ToString());
                lines.Add("importedUtc=" + pack.ImportedUtc.ToUniversalTime().ToString("o"));
                lines.Add("");
            }
            File.WriteAllLines(path, lines.ToArray());
        }

        public static ManagedPack Import(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Pack file was not found.", sourcePath);

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext != ".mcpack" && ext != ".mcaddon" && ext != ".zip")
                throw new InvalidOperationException("Bomb Client can import .mcpack, .mcaddon, and .zip files.");

            Directory.CreateDirectory(ManagedRoot);
            ManagedPack pack = new ManagedPack();
            pack.Name = Path.GetFileNameWithoutExtension(sourcePath);
            pack.SourcePath = sourcePath;
            pack.Kind = ext.TrimStart('.');
            pack.StoredPath = Path.Combine(ManagedRoot, SafeFileName(pack.Name) + "-" + pack.Id.Substring(0, 8) + ext);
            File.Copy(sourcePath, pack.StoredPath, true);
            return pack;
        }

        public static string ResourceDevelopmentFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                "Microsoft.MinecraftUWP_8wekyb3d8bbwe",
                "LocalState",
                "games",
                "com.mojang",
                "development_resource_packs");
        }

        public static string BehaviorDevelopmentFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                "Microsoft.MinecraftUWP_8wekyb3d8bbwe",
                "LocalState",
                "games",
                "com.mojang",
                "development_behavior_packs");
        }

        private static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            if (name.Length == 0)
                return "pack";
            return name;
        }
    }
}
