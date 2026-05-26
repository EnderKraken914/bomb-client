using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("Bomb Client")]
[assembly: AssemblyProduct("Bomb Client")]
[assembly: AssemblyCompany("EnderKraken914")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace BombClient
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                AppSettings settings = AppSettings.Load();
                string pack = ResourcePackBuilder.Build(settings);
                return File.Exists(pack) ? 0 : 2;
            }

            if (!UpdateChecker.EnforceRequiredUpdate())
                return 0;

            using (System.Threading.Mutex mutex = new System.Threading.Mutex(false, "BombClientLauncher-9C83D71B"))
            {
                bool owns = mutex.WaitOne(0, false);
                if (!owns)
                {
                    MessageBox.Show("Bomb Client is already running.", "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                InputTracker.Install();
                try
                {
                    Application.Run(new MainForm());
                }
                finally
                {
                    OverlayManager.CloseAll();
                    InputTracker.Uninstall();
                }
            }

            return 0;
        }
    }

    internal static class AppPaths
    {
        public static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BombClient");

        public static readonly string SettingsFile = Path.Combine(DataRoot, "settings.ini");
        public static readonly string PackRoot = Path.Combine(DataRoot, "GeneratedPacks");
        public static readonly string UpdateRoot = Path.Combine(DataRoot, "Updates");
    }

    internal static class AppInfo
    {
        public const string Version = "1.0.1";
        public const string RepoOwner = "EnderKraken914";
        public const string RepoName = "bomb-client";
        public const string UpdateManifestUrl = "https://raw.githubusercontent.com/EnderKraken914/bomb-client/main/update.json";
        public const string ReleaseDownloadUrl = "https://github.com/EnderKraken914/bomb-client/releases/latest/download/BombClient-Windows.zip";
    }

    internal sealed class UpdateManifest
    {
        public string LatestVersion = AppInfo.Version;
        public string RequiredVersion = AppInfo.Version;
        public string DownloadUrl = AppInfo.ReleaseDownloadUrl;
        public string ReleasePage = "https://github.com/" + AppInfo.RepoOwner + "/" + AppInfo.RepoName + "/releases/latest";
        public string Notes = "";
        public bool ForceUpdate = true;

        public bool RequiresUpdate()
        {
            Version current;
            Version required;
            if (!Version.TryParse(AppInfo.Version, out current) || !Version.TryParse(RequiredVersion, out required))
                return false;
            return ForceUpdate && current.CompareTo(required) < 0;
        }

        public bool HasOptionalUpdate()
        {
            Version current;
            Version latest;
            if (!Version.TryParse(AppInfo.Version, out current) || !Version.TryParse(LatestVersion, out latest))
                return false;
            return current.CompareTo(latest) < 0;
        }
    }

    internal static class UpdateChecker
    {
        public static bool EnforceRequiredUpdate()
        {
            UpdateManifest manifest = FetchManifest();
            if (manifest == null)
                return true;

            if (manifest.RequiresUpdate())
            {
                DialogResult result = MessageBox.Show(
                    "A Bomb Client update is required before you can continue.\n\nInstalled: " + AppInfo.Version +
                    "\nRequired: " + manifest.RequiredVersion +
                    "\nLatest: " + manifest.LatestVersion +
                    "\n\n" + manifest.Notes +
                    "\n\nDownload and install the update now?",
                    "Bomb Client Update Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                    DownloadAndRunUpdater(manifest);

                return false;
            }

            if (manifest.HasOptionalUpdate())
            {
                DialogResult result = MessageBox.Show(
                    "A newer Bomb Client update is available.\n\nInstalled: " + AppInfo.Version +
                    "\nLatest: " + manifest.LatestVersion +
                    "\n\n" + manifest.Notes +
                    "\n\nDownload it now?",
                    "Bomb Client Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    DownloadAndRunUpdater(manifest);
            }

            return true;
        }

        private static UpdateManifest FetchManifest()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (TimeoutWebClient client = new TimeoutWebClient(5000))
                {
                    client.Headers.Add("User-Agent", "BombClient/" + AppInfo.Version);
                    client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    string json = client.DownloadString(AppInfo.UpdateManifestUrl);
                    return ParseManifest(json);
                }
            }
            catch
            {
                return null;
            }
        }

        private static UpdateManifest ParseManifest(string json)
        {
            UpdateManifest manifest = new UpdateManifest();
            manifest.LatestVersion = ReadJsonString(json, "latest_version", manifest.LatestVersion);
            manifest.RequiredVersion = ReadJsonString(json, "required_version", manifest.RequiredVersion);
            manifest.DownloadUrl = ReadJsonString(json, "download_url", manifest.DownloadUrl);
            manifest.ReleasePage = ReadJsonString(json, "release_page", manifest.ReleasePage);
            manifest.Notes = ReadJsonString(json, "notes", manifest.Notes);
            manifest.ForceUpdate = ReadJsonBool(json, "force_update", manifest.ForceUpdate);
            return manifest;
        }

        private static string ReadJsonString(string json, string key, string fallback)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
            if (!match.Success)
                return fallback;
            return Regex.Unescape(match.Groups[1].Value);
        }

        private static bool ReadJsonBool(string json, string key, bool fallback)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return fallback;
            return string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void DownloadAndRunUpdater(UpdateManifest manifest)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.UpdateRoot);
                string safeVersion = SanitizeFileName(manifest.LatestVersion);
                string zipPath = Path.Combine(AppPaths.UpdateRoot, "BombClient-Windows-" + safeVersion + ".zip");
                string extractPath = Path.Combine(AppPaths.UpdateRoot, "BombClient-Windows-" + safeVersion);
                string scriptPath = Path.Combine(AppPaths.UpdateRoot, "Install-BombClient-Update.cmd");

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                using (TimeoutWebClient client = new TimeoutWebClient(30000))
                {
                    client.Headers.Add("User-Agent", "BombClient/" + AppInfo.Version);
                    client.DownloadFile(manifest.DownloadUrl, zipPath);
                }

                ZipFile.ExtractToDirectory(zipPath, extractPath);
                string newExe = Path.Combine(extractPath, "Bomb Client.exe");
                if (!File.Exists(newExe))
                    throw new FileNotFoundException("The update package did not contain Bomb Client.exe.");

                string currentExe = Application.ExecutablePath;
                string currentDir = Path.GetDirectoryName(currentExe);
                string newReadme = Path.Combine(extractPath, "README.md");
                string currentReadme = Path.Combine(currentDir, "README.md");

                StringBuilder script = new StringBuilder();
                script.AppendLine("@echo off");
                script.AppendLine("setlocal");
                script.AppendLine("title Bomb Client Updater");
                script.AppendLine("echo Updating Bomb Client...");
                script.AppendLine("timeout /t 2 /nobreak >nul");
                script.AppendLine("copy /y \"" + newExe + "\" \"" + currentExe + "\" >nul");
                if (File.Exists(newReadme))
                    script.AppendLine("copy /y \"" + newReadme + "\" \"" + currentReadme + "\" >nul");
                script.AppendLine("start \"\" \"" + currentExe + "\"");
                script.AppendLine("exit /b 0");
                File.WriteAllText(scriptPath, script.ToString(), Encoding.ASCII);

                Process.Start(new ProcessStartInfo(scriptPath) { UseShellExecute = true, WorkingDirectory = AppPaths.UpdateRoot });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Bomb Client could not install the update automatically.\n\n" +
                    ex.Message +
                    "\n\nThe release page will open instead.",
                    "Bomb Client Update",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                try
                {
                    Process.Start(manifest.ReleasePage);
                }
                catch
                {
                }
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Length == 0 ? "latest" : value;
        }
    }

    internal sealed class TimeoutWebClient : WebClient
    {
        private readonly int timeoutMs;

        public TimeoutWebClient(int timeout)
        {
            timeoutMs = timeout;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request != null)
                request.Timeout = timeoutMs;
            return request;
        }
    }

    internal sealed class AppSettings
    {
        public readonly Dictionary<string, bool> Overlays = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, Point> Positions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        public string ServerHost = "play.cubecraft.net";
        public int ServerPort = 19132;
        public string LaunchProfile = "release";
        public string CustomLaunchTarget = "";
        public int OverlayOpacity = 92;
        public bool EditMode = false;
        public bool OnlyShowInWorld = true;
        public bool VisualLowFire = true;
        public bool VisualNoBobber = true;
        public bool VisualCleanPumpkin = true;
        public bool VisualClearVignette = true;

        public static AppSettings Load()
        {
            AppSettings settings = CreateDefault();
            Directory.CreateDirectory(AppPaths.DataRoot);

            if (!File.Exists(AppPaths.SettingsFile))
            {
                settings.Save();
                return settings;
            }

            string[] lines = File.ReadAllLines(AppPaths.SettingsFile);
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

                if (key.StartsWith("overlay.", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Overlays[key.Substring(8)] = ParseBool(value, false);
                }
                else if (key.StartsWith("pos.", StringComparison.OrdinalIgnoreCase))
                {
                    Point p;
                    if (TryParsePoint(value, out p))
                        settings.Positions[key.Substring(4)] = p;
                }
                else if (key.Equals("server.host", StringComparison.OrdinalIgnoreCase))
                {
                    settings.ServerHost = value.Length == 0 ? settings.ServerHost : value;
                }
                else if (key.Equals("server.port", StringComparison.OrdinalIgnoreCase))
                {
                    int port;
                    if (int.TryParse(value, out port) && port > 0 && port < 65536)
                        settings.ServerPort = port;
                }
                else if (key.Equals("launch.profile", StringComparison.OrdinalIgnoreCase))
                {
                    settings.LaunchProfile = value.Length == 0 ? "release" : value;
                }
                else if (key.Equals("launch.customTarget", StringComparison.OrdinalIgnoreCase))
                {
                    settings.CustomLaunchTarget = value;
                }
                else if (key.Equals("overlay.opacity", StringComparison.OrdinalIgnoreCase))
                {
                    int opacity;
                    if (int.TryParse(value, out opacity))
                        settings.OverlayOpacity = Math.Max(40, Math.Min(100, opacity));
                }
                else if (key.Equals("edit.mode", StringComparison.OrdinalIgnoreCase))
                {
                    settings.EditMode = ParseBool(value, false);
                }
                else if (key.Equals("overlay.onlyShowInWorld", StringComparison.OrdinalIgnoreCase))
                {
                    settings.OnlyShowInWorld = ParseBool(value, true);
                }
                else if (key.Equals("visual.lowFire", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualLowFire = ParseBool(value, true);
                }
                else if (key.Equals("visual.noBobber", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualNoBobber = ParseBool(value, true);
                }
                else if (key.Equals("visual.cleanPumpkin", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualCleanPumpkin = ParseBool(value, true);
                }
                else if (key.Equals("visual.clearVignette", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualClearVignette = ParseBool(value, true);
                }
            }

            foreach (OverlayDefinition def in OverlayCatalog.All)
            {
                if (!settings.Overlays.ContainsKey(def.Id))
                    settings.Overlays[def.Id] = def.DefaultEnabled;
            }

            return settings;
        }

        private static AppSettings CreateDefault()
        {
            AppSettings settings = new AppSettings();
            foreach (OverlayDefinition def in OverlayCatalog.All)
                settings.Overlays[def.Id] = def.DefaultEnabled;
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            List<string> lines = new List<string>();
            lines.Add("# Bomb Client settings");
            lines.Add("server.host=" + ServerHost);
            lines.Add("server.port=" + ServerPort.ToString());
            lines.Add("launch.profile=" + LaunchProfile);
            lines.Add("launch.customTarget=" + CustomLaunchTarget);
            lines.Add("overlay.opacity=" + OverlayOpacity.ToString());
            lines.Add("edit.mode=" + EditMode.ToString());
            lines.Add("overlay.onlyShowInWorld=" + OnlyShowInWorld.ToString());
            lines.Add("visual.lowFire=" + VisualLowFire.ToString());
            lines.Add("visual.noBobber=" + VisualNoBobber.ToString());
            lines.Add("visual.cleanPumpkin=" + VisualCleanPumpkin.ToString());
            lines.Add("visual.clearVignette=" + VisualClearVignette.ToString());

            foreach (OverlayDefinition def in OverlayCatalog.All)
            {
                bool enabled = false;
                Overlays.TryGetValue(def.Id, out enabled);
                lines.Add("overlay." + def.Id + "=" + enabled.ToString());
            }

            foreach (KeyValuePair<string, Point> entry in Positions)
            {
                lines.Add("pos." + entry.Key + "=" + entry.Value.X.ToString() + "," + entry.Value.Y.ToString());
            }

            File.WriteAllLines(AppPaths.SettingsFile, lines.ToArray());
        }

        private static bool ParseBool(string text, bool fallback)
        {
            bool result;
            if (bool.TryParse(text, out result))
                return result;
            if (text == "1" || text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text.Equals("on", StringComparison.OrdinalIgnoreCase))
                return true;
            if (text == "0" || text.Equals("no", StringComparison.OrdinalIgnoreCase) || text.Equals("off", StringComparison.OrdinalIgnoreCase))
                return false;
            return fallback;
        }

        private static bool TryParsePoint(string text, out Point point)
        {
            point = Point.Empty;
            string[] parts = text.Split(',');
            if (parts.Length != 2)
                return false;
            int x;
            int y;
            if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out y))
                return false;
            point = new Point(x, y);
            return true;
        }
    }

    internal sealed class LaunchProfile
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string AppsFolderId;
        public readonly string PackageFolder;
        public readonly string FallbackUri;
        public readonly bool IsCustom;

        public LaunchProfile(string id, string name, string appsFolderId, string packageFolder, string fallbackUri, bool isCustom)
        {
            Id = id;
            Name = name;
            AppsFolderId = appsFolderId;
            PackageFolder = packageFolder;
            FallbackUri = fallbackUri;
            IsCustom = isCustom;
        }

        public override string ToString()
        {
            if (IsCustom)
                return Name;
            return Name + (IsInstalled() ? "  (installed)" : "  (not found)");
        }

        public bool IsInstalled()
        {
            if (PackageFolder.Length == 0)
                return true;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", PackageFolder);
            return Directory.Exists(path);
        }
    }

    internal static class LaunchProfileCatalog
    {
        public static readonly LaunchProfile Release = new LaunchProfile(
            "release",
            "Minecraft Bedrock Release",
            "Microsoft.MinecraftUWP_8wekyb3d8bbwe!App",
            "Microsoft.MinecraftUWP_8wekyb3d8bbwe",
            "minecraft:",
            false);

        public static readonly LaunchProfile Preview = new LaunchProfile(
            "preview",
            "Minecraft Preview",
            "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe!App",
            "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe",
            "",
            false);

        public static readonly LaunchProfile Custom = new LaunchProfile(
            "custom",
            "Custom Launcher / Version Manager",
            "",
            "",
            "",
            true);

        public static readonly LaunchProfile[] All = new LaunchProfile[] { Release, Preview, Custom };

        public static LaunchProfile Get(string id)
        {
            foreach (LaunchProfile profile in All)
            {
                if (string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
                    return profile;
            }
            return Release;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly AppSettings settings;
        private readonly Dictionary<string, Button> toggleButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        private FlowLayoutPanel overlayFlow;
        private Label statusLabel;
        private Label minecraftLabel;
        private ComboBox launchProfileBox;
        private TextBox customLaunchBox;
        private TextBox serverHostBox;
        private NumericUpDown serverPortBox;
        private TrackBar opacityTrack;
        private Label opacityLabel;
        private CheckBox gameplayOnlyCheck;
        private CheckBox lowFireCheck;
        private CheckBox noBobberCheck;
        private CheckBox cleanPumpkinCheck;
        private CheckBox clearVignetteCheck;
        private Timer statusTimer;
        private readonly Font titleFont = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
        private readonly Font headerFont = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        private readonly Font uiFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        private readonly Color bg = Color.FromArgb(9, 12, 18);
        private readonly Color panel = Color.FromArgb(18, 23, 33);
        private readonly Color panel2 = Color.FromArgb(24, 30, 43);
        private readonly Color red = Color.FromArgb(225, 48, 48);
        private readonly Color orange = Color.FromArgb(255, 164, 58);
        private readonly Color text = Color.FromArgb(239, 244, 252);
        private readonly Color muted = Color.FromArgb(152, 163, 180);

        public MainForm()
        {
            settings = AppSettings.Load();
            OverlayManager.Configure(settings);

            Text = "Bomb Client";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 640);
            Size = new Size(1060, 690);
            BackColor = bg;
            Font = uiFont;

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 2;
            shell.RowCount = 2;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 198f));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 98f));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            shell.BackColor = bg;
            Controls.Add(shell);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(12, 16, 24);
            header.Padding = new Padding(24, 14, 24, 14);
            shell.SetColumnSpan(header, 2);
            shell.Controls.Add(header, 0, 0);

            PictureBox logo = new PictureBox();
            logo.Image = AssetLoader.LoadLogo();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Location = new Point(22, 12);
            logo.Size = new Size(72, 72);
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = "Bomb Client";
            title.AutoSize = true;
            title.ForeColor = text;
            title.Font = titleFont;
            title.Location = new Point(105, 16);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Bedrock launcher and external PvP overlays";
            subtitle.AutoSize = true;
            subtitle.ForeColor = muted;
            subtitle.Location = new Point(110, 58);
            header.Controls.Add(subtitle);

            Button launch = CreatePrimaryButton("Launch Minecraft");
            launch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            launch.Size = new Size(178, 42);
            launch.Location = new Point(header.Width - 202, 28);
            launch.Click += delegate { LaunchMinecraft(); };
            header.Resize += delegate { launch.Location = new Point(header.Width - 202, 28); };
            header.Controls.Add(launch);

            Panel nav = new Panel();
            nav.Dock = DockStyle.Fill;
            nav.BackColor = Color.FromArgb(11, 15, 23);
            nav.Padding = new Padding(14, 18, 14, 18);
            shell.Controls.Add(nav, 0, 1);

            Button navHome = CreateNavButton("Home");
            Button navOverlays = CreateNavButton("Overlays");
            Button navProfiles = CreateNavButton("Profiles");
            Button navAccount = CreateNavButton("Account");
            Button navVisual = CreateNavButton("Visual Pack");
            Button navSettings = CreateNavButton("Settings");
            navHome.Location = new Point(14, 18);
            navOverlays.Location = new Point(14, 68);
            navProfiles.Location = new Point(14, 118);
            navAccount.Location = new Point(14, 168);
            navVisual.Location = new Point(14, 218);
            navSettings.Location = new Point(14, 268);
            nav.Controls.Add(navHome);
            nav.Controls.Add(navOverlays);
            nav.Controls.Add(navProfiles);
            nav.Controls.Add(navAccount);
            nav.Controls.Add(navVisual);
            nav.Controls.Add(navSettings);

            statusLabel = new Label();
            statusLabel.ForeColor = muted;
            statusLabel.Text = "Ready";
            statusLabel.AutoSize = false;
            statusLabel.Size = new Size(164, 70);
            statusLabel.Location = new Point(16, nav.Height - 92);
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            nav.Controls.Add(statusLabel);

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = bg;
            content.Padding = new Padding(22);
            shell.Controls.Add(content, 1, 1);

            Panel homePage = BuildHomePage();
            Panel overlaysPage = BuildOverlaysPage();
            Panel profilesPage = BuildProfilesPage();
            Panel accountPage = BuildAccountPage();
            Panel visualPage = BuildVisualPage();
            Panel settingsPage = BuildSettingsPage();
            pages["Home"] = homePage;
            pages["Overlays"] = overlaysPage;
            pages["Profiles"] = profilesPage;
            pages["Account"] = accountPage;
            pages["Visual"] = visualPage;
            pages["Settings"] = settingsPage;
            content.Controls.Add(homePage);
            content.Controls.Add(overlaysPage);
            content.Controls.Add(profilesPage);
            content.Controls.Add(accountPage);
            content.Controls.Add(visualPage);
            content.Controls.Add(settingsPage);

            navHome.Click += delegate { ShowPage("Home"); };
            navOverlays.Click += delegate { ShowPage("Overlays"); };
            navProfiles.Click += delegate { ShowPage("Profiles"); };
            navAccount.Click += delegate { ShowPage("Account"); };
            navVisual.Click += delegate { ShowPage("Visual"); };
            navSettings.Click += delegate { ShowPage("Settings"); };
            ShowPage("Home");

            statusTimer = new Timer();
            statusTimer.Interval = 1000;
            statusTimer.Tick += delegate { UpdateMinecraftStatus(); };
            statusTimer.Start();
            UpdateMinecraftStatus();

            Shown += delegate { OverlayManager.ApplyConfiguredOverlays(); };
            FormClosing += delegate
            {
                PersistSettingsFromUi();
                settings.Save();
            };
        }

        private Panel BuildHomePage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Home");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel launchCard = CreateCard(0, 54, 430, 194);
            page.Controls.Add(launchCard);
            Label launchTitle = CreateCardTitle("Launcher");
            launchTitle.Location = new Point(20, 18);
            launchCard.Controls.Add(launchTitle);

            minecraftLabel = CreateMutedLabel("Minecraft status");
            minecraftLabel.Location = new Point(22, 54);
            minecraftLabel.Size = new Size(360, 28);
            launchCard.Controls.Add(minecraftLabel);

            Label profileLabel = CreateMutedLabel("Profile: " + LaunchProfileCatalog.Get(settings.LaunchProfile).Name);
            profileLabel.Location = new Point(22, 78);
            profileLabel.Size = new Size(360, 24);
            launchCard.Controls.Add(profileLabel);

            Button launch = CreatePrimaryButton("Launch Minecraft");
            launch.Location = new Point(20, 118);
            launch.Size = new Size(180, 42);
            launch.Click += delegate { LaunchMinecraft(); };
            launchCard.Controls.Add(launch);

            Button openFolder = CreateSecondaryButton("Open Bedrock Folder");
            openFolder.Location = new Point(214, 118);
            openFolder.Size = new Size(180, 42);
            openFolder.Click += delegate { OpenBedrockFolder(); };
            launchCard.Controls.Add(openFolder);

            Panel hudCard = CreateCard(452, 54, 430, 194);
            page.Controls.Add(hudCard);
            Label hudTitle = CreateCardTitle("HUD");
            hudTitle.Location = new Point(20, 18);
            hudCard.Controls.Add(hudTitle);

            Button startOverlays = CreatePrimaryButton("Start Enabled Overlays");
            startOverlays.Location = new Point(20, 58);
            startOverlays.Size = new Size(180, 42);
            startOverlays.Click += delegate
            {
                PersistSettingsFromUi();
                OverlayManager.ApplyConfiguredOverlays();
                SetStatus("Enabled overlays started.");
            };
            hudCard.Controls.Add(startOverlays);

            Button stopOverlays = CreateSecondaryButton("Stop Overlays");
            stopOverlays.Location = new Point(214, 58);
            stopOverlays.Size = new Size(150, 42);
            stopOverlays.Click += delegate
            {
                OverlayManager.CloseAll();
                SetStatus("Overlays stopped.");
            };
            hudCard.Controls.Add(stopOverlays);

            CheckBox editMode = CreateCheck("HUD edit mode", settings.EditMode);
            editMode.Location = new Point(22, 122);
            editMode.CheckedChanged += delegate
            {
                settings.EditMode = editMode.Checked;
                OverlayManager.SetEditMode(settings.EditMode);
                settings.Save();
            };
            hudCard.Controls.Add(editMode);

            Panel packCard = CreateCard(0, 274, 430, 194);
            page.Controls.Add(packCard);
            Label packTitle = CreateCardTitle("PvP Visual Pack");
            packTitle.Location = new Point(20, 18);
            packCard.Controls.Add(packTitle);

            Button buildPack = CreatePrimaryButton("Build .mcpack");
            buildPack.Location = new Point(20, 58);
            buildPack.Size = new Size(150, 42);
            buildPack.Click += delegate { BuildPack(false); };
            packCard.Controls.Add(buildPack);

            Button importPack = CreateSecondaryButton("Build and Import");
            importPack.Location = new Point(184, 58);
            importPack.Size = new Size(150, 42);
            importPack.Click += delegate { BuildPack(true); };
            packCard.Controls.Add(importPack);

            Label packNote = CreateMutedLabel("Low fire, no bobber, clean pumpkin, clear vignette");
            packNote.Location = new Point(22, 124);
            packNote.Size = new Size(360, 40);
            packCard.Controls.Add(packNote);

            Panel infoCard = CreateCard(452, 274, 430, 194);
            page.Controls.Add(infoCard);
            Label infoTitle = CreateCardTitle("External Mode");
            infoTitle.Location = new Point(20, 18);
            infoCard.Controls.Add(infoTitle);
            Label info = CreateMutedLabel("Bomb Client launches Bedrock and draws Windows overlays above it. It does not inject code or patch the installed game.");
            info.Location = new Point(22, 56);
            info.Size = new Size(370, 76);
            infoCard.Controls.Add(info);

            return page;
        }

        private Panel BuildOverlaysPage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Overlays");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            overlayFlow = new FlowLayoutPanel();
            overlayFlow.Location = new Point(0, 54);
            overlayFlow.Size = new Size(page.Width, page.Height - 54);
            overlayFlow.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            overlayFlow.AutoScroll = true;
            overlayFlow.WrapContents = true;
            overlayFlow.FlowDirection = FlowDirection.LeftToRight;
            overlayFlow.BackColor = bg;
            overlayFlow.Resize += delegate { ResizeOverlayCards(); };
            page.Controls.Add(overlayFlow);

            foreach (OverlayDefinition def in OverlayCatalog.All)
                overlayFlow.Controls.Add(CreateOverlayCard(def));

            ResizeOverlayCards();

            return page;
        }

        private Panel BuildProfilesPage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Profiles");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel launchCard = CreateCard(0, 54, 700, 260);
            page.Controls.Add(launchCard);
            Label title = CreateCardTitle("Bedrock Launch Profile");
            title.Location = new Point(20, 18);
            launchCard.Controls.Add(title);

            Label profileLabel = CreateMutedLabel("Version target");
            profileLabel.Location = new Point(22, 62);
            profileLabel.Size = new Size(120, 24);
            launchCard.Controls.Add(profileLabel);

            launchProfileBox = new ComboBox();
            launchProfileBox.DropDownStyle = ComboBoxStyle.DropDownList;
            launchProfileBox.BackColor = Color.FromArgb(30, 37, 51);
            launchProfileBox.ForeColor = text;
            launchProfileBox.FlatStyle = FlatStyle.Flat;
            launchProfileBox.Location = new Point(150, 60);
            launchProfileBox.Width = 310;
            foreach (LaunchProfile profile in LaunchProfileCatalog.All)
                launchProfileBox.Items.Add(profile);
            SelectLaunchProfileBoxItem();
            launchProfileBox.SelectedIndexChanged += delegate
            {
                LaunchProfile selected = launchProfileBox.SelectedItem as LaunchProfile;
                if (selected != null)
                {
                    settings.LaunchProfile = selected.Id;
                    settings.Save();
                }
            };
            launchCard.Controls.Add(launchProfileBox);

            Button launch = CreatePrimaryButton("Launch Selected");
            launch.Location = new Point(482, 56);
            launch.Size = new Size(160, 40);
            launch.Click += delegate { LaunchMinecraft(); };
            launchCard.Controls.Add(launch);

            Label customLabel = CreateMutedLabel("Custom target");
            customLabel.Location = new Point(22, 112);
            customLabel.Size = new Size(120, 24);
            launchCard.Controls.Add(customLabel);

            customLaunchBox = new TextBox();
            customLaunchBox.Text = settings.CustomLaunchTarget;
            customLaunchBox.Location = new Point(150, 110);
            customLaunchBox.Width = 492;
            customLaunchBox.BackColor = Color.FromArgb(30, 37, 51);
            customLaunchBox.ForeColor = text;
            customLaunchBox.BorderStyle = BorderStyle.FixedSingle;
            launchCard.Controls.Add(customLaunchBox);

            Button save = CreateSecondaryButton("Save Profile");
            save.Location = new Point(150, 156);
            save.Size = new Size(140, 38);
            save.Click += delegate
            {
                PersistSettingsFromUi();
                settings.Save();
                SetStatus("Launch profile saved.");
            };
            launchCard.Controls.Add(save);

            Button openVersions = CreateSecondaryButton("Open Minecraft Data");
            openVersions.Location = new Point(306, 156);
            openVersions.Size = new Size(172, 38);
            openVersions.Click += delegate { OpenBedrockFolder(); };
            launchCard.Controls.Add(openVersions);

            Label note = CreateMutedLabel("Release and Preview use Microsoft's installed Bedrock apps. Custom can launch a shortcut, exe, URI, or a third-party Bedrock version manager you already have installed.");
            note.Location = new Point(22, 208);
            note.Size = new Size(620, 38);
            launchCard.Controls.Add(note);

            Panel clientCard = CreateCard(0, 338, 700, 164);
            page.Controls.Add(clientCard);
            Label clientTitle = CreateCardTitle("Client Hub Behavior");
            clientTitle.Location = new Point(20, 18);
            clientCard.Controls.Add(clientTitle);
            Label clientNote = CreateMutedLabel("Bomb Client now launches profiles, keeps overlay presets, builds the PvP visual pack, and starts external HUD modules around Bedrock. External clients cannot safely replace Bedrock's built-in version system the same way Feather manages Java installations, but profiles make the workflow similar.");
            clientNote.Location = new Point(22, 58);
            clientNote.Size = new Size(620, 78);
            clientCard.Controls.Add(clientNote);

            return page;
        }

        private void ResizeOverlayCards()
        {
            if (overlayFlow == null)
                return;

            int available = overlayFlow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10;
            int columns = available >= 760 ? 2 : 1;
            int width = Math.Max(320, (available - ((columns - 1) * 16)) / columns);

            foreach (Control control in overlayFlow.Controls)
            {
                Panel card = control as Panel;
                if (card != null)
                    card.Width = width;
            }
        }

        private void SelectLaunchProfileBoxItem()
        {
            if (launchProfileBox == null)
                return;

            for (int i = 0; i < launchProfileBox.Items.Count; i++)
            {
                LaunchProfile profile = launchProfileBox.Items[i] as LaunchProfile;
                if (profile != null && string.Equals(profile.Id, settings.LaunchProfile, StringComparison.OrdinalIgnoreCase))
                {
                    launchProfileBox.SelectedIndex = i;
                    return;
                }
            }

            if (launchProfileBox.Items.Count > 0)
                launchProfileBox.SelectedIndex = 0;
        }

        private Panel BuildAccountPage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Account");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel signInCard = CreateCard(0, 54, 700, 280);
            page.Controls.Add(signInCard);
            Label title = CreateCardTitle("Microsoft Account");
            title.Location = new Point(20, 18);
            signInCard.Controls.Add(title);

            Label note = CreateMutedLabel("Sign in through Minecraft, Xbox, or Windows. Bomb Client never asks for your Microsoft password and does not store account tokens.");
            note.Location = new Point(22, 56);
            note.Size = new Size(620, 48);
            signInCard.Controls.Add(note);

            Button minecraftSignIn = CreatePrimaryButton("Open Minecraft Sign-In");
            minecraftSignIn.Location = new Point(22, 120);
            minecraftSignIn.Size = new Size(190, 42);
            minecraftSignIn.Click += delegate
            {
                LaunchMinecraft();
                SetStatus("Use Minecraft's profile/sign-in button to sign in.");
            };
            signInCard.Controls.Add(minecraftSignIn);

            Button xboxSignIn = CreateSecondaryButton("Open Xbox App");
            xboxSignIn.Location = new Point(230, 120);
            xboxSignIn.Size = new Size(150, 42);
            xboxSignIn.Click += delegate { OpenAccountTarget("xbox:", "Xbox app opened."); };
            signInCard.Controls.Add(xboxSignIn);

            Button windowsAccounts = CreateSecondaryButton("Windows Accounts");
            windowsAccounts.Location = new Point(398, 120);
            windowsAccounts.Size = new Size(170, 42);
            windowsAccounts.Click += delegate { OpenAccountTarget("ms-settings:emailandaccounts", "Windows account settings opened."); };
            signInCard.Controls.Add(windowsAccounts);

            Button minecraftWeb = CreateSecondaryButton("Minecraft Account Page");
            minecraftWeb.Location = new Point(22, 178);
            minecraftWeb.Size = new Size(190, 42);
            minecraftWeb.Click += delegate { OpenAccountTarget("https://www.minecraft.net/msaprofile", "Minecraft account page opened."); };
            signInCard.Controls.Add(minecraftWeb);

            Button microsoftWeb = CreateSecondaryButton("Microsoft Account");
            microsoftWeb.Location = new Point(230, 178);
            microsoftWeb.Size = new Size(150, 42);
            microsoftWeb.Click += delegate { OpenAccountTarget("https://account.microsoft.com/", "Microsoft account page opened."); };
            signInCard.Controls.Add(microsoftWeb);

            Button store = CreateSecondaryButton("Microsoft Store");
            store.Location = new Point(398, 178);
            store.Size = new Size(170, 42);
            store.Click += delegate { OpenAccountTarget("ms-windows-store://home", "Microsoft Store opened."); };
            signInCard.Controls.Add(store);

            Label hint = CreateMutedLabel("If Minecraft says you are not signed in, sign into the Xbox app and Windows account settings first, then reopen Minecraft from Bomb Client.");
            hint.Location = new Point(24, 236);
            hint.Size = new Size(620, 28);
            signInCard.Controls.Add(hint);

            Panel statusCard = CreateCard(0, 360, 700, 146);
            page.Controls.Add(statusCard);
            Label statusTitle = CreateCardTitle("Account Status");
            statusTitle.Location = new Point(20, 18);
            statusCard.Controls.Add(statusTitle);

            Label status = CreateMutedLabel("Microsoft sign-in status is protected by Minecraft/Xbox. Bomb Client can open the official sign-in surfaces, but the game decides which account is active.");
            status.Location = new Point(22, 58);
            status.Size = new Size(620, 54);
            statusCard.Controls.Add(status);

            return page;
        }

        private Panel BuildVisualPage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Visual Pack");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel card = CreateCard(0, 54, 620, 330);
            page.Controls.Add(card);
            Label title = CreateCardTitle("Bomb Client PvP Pack");
            title.Location = new Point(20, 18);
            card.Controls.Add(title);

            lowFireCheck = CreateCheck("Low fire", settings.VisualLowFire);
            lowFireCheck.Location = new Point(22, 62);
            card.Controls.Add(lowFireCheck);

            noBobberCheck = CreateCheck("No bobber", settings.VisualNoBobber);
            noBobberCheck.Location = new Point(22, 102);
            card.Controls.Add(noBobberCheck);

            cleanPumpkinCheck = CreateCheck("Clean pumpkin", settings.VisualCleanPumpkin);
            cleanPumpkinCheck.Location = new Point(22, 142);
            card.Controls.Add(cleanPumpkinCheck);

            clearVignetteCheck = CreateCheck("Clear vignette", settings.VisualClearVignette);
            clearVignetteCheck.Location = new Point(22, 182);
            card.Controls.Add(clearVignetteCheck);

            Button build = CreatePrimaryButton("Build .mcpack");
            build.Location = new Point(22, 238);
            build.Size = new Size(150, 42);
            build.Click += delegate { BuildPack(false); };
            card.Controls.Add(build);

            Button import = CreateSecondaryButton("Build and Import");
            import.Location = new Point(190, 238);
            import.Size = new Size(158, 42);
            import.Click += delegate { BuildPack(true); };
            card.Controls.Add(import);

            Button openPacks = CreateSecondaryButton("Open Pack Folder");
            openPacks.Location = new Point(366, 238);
            openPacks.Size = new Size(150, 42);
            openPacks.Click += delegate
            {
                Directory.CreateDirectory(AppPaths.PackRoot);
                Process.Start(AppPaths.PackRoot);
            };
            card.Controls.Add(openPacks);

            return page;
        }

        private Panel BuildSettingsPage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Settings");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel serverCard = CreateCard(0, 54, 620, 210);
            page.Controls.Add(serverCard);
            Label serverTitle = CreateCardTitle("Server Ping");
            serverTitle.Location = new Point(20, 18);
            serverCard.Controls.Add(serverTitle);

            Label hostLabel = CreateMutedLabel("Host");
            hostLabel.Location = new Point(22, 62);
            hostLabel.Size = new Size(80, 24);
            serverCard.Controls.Add(hostLabel);

            serverHostBox = new TextBox();
            serverHostBox.Text = settings.ServerHost;
            serverHostBox.Location = new Point(110, 60);
            serverHostBox.Width = 260;
            serverHostBox.BackColor = Color.FromArgb(30, 37, 51);
            serverHostBox.ForeColor = text;
            serverHostBox.BorderStyle = BorderStyle.FixedSingle;
            serverCard.Controls.Add(serverHostBox);

            Label portLabel = CreateMutedLabel("Port");
            portLabel.Location = new Point(22, 102);
            portLabel.Size = new Size(80, 24);
            serverCard.Controls.Add(portLabel);

            serverPortBox = new NumericUpDown();
            serverPortBox.Minimum = 1;
            serverPortBox.Maximum = 65535;
            serverPortBox.Value = settings.ServerPort;
            serverPortBox.Location = new Point(110, 100);
            serverPortBox.Width = 100;
            serverPortBox.BackColor = Color.FromArgb(30, 37, 51);
            serverPortBox.ForeColor = text;
            serverCard.Controls.Add(serverPortBox);

            Button saveServer = CreatePrimaryButton("Save");
            saveServer.Location = new Point(22, 146);
            saveServer.Size = new Size(110, 38);
            saveServer.Click += delegate
            {
                PersistSettingsFromUi();
                settings.Save();
                SetStatus("Settings saved.");
            };
            serverCard.Controls.Add(saveServer);

            Panel hudCard = CreateCard(0, 288, 620, 160);
            page.Controls.Add(hudCard);
            Label hudTitle = CreateCardTitle("Overlay Opacity");
            hudTitle.Location = new Point(20, 18);
            hudCard.Controls.Add(hudTitle);

            opacityTrack = new TrackBar();
            opacityTrack.Minimum = 40;
            opacityTrack.Maximum = 100;
            opacityTrack.TickFrequency = 10;
            opacityTrack.Value = settings.OverlayOpacity;
            opacityTrack.Location = new Point(20, 60);
            opacityTrack.Width = 360;
            opacityTrack.BackColor = panel;
            opacityTrack.Scroll += delegate
            {
                settings.OverlayOpacity = opacityTrack.Value;
                OverlayManager.SetOpacity(settings.OverlayOpacity);
                opacityLabel.Text = settings.OverlayOpacity.ToString() + "%";
            };
            hudCard.Controls.Add(opacityTrack);

            opacityLabel = CreateMutedLabel(settings.OverlayOpacity.ToString() + "%");
            opacityLabel.Location = new Point(400, 67);
            opacityLabel.Size = new Size(80, 24);
            hudCard.Controls.Add(opacityLabel);

            Panel behaviorCard = CreateCard(0, 472, 620, 132);
            page.Controls.Add(behaviorCard);
            Label behaviorTitle = CreateCardTitle("Overlay Behavior");
            behaviorTitle.Location = new Point(20, 18);
            behaviorCard.Controls.Add(behaviorTitle);

            gameplayOnlyCheck = CreateCheck("Only show overlays during active gameplay", settings.OnlyShowInWorld);
            gameplayOnlyCheck.Location = new Point(22, 60);
            gameplayOnlyCheck.CheckedChanged += delegate
            {
                settings.OnlyShowInWorld = gameplayOnlyCheck.Checked;
                OverlayManager.RefreshVisibility();
                settings.Save();
            };
            behaviorCard.Controls.Add(gameplayOnlyCheck);

            Label behaviorNote = CreateMutedLabel("Uses Minecraft focus plus hidden cursor detection, so menus and other apps stay clean.");
            behaviorNote.Location = new Point(24, 88);
            behaviorNote.Size = new Size(520, 28);
            behaviorCard.Controls.Add(behaviorNote);

            return page;
        }

        private Panel CreateOverlayCard(OverlayDefinition def)
        {
            Panel card = new Panel();
            card.Width = 420;
            card.Height = 118;
            card.Margin = new Padding(0, 0, 16, 16);
            card.BackColor = panel;
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                DrawRoundedPanel(e.Graphics, card.ClientRectangle, panel, 8);
            };

            Label title = CreateCardTitle(def.Name);
            title.Location = new Point(18, 14);
            title.Size = new Size(280, 26);
            card.Controls.Add(title);

            Label desc = CreateMutedLabel(def.ShortText);
            desc.Location = new Point(20, 45);
            desc.Size = new Size(272, 52);
            card.Controls.Add(desc);

            Button toggle = CreateToggleButton(IsOverlayEnabled(def.Id));
            toggle.Location = new Point(308, 38);
            toggle.Click += delegate
            {
                bool next = !IsOverlayEnabled(def.Id);
                settings.Overlays[def.Id] = next;
                settings.Save();
                UpdateToggleButton(toggle, next);
                OverlayManager.SetOverlay(def.Id, next);
            };
            toggleButtons[def.Id] = toggle;
            card.Controls.Add(toggle);

            card.Resize += delegate { LayoutOverlayCard(card, title, desc, toggle); };
            LayoutOverlayCard(card, title, desc, toggle);

            return card;
        }

        private void LayoutOverlayCard(Panel card, Label title, Label desc, Button toggle)
        {
            toggle.Location = new Point(Math.Max(224, card.Width - 104), 38);
            title.Size = new Size(Math.Max(180, card.Width - 136), 26);
            desc.Size = new Size(Math.Max(180, card.Width - 142), 52);
            card.Invalidate();
        }

        private bool IsOverlayEnabled(string id)
        {
            bool enabled = false;
            settings.Overlays.TryGetValue(id, out enabled);
            return enabled;
        }

        private void ShowPage(string name)
        {
            foreach (KeyValuePair<string, Panel> entry in pages)
                entry.Value.Visible = string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase);
        }

        private void PersistSettingsFromUi()
        {
            if (serverHostBox != null)
                settings.ServerHost = serverHostBox.Text.Trim().Length == 0 ? "play.cubecraft.net" : serverHostBox.Text.Trim();
            if (serverPortBox != null)
                settings.ServerPort = (int)serverPortBox.Value;
            if (opacityTrack != null)
                settings.OverlayOpacity = opacityTrack.Value;
            if (gameplayOnlyCheck != null)
                settings.OnlyShowInWorld = gameplayOnlyCheck.Checked;
            if (launchProfileBox != null)
            {
                LaunchProfile selected = launchProfileBox.SelectedItem as LaunchProfile;
                if (selected != null)
                    settings.LaunchProfile = selected.Id;
            }
            if (customLaunchBox != null)
                settings.CustomLaunchTarget = customLaunchBox.Text.Trim();
            if (lowFireCheck != null)
                settings.VisualLowFire = lowFireCheck.Checked;
            if (noBobberCheck != null)
                settings.VisualNoBobber = noBobberCheck.Checked;
            if (cleanPumpkinCheck != null)
                settings.VisualCleanPumpkin = cleanPumpkinCheck.Checked;
            if (clearVignetteCheck != null)
                settings.VisualClearVignette = clearVignetteCheck.Checked;

            OverlayManager.Configure(settings);
        }

        private void LaunchMinecraft()
        {
            PersistSettingsFromUi();
            LaunchProfile profile = LaunchProfileCatalog.Get(settings.LaunchProfile);
            try
            {
                if (profile.IsCustom)
                {
                    LaunchCustomTarget(settings.CustomLaunchTarget);
                }
                else if (string.Equals(profile.Id, "release", StringComparison.OrdinalIgnoreCase) &&
                    TryLaunchShellTarget(profile.FallbackUri))
                {
                }
                else if (TryLaunchShellTarget("shell:AppsFolder\\" + profile.AppsFolderId))
                {
                }
                else if (profile.FallbackUri.Length > 0 && TryLaunchShellTarget(profile.FallbackUri))
                {
                }
                else
                {
                    throw new InvalidOperationException("Windows did not activate the app target.");
                }
                SetStatus(profile.Name + " launch requested.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not launch " + profile.Name + ".\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool TryLaunchShellTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = target;
                info.UseShellExecute = true;
                Process.Start(info);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LaunchCustomTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException("Custom launch target is empty. Add a shortcut, exe path, URI, or command in Profiles.");

            string trimmed = target.Trim();
            if (File.Exists(trimmed) || Directory.Exists(trimmed) || trimmed.IndexOf(":", StringComparison.Ordinal) > 1)
            {
                TryLaunchShellTarget(trimmed);
                return;
            }

            if (!TryLaunchShellTarget(trimmed))
                throw new InvalidOperationException("Windows could not open the custom launch target.");
        }

        private void OpenAccountTarget(string target, string successMessage)
        {
            try
            {
                Process.Start(target);
                SetStatus(successMessage);
            }
            catch
            {
                try
                {
                    if (target.StartsWith("xbox:", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start("https://www.xbox.com/");
                        SetStatus("Xbox web page opened.");
                    }
                    else if (target.StartsWith("ms-windows-store:", StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start("https://www.microsoft.com/store/apps");
                        SetStatus("Microsoft Store web page opened.");
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not open this sign-in page.\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OpenBedrockFolder()
        {
            LaunchProfile profile = LaunchProfileCatalog.Get(settings.LaunchProfile);
            string folder = profile.PackageFolder.Length == 0 ? LaunchProfileCatalog.Release.PackageFolder : profile.PackageFolder;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", folder);
            if (Directory.Exists(path))
                Process.Start(path);
            else
                MessageBox.Show(profile.Name + " package folder was not found.", "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildPack(bool importAfterBuild)
        {
            try
            {
                PersistSettingsFromUi();
                string pack = ResourcePackBuilder.Build(settings);
                SetStatus("Pack built: " + pack);
                if (importAfterBuild)
                    Process.Start(pack);
                else
                    Process.Start("explorer.exe", "/select,\"" + pack + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Pack build failed.\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateMinecraftStatus()
        {
            bool running = MinecraftInfo.IsMinecraftRunning();
            bool installed = MinecraftInfo.IsMinecraftInstalled();
            string textStatus = running ? "Minecraft is running" : (installed ? "Minecraft is installed" : "Minecraft was not found");
            if (minecraftLabel != null)
                minecraftLabel.Text = textStatus;
            if (statusLabel != null && statusLabel.Text == "Ready")
                statusLabel.Text = textStatus;
        }

        private void SetStatus(string message)
        {
            statusLabel.Text = message;
        }

        private Panel CreatePage()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = bg;
            return page;
        }

        private Label CreateHeading(string value)
        {
            Label label = new Label();
            label.Text = value;
            label.ForeColor = text;
            label.Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold);
            label.AutoSize = true;
            return label;
        }

        private Label CreateCardTitle(string value)
        {
            Label label = new Label();
            label.Text = value;
            label.ForeColor = text;
            label.Font = headerFont;
            label.AutoSize = false;
            label.Size = new Size(360, 28);
            return label;
        }

        private Label CreateMutedLabel(string value)
        {
            Label label = new Label();
            label.Text = value;
            label.ForeColor = muted;
            label.Font = uiFont;
            label.AutoSize = false;
            return label;
        }

        private Panel CreateCard(int x, int y, int w, int h)
        {
            Panel card = new Panel();
            card.Location = new Point(x, y);
            card.Size = new Size(w, h);
            card.BackColor = panel;
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                DrawRoundedPanel(e.Graphics, card.ClientRectangle, panel, 8);
            };
            return card;
        }

        private Button CreatePrimaryButton(string label)
        {
            Button button = CreateBaseButton(label);
            button.BackColor = red;
            button.ForeColor = Color.White;
            return button;
        }

        private Button CreateSecondaryButton(string label)
        {
            Button button = CreateBaseButton(label);
            button.BackColor = panel2;
            button.ForeColor = text;
            return button;
        }

        private Button CreateBaseButton(string label)
        {
            Button button = new Button();
            button.Text = label;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Size = new Size(150, 40);
            return button;
        }

        private Button CreateNavButton(string label)
        {
            Button button = CreateSecondaryButton(label);
            button.Size = new Size(166, 38);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(14, 0, 0, 0);
            return button;
        }

        private CheckBox CreateCheck(string label, bool isChecked)
        {
            CheckBox check = new CheckBox();
            check.Text = label;
            check.Checked = isChecked;
            check.ForeColor = text;
            check.AutoSize = true;
            check.FlatStyle = FlatStyle.Flat;
            return check;
        }

        private Button CreateToggleButton(bool enabled)
        {
            Button button = CreateBaseButton("");
            button.Size = new Size(78, 34);
            UpdateToggleButton(button, enabled);
            return button;
        }

        private void UpdateToggleButton(Button button, bool enabled)
        {
            button.Text = enabled ? "ON" : "OFF";
            button.BackColor = enabled ? orange : Color.FromArgb(45, 53, 69);
            button.ForeColor = enabled ? Color.FromArgb(20, 20, 24) : muted;
        }

        private static void DrawRoundedPanel(Graphics g, Rectangle bounds, Color fill, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (GraphicsPath path = RoundedRect(r, radius))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen border = new Pen(Color.FromArgb(38, 47, 63)))
            {
                g.FillPath(brush, path);
                g.DrawPath(border, path);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class OverlayDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string ShortText;
        public readonly bool DefaultEnabled;
        public readonly Point DefaultPosition;

        public OverlayDefinition(string id, string name, string shortText, bool defaultEnabled, int x, int y)
        {
            Id = id;
            Name = name;
            ShortText = shortText;
            DefaultEnabled = defaultEnabled;
            DefaultPosition = new Point(x, y);
        }
    }

    internal static class OverlayCatalog
    {
        public static readonly OverlayDefinition[] All = new OverlayDefinition[]
        {
            new OverlayDefinition("fps", "FPS", "Small live framerate readout for the overlay layer.", true, 22, 22),
            new OverlayDefinition("ping", "Ping", "Bedrock server ping from the host and port in settings.", true, 22, 64),
            new OverlayDefinition("cps", "CPS", "Left and right click speed counters.", true, 22, 106),
            new OverlayDefinition("keystrokes", "Keystrokes", "WASD, jump, sneak, and mouse button input display.", true, 22, 154),
            new OverlayDefinition("combo", "Combo", "Click streak counter for PvP practice.", false, 22, 330),
            new OverlayDefinition("crosshair", "Crosshair", "Simple external center-screen crosshair.", false, 0, 0),
            new OverlayDefinition("clock", "Clock", "Compact local time display.", false, 22, 378),
            new OverlayDefinition("session", "Session Timer", "Timer for your current Bomb Client session.", false, 22, 420),
            new OverlayDefinition("memory", "Minecraft RAM", "Memory usage for the Bedrock process when it is running.", true, 22, 462),
            new OverlayDefinition("system", "System Stats", "PC memory and CPU load readout.", false, 22, 504),
            new OverlayDefinition("server", "Server Info", "Current configured Bedrock server target.", false, 22, 546),
            new OverlayDefinition("status", "Game Status", "Shows whether Minecraft Bedrock is running.", false, 22, 588)
        };

        public static OverlayDefinition Find(string id)
        {
            foreach (OverlayDefinition def in All)
            {
                if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
                    return def;
            }
            return null;
        }
    }

    internal static class OverlayManager
    {
        private static readonly Dictionary<string, BaseOverlayForm> open = new Dictionary<string, BaseOverlayForm>(StringComparer.OrdinalIgnoreCase);
        private static AppSettings settings;
        private static Timer visibilityTimer;
        private static bool lastGameplayVisible = true;

        public static void Configure(AppSettings current)
        {
            settings = current;
            EnsureVisibilityTimer();
            UpdateVisibilityState();
        }

        public static void ApplyConfiguredOverlays()
        {
            if (settings == null)
                return;

            foreach (OverlayDefinition def in OverlayCatalog.All)
            {
                bool enabled = false;
                settings.Overlays.TryGetValue(def.Id, out enabled);
                SetOverlay(def.Id, enabled);
            }
        }

        public static void SetOverlay(string id, bool enabled)
        {
            if (enabled)
                Show(id);
            else
                Hide(id);
        }

        public static void Show(string id)
        {
            if (settings == null)
                return;
            if (open.ContainsKey(id))
                return;

            OverlayDefinition def = OverlayCatalog.Find(id);
            if (def == null)
                return;

            BaseOverlayForm form = CreateOverlay(id);
            if (form == null)
                return;

            Point pos;
            if (!settings.Positions.TryGetValue(id, out pos))
                pos = def.DefaultPosition;

            if (id == "crosshair")
            {
                form.Bounds = Screen.PrimaryScreen.Bounds;
            }
            else
            {
                form.Location = pos;
            }

            form.EditMode = settings.EditMode;
            form.Opacity = Math.Max(0.4, Math.Min(1.0, settings.OverlayOpacity / 100.0));
            form.PositionChanged += delegate(string moduleId, Point location)
            {
                settings.Positions[moduleId] = location;
                settings.Save();
            };
            form.FormClosed += delegate { open.Remove(id); };
            open[id] = form;
            form.Show();
            UpdateVisibilityState();
        }

        public static void Hide(string id)
        {
            BaseOverlayForm form;
            if (open.TryGetValue(id, out form))
            {
                open.Remove(id);
                form.Close();
            }
        }

        public static void CloseAll()
        {
            BaseOverlayForm[] forms = new BaseOverlayForm[open.Values.Count];
            open.Values.CopyTo(forms, 0);
            open.Clear();
            foreach (BaseOverlayForm form in forms)
                form.Close();
        }

        public static void RefreshVisibility()
        {
            UpdateVisibilityState();
        }

        public static void SetEditMode(bool editMode)
        {
            if (settings != null)
                settings.EditMode = editMode;
            foreach (BaseOverlayForm form in open.Values)
                form.EditMode = editMode;
            UpdateVisibilityState();
        }

        public static void SetOpacity(int opacity)
        {
            if (settings != null)
                settings.OverlayOpacity = opacity;
            foreach (BaseOverlayForm form in open.Values)
                form.Opacity = Math.Max(0.4, Math.Min(1.0, opacity / 100.0));
        }

        private static void EnsureVisibilityTimer()
        {
            if (visibilityTimer != null)
                return;

            visibilityTimer = new Timer();
            visibilityTimer.Interval = 180;
            visibilityTimer.Tick += delegate { UpdateVisibilityState(); };
            visibilityTimer.Start();
        }

        private static void UpdateVisibilityState()
        {
            if (settings == null)
                return;

            bool shouldDisplay = !settings.OnlyShowInWorld || settings.EditMode || MinecraftInfo.IsGameplayActive();
            if (shouldDisplay == lastGameplayVisible && open.Count == 0)
                return;

            lastGameplayVisible = shouldDisplay;
            foreach (BaseOverlayForm form in open.Values)
            {
                if (form.Visible != shouldDisplay)
                    form.Visible = shouldDisplay;
            }
        }

        private static BaseOverlayForm CreateOverlay(string id)
        {
            if (id == "fps")
                return new TextOverlayForm(id, 98, 36, delegate { return "FPS " + FrameCounter.CurrentFps.ToString(); }, true);
            if (id == "ping")
                return new PingOverlayForm(id, settings);
            if (id == "cps")
                return new TextOverlayForm(id, 164, 36, delegate { return "LMB " + InputTracker.LeftCps.ToString() + " | RMB " + InputTracker.RightCps.ToString(); });
            if (id == "keystrokes")
                return new KeystrokesOverlayForm(id);
            if (id == "combo")
                return new TextOverlayForm(id, 122, 36, delegate { return "COMBO " + InputTracker.ComboCount.ToString(); });
            if (id == "crosshair")
                return new CrosshairOverlayForm(id);
            if (id == "clock")
                return new TextOverlayForm(id, 130, 36, delegate { return DateTime.Now.ToString("h:mm:ss tt"); });
            if (id == "session")
                return new TextOverlayForm(id, 138, 36, delegate { return "SESSION " + SessionClock.ElapsedText; });
            if (id == "memory")
                return new TextOverlayForm(id, 178, 36, delegate { return MinecraftInfo.MemoryText(); });
            if (id == "system")
                return new TextOverlayForm(id, 180, 36, delegate { return SystemInfo.StatusText(); });
            if (id == "server")
                return new TextOverlayForm(id, 230, 36, delegate { return settings.ServerHost + ":" + settings.ServerPort.ToString(); });
            if (id == "status")
                return new TextOverlayForm(id, 176, 36, delegate { return MinecraftInfo.IsMinecraftRunning() ? "BEDROCK RUNNING" : "BEDROCK CLOSED"; });
            return null;
        }
    }

    internal abstract class BaseOverlayForm : Form
    {
        private bool editMode;
        private Point dragStart;
        private bool dragging;
        protected readonly string ModuleId;
        protected readonly Timer Timer;
        public event Action<string, Point> PositionChanged;

        protected BaseOverlayForm(string moduleId, int width, int height)
        {
            ModuleId = moduleId;
            Width = width;
            Height = height;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            DoubleBuffered = true;

            Timer = new Timer();
            Timer.Interval = 120;
            Timer.Tick += delegate
            {
                Invalidate();
            };

            MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (!EditMode || e.Button != MouseButtons.Left)
                    return;
                dragging = true;
                dragStart = e.Location;
            };

            MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!dragging)
                    return;
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - dragStart.X, p.Y - dragStart.Y);
            };

            MouseUp += delegate
            {
                if (!dragging)
                    return;
                dragging = false;
                if (PositionChanged != null)
                    PositionChanged(ModuleId, Location);
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_LAYERED;
                if (!editMode)
                    cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        public bool EditMode
        {
            get { return editMode; }
            set
            {
                editMode = value;
                if (IsHandleCreated)
                {
                    int style = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
                    if (editMode)
                        style &= ~NativeMethods.WS_EX_TRANSPARENT;
                    else
                        style |= NativeMethods.WS_EX_TRANSPARENT;
                    style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_LAYERED;
                    NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_EXSTYLE, style);
                }
                Invalidate();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Timer.Stop();
            base.OnFormClosed(e);
        }

        protected void DrawOverlayBack(Graphics g, Rectangle bounds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), 8))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(24, 29, 39)))
            using (Pen border = new Pen(EditMode ? Color.FromArgb(255, 164, 58) : Color.FromArgb(58, 67, 84)))
            {
                g.FillPath(brush, path);
                g.DrawPath(border, path);
            }
        }

        protected static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class TextOverlayForm : BaseOverlayForm
    {
        private readonly Func<string> provider;
        private readonly bool trackFrames;
        private readonly Font font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);

        public TextOverlayForm(string moduleId, int width, int height, Func<string> textProvider)
            : this(moduleId, width, height, textProvider, false)
        {
        }

        public TextOverlayForm(string moduleId, int width, int height, Func<string> textProvider, bool measureFrames)
            : base(moduleId, width, height)
        {
            provider = textProvider;
            trackFrames = measureFrames;
            if (trackFrames)
                Timer.Interval = 16;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (trackFrames)
                FrameCounter.Tick();
            DrawOverlayBack(e.Graphics, ClientRectangle);
            string value = provider();
            TextRenderer.DrawText(e.Graphics, value, font, new Rectangle(10, 7, Width - 20, Height - 12), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            base.OnPaint(e);
        }
    }

    internal sealed class PingOverlayForm : BaseOverlayForm
    {
        private readonly AppSettings settings;
        private readonly Font font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        private string current = "PING ...";
        private DateTime lastPing = DateTime.MinValue;
        private bool active;

        public PingOverlayForm(string moduleId, AppSettings appSettings)
            : base(moduleId, 138, 36)
        {
            settings = appSettings;
            Timer.Interval = 250;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawOverlayBack(e.Graphics, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, current, font, new Rectangle(10, 7, Width - 20, Height - 12), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            if ((DateTime.UtcNow - lastPing).TotalSeconds > 2 && !active)
                BeginPing();
            base.OnPaint(e);
        }

        private void BeginPing()
        {
            active = true;
            lastPing = DateTime.UtcNow;
            Task.Factory.StartNew(delegate
            {
                return PingTools.Measure(settings.ServerHost, settings.ServerPort);
            }).ContinueWith(delegate(Task<long?> task)
            {
                if (IsDisposed)
                    return;
                long? ms = null;
                if (task.Status == TaskStatus.RanToCompletion)
                    ms = task.Result;
                BeginInvoke(new Action(delegate
                {
                    current = ms.HasValue ? "PING " + ms.Value.ToString() + "ms" : "PING --";
                    active = false;
                    Invalidate();
                }));
            });
        }
    }

    internal sealed class KeystrokesOverlayForm : BaseOverlayForm
    {
        private readonly Font keyFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        private readonly Font smallFont = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);

        public KeystrokesOverlayForm(string moduleId)
            : base(moduleId, 164, 164)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawOverlayBack(e.Graphics, ClientRectangle);
            DrawKey(e.Graphics, "W", Keys.W, 62, 12, 40, 34);
            DrawKey(e.Graphics, "A", Keys.A, 18, 50, 40, 34);
            DrawKey(e.Graphics, "S", Keys.S, 62, 50, 40, 34);
            DrawKey(e.Graphics, "D", Keys.D, 106, 50, 40, 34);
            DrawMouse(e.Graphics, "LMB", InputTracker.LeftDown, 18, 92, 62, 28);
            DrawMouse(e.Graphics, "RMB", InputTracker.RightDown, 84, 92, 62, 28);
            DrawKey(e.Graphics, "SPACE", Keys.Space, 18, 126, 86, 26);
            DrawKey(e.Graphics, "SHIFT", Keys.ShiftKey, 108, 126, 38, 26);
            base.OnPaint(e);
        }

        private void DrawKey(Graphics g, string label, Keys key, int x, int y, int w, int h)
        {
            bool down = InputTracker.IsDown(key);
            DrawKeyBox(g, label, down, x, y, w, h, label.Length > 1 ? smallFont : keyFont);
        }

        private void DrawMouse(Graphics g, string label, bool down, int x, int y, int w, int h)
        {
            DrawKeyBox(g, label, down, x, y, w, h, smallFont);
        }

        private void DrawKeyBox(Graphics g, string label, bool down, int x, int y, int w, int h, Font font)
        {
            Rectangle rect = new Rectangle(x, y, w, h);
            using (GraphicsPath path = RoundedRect(rect, 6))
            using (SolidBrush brush = new SolidBrush(down ? Color.FromArgb(255, 164, 58) : Color.FromArgb(37, 44, 58)))
            using (Pen pen = new Pen(down ? Color.FromArgb(255, 203, 104) : Color.FromArgb(64, 75, 94)))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, label, font, rect, down ? Color.FromArgb(20, 20, 24) : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class CrosshairOverlayForm : BaseOverlayForm
    {
        public CrosshairOverlayForm(string moduleId)
            : base(moduleId, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        {
            TopMost = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int cx = Width / 2;
            int cy = Height / 2;
            using (Pen shadow = new Pen(Color.FromArgb(160, 0, 0, 0), 3f))
            using (Pen hot = new Pen(Color.FromArgb(255, 245, 80, 58), 1.6f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLine(shadow, cx - 12, cy, cx - 4, cy);
                e.Graphics.DrawLine(shadow, cx + 4, cy, cx + 12, cy);
                e.Graphics.DrawLine(shadow, cx, cy - 12, cx, cy - 4);
                e.Graphics.DrawLine(shadow, cx, cy + 4, cx, cy + 12);
                e.Graphics.DrawLine(hot, cx - 12, cy, cx - 4, cy);
                e.Graphics.DrawLine(hot, cx + 4, cy, cx + 12, cy);
                e.Graphics.DrawLine(hot, cx, cy - 12, cx, cy - 4);
                e.Graphics.DrawLine(hot, cx, cy + 4, cx, cy + 12);
            }
            base.OnPaint(e);
        }
    }

    internal static class InputTracker
    {
        private static NativeMethods.HookProc keyboardProc = KeyboardHook;
        private static NativeMethods.HookProc mouseProc = MouseHook;
        private static IntPtr keyboardHook = IntPtr.Zero;
        private static IntPtr mouseHook = IntPtr.Zero;
        private static readonly bool[] keyDown = new bool[256];
        private static readonly Queue<DateTime> leftClicks = new Queue<DateTime>();
        private static readonly Queue<DateTime> rightClicks = new Queue<DateTime>();
        private static readonly object sync = new object();
        private static DateTime lastLeftClick = DateTime.MinValue;
        private static int comboCount;
        public static bool LeftDown;
        public static bool RightDown;

        public static int LeftCps
        {
            get
            {
                lock (sync)
                {
                    Trim(leftClicks);
                    return leftClicks.Count;
                }
            }
        }

        public static int RightCps
        {
            get
            {
                lock (sync)
                {
                    Trim(rightClicks);
                    return rightClicks.Count;
                }
            }
        }

        public static int ComboCount
        {
            get
            {
                lock (sync)
                {
                    if ((DateTime.UtcNow - lastLeftClick).TotalMilliseconds > 1400)
                        comboCount = 0;
                    return comboCount;
                }
            }
        }

        public static void Install()
        {
            if (keyboardHook == IntPtr.Zero)
                keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, keyboardProc, IntPtr.Zero, 0);
            if (mouseHook == IntPtr.Zero)
                mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, mouseProc, IntPtr.Zero, 0);
        }

        public static void Uninstall()
        {
            if (keyboardHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
            if (mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
        }

        public static bool IsDown(Keys key)
        {
            int index = (int)key;
            if (index < 0 || index >= keyDown.Length)
                return false;
            return keyDown[index];
        }

        private static IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode >= 0 && vkCode < keyDown.Length)
                {
                    int msg = wParam.ToInt32();
                    if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                    {
                        keyDown[vkCode] = true;
                        if (vkCode == (int)Keys.ShiftKey || vkCode == (int)Keys.LShiftKey || vkCode == (int)Keys.RShiftKey)
                            keyDown[(int)Keys.ShiftKey] = true;
                    }
                    else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                    {
                        keyDown[vkCode] = false;
                        if (vkCode == (int)Keys.ShiftKey || vkCode == (int)Keys.LShiftKey || vkCode == (int)Keys.RShiftKey)
                            keyDown[(int)Keys.ShiftKey] = false;
                    }
                }
            }
            return NativeMethods.CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        private static IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int msg = wParam.ToInt32();
                DateTime now = DateTime.UtcNow;
                lock (sync)
                {
                    if (msg == NativeMethods.WM_LBUTTONDOWN)
                    {
                        LeftDown = true;
                        leftClicks.Enqueue(now);
                        if ((now - lastLeftClick).TotalMilliseconds > 1400)
                            comboCount = 0;
                        comboCount++;
                        lastLeftClick = now;
                    }
                    else if (msg == NativeMethods.WM_LBUTTONUP)
                    {
                        LeftDown = false;
                    }
                    else if (msg == NativeMethods.WM_RBUTTONDOWN)
                    {
                        RightDown = true;
                        rightClicks.Enqueue(now);
                    }
                    else if (msg == NativeMethods.WM_RBUTTONUP)
                    {
                        RightDown = false;
                    }

                    Trim(leftClicks);
                    Trim(rightClicks);
                }
            }
            return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        private static void Trim(Queue<DateTime> queue)
        {
            DateTime cutoff = DateTime.UtcNow.AddSeconds(-1);
            while (queue.Count > 0 && queue.Peek() < cutoff)
                queue.Dequeue();
        }
    }

    internal static class PingTools
    {
        public static long? Measure(string host, int port)
        {
            long? rak = TryRakNetPing(host, port);
            if (rak.HasValue)
                return rak;

            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send(host, 1200);
                if (reply != null && reply.Status == IPStatus.Success)
                    return reply.RoundtripTime;
            }
            catch
            {
            }
            return null;
        }

        private static long? TryRakNetPing(string host, int port)
        {
            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(host);
                if (addresses.Length == 0)
                    return null;

                IPEndPoint endpoint = new IPEndPoint(addresses[0], port);
                using (UdpClient client = new UdpClient(addresses[0].AddressFamily))
                {
                    client.Client.ReceiveTimeout = 1200;
                    byte[] packet = BuildUnconnectedPing();
                    Stopwatch watch = Stopwatch.StartNew();
                    client.Send(packet, packet.Length, endpoint);
                    IPEndPoint remote = null;
                    byte[] response = client.Receive(ref remote);
                    watch.Stop();
                    if (response != null && response.Length > 0)
                        return watch.ElapsedMilliseconds;
                }
            }
            catch
            {
            }
            return null;
        }

        private static byte[] BuildUnconnectedPing()
        {
            byte[] magic = new byte[]
            {
                0x00, 0xff, 0xff, 0x00, 0xfe, 0xfe, 0xfe, 0xfe,
                0xfd, 0xfd, 0xfd, 0xfd, 0x12, 0x34, 0x56, 0x78
            };
            byte[] packet = new byte[1 + 8 + magic.Length + 8];
            packet[0] = 0x01;
            byte[] time = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(DateTime.UtcNow.Ticks));
            Buffer.BlockCopy(time, 0, packet, 1, 8);
            Buffer.BlockCopy(magic, 0, packet, 9, magic.Length);
            byte[] guid = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(0x424f4d42434c4945L));
            Buffer.BlockCopy(guid, 0, packet, 25, 8);
            return packet;
        }
    }

    internal static class MinecraftInfo
    {
        public static bool IsMinecraftInstalled()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe");
            return Directory.Exists(path);
        }

        public static bool IsMinecraftRunning()
        {
            return FindMinecraftProcess() != null;
        }

        public static bool IsGameplayActive()
        {
            return IsMinecraftForeground() && !IsCursorVisible();
        }

        public static bool IsMinecraftForeground()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return false;

            string title = GetWindowTitle(hwnd);
            if (title.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0)
                return false;

            try
            {
                Process process = Process.GetProcessById((int)pid);
                string name = process.ProcessName;
                if (name.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) &&
                    title.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch
            {
            }

            return false;
        }

        public static bool IsCursorVisible()
        {
            CURSORINFO info = new CURSORINFO();
            info.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
            if (!NativeMethods.GetCursorInfo(out info))
                return true;
            return (info.flags & NativeMethods.CURSOR_SHOWING) == NativeMethods.CURSOR_SHOWING;
        }

        public static string MemoryText()
        {
            Process process = FindMinecraftProcess();
            if (process == null)
                return "MC RAM --";
            try
            {
                long mb = process.WorkingSet64 / (1024 * 1024);
                return "MC RAM " + mb.ToString() + " MB";
            }
            catch
            {
                return "MC RAM --";
            }
        }

        private static Process FindMinecraftProcess()
        {
            Process[] exact = Process.GetProcessesByName("Minecraft.Windows");
            if (exact.Length > 0)
                return exact[0];
            Process[] all = Process.GetProcesses();
            foreach (Process p in all)
            {
                try
                {
                    if (p.ProcessName.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0)
                        return p;
                }
                catch
                {
                }
            }
            return null;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            StringBuilder builder = new StringBuilder(256);
            int length = NativeMethods.GetWindowText(hwnd, builder, builder.Capacity);
            if (length <= 0)
                return string.Empty;
            return builder.ToString();
        }
    }

    internal static class SystemInfo
    {
        private static readonly PerformanceCounter CpuCounter = CreateCpuCounter();

        public static string StatusText()
        {
            MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            NativeMethods.GlobalMemoryStatusEx(ref mem);
            int ram = (int)mem.dwMemoryLoad;
            int cpu = 0;
            try
            {
                if (CpuCounter != null)
                    cpu = (int)CpuCounter.NextValue();
            }
            catch
            {
            }
            return "CPU " + cpu.ToString() + "% | RAM " + ram.ToString() + "%";
        }

        private static PerformanceCounter CreateCpuCounter()
        {
            try
            {
                PerformanceCounter counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                counter.NextValue();
                return counter;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class FrameCounter
    {
        private static int frames;
        private static DateTime last = DateTime.UtcNow;
        private static int current = 60;

        public static int CurrentFps
        {
            get { return current; }
        }

        public static void Tick()
        {
            frames++;
            DateTime now = DateTime.UtcNow;
            double elapsed = (now - last).TotalSeconds;
            if (elapsed >= 1.0)
            {
                current = Math.Max(1, (int)Math.Round(frames / elapsed));
                frames = 0;
                last = now;
            }
        }
    }

    internal static class SessionClock
    {
        private static readonly DateTime Start = DateTime.UtcNow;

        public static string ElapsedText
        {
            get
            {
                TimeSpan span = DateTime.UtcNow - Start;
                if (span.TotalHours >= 1)
                    return ((int)span.TotalHours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
                return span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
            }
        }
    }

    internal static class ResourcePackBuilder
    {
        public static string Build(AppSettings settings)
        {
            Directory.CreateDirectory(AppPaths.PackRoot);
            string working = Path.Combine(AppPaths.PackRoot, "BombClientPvPPack");
            string output = Path.Combine(AppPaths.PackRoot, "BombClientPvPPack.mcpack");

            if (Directory.Exists(working))
                Directory.Delete(working, true);
            if (File.Exists(output))
                File.Delete(output);

            Directory.CreateDirectory(working);
            Directory.CreateDirectory(Path.Combine(working, "textures", "blocks"));
            Directory.CreateDirectory(Path.Combine(working, "textures", "entity"));
            Directory.CreateDirectory(Path.Combine(working, "textures", "misc"));

            File.WriteAllText(Path.Combine(working, "manifest.json"), BuildManifest(), Encoding.UTF8);
            SavePackIcon(Path.Combine(working, "pack_icon.png"));

            if (settings.VisualLowFire)
                WriteLowFireTextures(working);
            if (settings.VisualNoBobber)
                WriteTransparentTextures(working, new string[]
                {
                    Path.Combine("textures", "entity", "fishing_hook.png"),
                    Path.Combine("textures", "entity", "fishing_hook_marker.png")
                }, 32, 32);
            if (settings.VisualCleanPumpkin)
                WriteTransparentTextures(working, new string[] { Path.Combine("textures", "misc", "pumpkinblur.png") }, 256, 256);
            if (settings.VisualClearVignette)
                WriteTransparentTextures(working, new string[] { Path.Combine("textures", "misc", "vignette.png") }, 256, 256);

            ZipFile.CreateFromDirectory(working, output, CompressionLevel.Optimal, false);
            return output;
        }

        private static string BuildManifest()
        {
            string headerUuid = Guid.NewGuid().ToString();
            string moduleUuid = Guid.NewGuid().ToString();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"format_version\": 2,");
            sb.AppendLine("  \"header\": {");
            sb.AppendLine("    \"name\": \"Bomb Client PvP Pack\",");
            sb.AppendLine("    \"description\": \"Visual PvP pack generated by Bomb Client.\",");
            sb.AppendLine("    \"uuid\": \"" + headerUuid + "\",");
            sb.AppendLine("    \"version\": [1, 0, 0],");
            sb.AppendLine("    \"min_engine_version\": [1, 20, 0]");
            sb.AppendLine("  },");
            sb.AppendLine("  \"modules\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"type\": \"resources\",");
            sb.AppendLine("      \"uuid\": \"" + moduleUuid + "\",");
            sb.AppendLine("      \"version\": [1, 0, 0]");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void SavePackIcon(string path)
        {
            using (Image image = AssetLoader.LoadLogo())
            using (Bitmap bitmap = new Bitmap(256, 256))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, new Rectangle(12, 12, 232, 232));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static void WriteLowFireTextures(string working)
        {
            string[] names = new string[]
            {
                "fire_0.png", "fire_1.png", "fire_0_placeholder.png", "fire_1_placeholder.png",
                "soul_fire_0.png", "soul_fire_1.png", "soul_fire_0_placeholder.png", "soul_fire_1_placeholder.png"
            };
            for (int i = 0; i < names.Length; i++)
            {
                bool soul = names[i].StartsWith("soul_", StringComparison.OrdinalIgnoreCase);
                using (Bitmap bmp = CreateLowFireTexture(soul, i % 2 == 1))
                    bmp.Save(Path.Combine(working, "textures", "blocks", names[i]), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static Bitmap CreateLowFireTexture(bool soul, bool alternate)
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Color c1 = soul ? Color.FromArgb(210, 62, 240, 255) : Color.FromArgb(235, 255, 70, 22);
                Color c2 = soul ? Color.FromArgb(210, 30, 135, 255) : Color.FromArgb(230, 255, 174, 34);
                Color c3 = soul ? Color.FromArgb(190, 180, 255, 255) : Color.FromArgb(220, 255, 236, 117);
                Point[] outer = alternate
                    ? new Point[] { new Point(0, 15), new Point(3, 10), new Point(5, 14), new Point(8, 8), new Point(11, 14), new Point(13, 10), new Point(15, 15) }
                    : new Point[] { new Point(0, 15), new Point(2, 11), new Point(4, 15), new Point(7, 8), new Point(10, 15), new Point(13, 9), new Point(15, 15) };
                using (SolidBrush b = new SolidBrush(c1))
                    g.FillPolygon(b, outer);
                using (SolidBrush b = new SolidBrush(c2))
                    g.FillPolygon(b, new Point[] { new Point(3, 15), new Point(6, 11), new Point(8, 15), new Point(10, 11), new Point(13, 15) });
                using (SolidBrush b = new SolidBrush(c3))
                    g.FillPolygon(b, new Point[] { new Point(6, 15), new Point(8, 12), new Point(10, 15) });
            }
            return bmp;
        }

        private static void WriteTransparentTextures(string working, string[] relativePaths, int width, int height)
        {
            for (int i = 0; i < relativePaths.Length; i++)
            {
                string path = Path.Combine(working, relativePaths[i]);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (Bitmap bmp = new Bitmap(width, height))
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }

    internal static class AssetLoader
    {
        public static Image LoadLogo()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BombClientLogo.png");
            if (stream != null)
                return Image.FromStream(stream);

            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BombClientLogo.png");
            if (File.Exists(local))
                return Image.FromFile(local);

            Bitmap fallback = new Bitmap(64, 64);
            using (Graphics g = Graphics.FromImage(fallback))
            {
                g.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(225, 48, 48)))
                    g.FillEllipse(brush, 8, 8, 48, 48);
            }
            return fallback;
        }
    }

    internal static class NativeMethods
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int CURSOR_SHOWING = 0x00000001;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public NativePoint ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
