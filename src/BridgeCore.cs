using System;
using System.Collections.Generic;
using System.IO;

namespace BombClient
{
    internal sealed class BridgeSettings
    {
        public bool Enabled;
        public bool MockMode;
        public string Url = "http://localhost:39132/bomb-client";
    }

    internal sealed class BridgeDataSnapshot
    {
        public string ScoreboardText = "";
        public string CurrentKit = "";
        public string MatchState = "";
        public string ArenaName = "";
        public string PartyTeamInfo = "";
        public string Cooldowns = "";
        public string ServerMessage = "";
        public string PlayerStats = "";
        public string PotionEffects = "";
        public string ArmorHud = "";
        public string Hotbar = "";
        public string ShulkerPreview = "";
        public DateTime UpdatedUtc = DateTime.MinValue;

        public bool HasAnyData()
        {
            return ScoreboardText.Length > 0 ||
                CurrentKit.Length > 0 ||
                MatchState.Length > 0 ||
                ArenaName.Length > 0 ||
                PartyTeamInfo.Length > 0 ||
                Cooldowns.Length > 0 ||
                ServerMessage.Length > 0 ||
                PlayerStats.Length > 0 ||
                PotionEffects.Length > 0 ||
                ArmorHud.Length > 0 ||
                Hotbar.Length > 0 ||
                ShulkerPreview.Length > 0;
        }
    }

    internal static class BridgeManager
    {
        private static BridgeSettings settings = LoadSettings();
        private static BridgeDataSnapshot snapshot = new BridgeDataSnapshot();
        private static string status = "Offline";

        public static BridgeSettings Settings
        {
            get { return settings; }
        }

        public static BridgeDataSnapshot CurrentData
        {
            get { return snapshot; }
        }

        public static string Status
        {
            get { return status; }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(AppPaths.DataRoot, "bridge.ini"); }
        }

        public static BridgeSettings LoadSettings()
        {
            BridgeSettings loaded = new BridgeSettings();
            if (!File.Exists(ConfigPath))
                return loaded;

            string[] lines = File.ReadAllLines(ConfigPath);
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
                if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                    loaded.Enabled = ParseBool(value, false);
                else if (key.Equals("mockMode", StringComparison.OrdinalIgnoreCase))
                    loaded.MockMode = ParseBool(value, false);
                else if (key.Equals("url", StringComparison.OrdinalIgnoreCase))
                    loaded.Url = value.Length == 0 ? loaded.Url : value;
            }
            return loaded;
        }

        public static void SaveSettings(BridgeSettings next)
        {
            settings = next;
            Directory.CreateDirectory(AppPaths.DataRoot);
            List<string> lines = new List<string>();
            lines.Add("# Bomb Server Bridge settings");
            lines.Add("enabled=" + settings.Enabled.ToString());
            lines.Add("mockMode=" + settings.MockMode.ToString());
            lines.Add("url=" + settings.Url);
            File.WriteAllLines(ConfigPath, lines.ToArray());
            RefreshStatus();
        }

        public static void StartMock()
        {
            settings.Enabled = true;
            settings.MockMode = true;
            snapshot = CreateMockSnapshot();
            status = "Connected";
            SaveSettings(settings);
        }

        public static void Stop()
        {
            settings.Enabled = false;
            settings.MockMode = false;
            snapshot = new BridgeDataSnapshot();
            status = "Offline";
            SaveSettings(settings);
        }

        public static void RefreshStatus()
        {
            if (!settings.Enabled)
            {
                status = "Offline";
                return;
            }

            if (settings.MockMode)
            {
                snapshot = CreateMockSnapshot();
                status = "Connected";
                return;
            }

            status = "Waiting";
        }

        public static string ExampleJson()
        {
            return "{" +
                "\"type\":\"bomb.overlay\"," +
                "\"arena\":\"Bridge Practice\"," +
                "\"kit\":\"Pots\"," +
                "\"match_state\":\"In match\"," +
                "\"scoreboard\":[\"Bomb Bridge\",\"Round 2\",\"Red 1 - Blue 0\"]," +
                "\"cooldowns\":{\"pearl\":\"00:07\"}," +
                "\"message\":\"Server-authoritative test payload\"" +
                "}";
        }

        private static BridgeDataSnapshot CreateMockSnapshot()
        {
            BridgeDataSnapshot data = new BridgeDataSnapshot();
            data.ScoreboardText = "Bomb Bridge\nRound 2\nRed 1 - Blue 0";
            data.CurrentKit = "Pots";
            data.MatchState = "In match";
            data.ArenaName = "Bridge Practice";
            data.PartyTeamInfo = "Team: Red";
            data.Cooldowns = "Pearl 00:07 | Totem ready";
            data.ServerMessage = "Mock bridge data enabled";
            data.PlayerStats = "Kills 3 | Streak 2";
            data.PotionEffects = "Strength II 00:32\nFire Resistance I 01:14\nSpeed II 00:09";
            data.ArmorHud = "Helmet 96\nChest 84\nLegs 88\nBoots 91";
            data.Hotbar = "Totem, Sword, Pearl, Pots, Blocks";
            data.ShulkerPreview = "Mock shulker: 14 components, 27 stacks";
            data.UpdatedUtc = DateTime.UtcNow;
            return data;
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
