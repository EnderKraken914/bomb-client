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
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

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
                return SelfTestRunner.Run();
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
    }

    internal static class AppInfo
    {
        public const string Version = "2.0.0";
        public const string RepoOwner = "EnderKraken914";
        public const string RepoName = "bomb-client";
        public const string UpdateManifestUrl = "https://api.github.com/repos/EnderKraken914/bomb-client/contents/update.json?ref=main";
        public const string ReleaseDownloadUrl = "https://github.com/EnderKraken914/bomb-client/releases/download/v2.0.0/BombClient-Windows-2.0.0.zip";
    }

    internal sealed class UpdateManifest
    {
        public string LatestVersion = AppInfo.Version;
        public string RequiredVersion = AppInfo.Version;
        public string DownloadUrl = AppInfo.ReleaseDownloadUrl;
        public string ReleasePage = "https://github.com/" + AppInfo.RepoOwner + "/" + AppInfo.RepoName + "/releases/latest";
        public string Notes = "";
        public string ReleaseDate = "";
        public string ReleaseTitle = "";
        public string Changelog = "";
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
                    "\n\nOpen the public download now?",
                    "Bomb Client Update Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                    OpenUpdateDownload(manifest);

                return false;
            }

            if (manifest.HasOptionalUpdate())
            {
                DialogResult result = MessageBox.Show(
                    "A newer Bomb Client update is available.\n\nInstalled: " + AppInfo.Version +
                    "\nLatest: " + manifest.LatestVersion +
                    "\n\n" + manifest.Notes +
                    "\n\nOpen the public download now?",
                    "Bomb Client Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    OpenUpdateDownload(manifest);
            }

            return true;
        }

        public static UpdateManifest FetchManifest()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (TimeoutWebClient client = new TimeoutWebClient(5000))
                {
                    client.Headers.Add("User-Agent", "BombClient/" + AppInfo.Version);
                    client.Headers.Add("Accept", "application/vnd.github.raw+json");
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

        public static UpdateManifest ParseManifest(string json)
        {
            UpdateManifest manifest = new UpdateManifest();
            manifest.LatestVersion = ReadJsonString(json, "latest_version", manifest.LatestVersion);
            manifest.RequiredVersion = ReadJsonString(json, "required_version", manifest.RequiredVersion);
            manifest.DownloadUrl = ReadJsonString(json, "download_url", manifest.DownloadUrl);
            manifest.ReleasePage = ReadJsonString(json, "release_page", manifest.ReleasePage);
            manifest.ReleasePage = ReadJsonString(json, "github_release_url", manifest.ReleasePage);
            manifest.Notes = ReadJsonString(json, "notes", manifest.Notes);
            manifest.ReleaseDate = ReadJsonString(json, "release_date", manifest.ReleaseDate);
            manifest.ReleaseTitle = ReadJsonString(json, "release_title", manifest.ReleaseTitle);
            manifest.Changelog = ReadJsonString(json, "changelog", manifest.Changelog);
            manifest.ForceUpdate = ReadJsonBool(json, "force_update", manifest.ForceUpdate);
            if (manifest.Notes.Length == 0)
                manifest.Notes = manifest.Changelog;
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

        private static void OpenUpdateDownload(UpdateManifest manifest)
        {
            try
            {
                string target = string.IsNullOrWhiteSpace(manifest.DownloadUrl) ? manifest.ReleasePage : manifest.DownloadUrl;
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Bomb Client could not open the update download.\n\n" +
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

    internal sealed class MicrosoftDeviceCode
    {
        public string DeviceCode = "";
        public string UserCode = "";
        public string VerificationUri = "";
        public string VerificationUriComplete = "";
        public string Message = "";
        public int ExpiresIn = 900;
        public int Interval = 5;
    }

    internal sealed class MicrosoftAccountProfile
    {
        public string DisplayName = "";
        public string Email = "";
    }

    internal static class MicrosoftAccountAuthenticator
    {
        private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
        private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        private const string GraphMeUrl = "https://graph.microsoft.com/v1.0/me?$select=displayName,mail,userPrincipalName";
        private const string Scope = "User.Read openid profile email";

        public static MicrosoftAccountProfile SignIn(string clientId, Action<string> status)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Add a Microsoft OAuth client ID first.");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            MicrosoftDeviceCode device = RequestDeviceCode(clientId.Trim());
            if (status != null)
                status("Microsoft sign-in opened. Complete the browser prompt.");
            OpenVerificationPage(device);

            string accessToken = PollForAccessToken(clientId.Trim(), device, status);
            if (status != null)
                status("Microsoft approved the sign-in. Reading profile...");
            return FetchProfile(accessToken);
        }

        private static MicrosoftDeviceCode RequestDeviceCode(string clientId)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>();
            fields["client_id"] = clientId;
            fields["scope"] = Scope;

            string json = PostForm(DeviceCodeUrl, fields, 15000);
            MicrosoftDeviceCode device = new MicrosoftDeviceCode();
            device.DeviceCode = ReadJsonString(json, "device_code", "");
            device.UserCode = ReadJsonString(json, "user_code", "");
            device.VerificationUri = ReadJsonString(json, "verification_uri", "");
            device.VerificationUriComplete = ReadJsonString(json, "verification_uri_complete", "");
            device.Message = ReadJsonString(json, "message", "");
            device.ExpiresIn = ReadJsonInt(json, "expires_in", 900);
            device.Interval = Math.Max(2, ReadJsonInt(json, "interval", 5));

            if (device.DeviceCode.Length == 0 || device.VerificationUri.Length == 0)
                throw new InvalidOperationException("Microsoft did not return a usable sign-in code.");

            return device;
        }

        private static void OpenVerificationPage(MicrosoftDeviceCode device)
        {
            string target = device.VerificationUriComplete.Length == 0 ? device.VerificationUri : device.VerificationUriComplete;
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        private static string PollForAccessToken(string clientId, MicrosoftDeviceCode device, Action<string> status)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(device.ExpiresIn);
            int interval = device.Interval;

            while (DateTime.UtcNow < deadline)
            {
                System.Threading.Thread.Sleep(interval * 1000);

                Dictionary<string, string> fields = new Dictionary<string, string>();
                fields["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code";
                fields["client_id"] = clientId;
                fields["device_code"] = device.DeviceCode;

                string json;
                try
                {
                    json = PostForm(TokenUrl, fields, 15000);
                }
                catch (MicrosoftOAuthException ex)
                {
                    if (string.Equals(ex.Error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
                    {
                        if (status != null)
                            status("Waiting for Microsoft sign-in approval...");
                        continue;
                    }
                    if (string.Equals(ex.Error, "slow_down", StringComparison.OrdinalIgnoreCase))
                    {
                        interval += 5;
                        continue;
                    }
                    if (string.Equals(ex.Error, "authorization_declined", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Microsoft sign-in was declined.");
                    if (string.Equals(ex.Error, "expired_token", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Microsoft sign-in expired. Try again.");

                    throw new InvalidOperationException(ex.Description.Length == 0 ? "Microsoft sign-in failed." : ex.Description);
                }

                string accessToken = ReadJsonString(json, "access_token", "");
                if (accessToken.Length > 0)
                    return accessToken;
            }

            throw new InvalidOperationException("Microsoft sign-in expired. Try again.");
        }

        private static MicrosoftAccountProfile FetchProfile(string accessToken)
        {
            using (TimeoutWebClient client = new TimeoutWebClient(15000))
            {
                client.Headers.Add("Authorization", "Bearer " + accessToken);
                client.Headers.Add("User-Agent", "BombClient/" + AppInfo.Version);
                string json = client.DownloadString(GraphMeUrl);

                MicrosoftAccountProfile profile = new MicrosoftAccountProfile();
                profile.DisplayName = ReadJsonString(json, "displayName", "");
                profile.Email = ReadJsonString(json, "mail", "");
                if (profile.Email.Length == 0)
                    profile.Email = ReadJsonString(json, "userPrincipalName", "");
                if (profile.DisplayName.Length == 0)
                    profile.DisplayName = "Microsoft account";
                return profile;
            }
        }

        private static string PostForm(string url, Dictionary<string, string> fields, int timeoutMs)
        {
            using (TimeoutWebClient client = new TimeoutWebClient(timeoutMs))
            {
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                client.Headers.Add("User-Agent", "BombClient/" + AppInfo.Version);
                try
                {
                    return client.UploadString(url, "POST", EncodeForm(fields));
                }
                catch (WebException ex)
                {
                    string body = "";
                    if (ex.Response != null)
                    {
                        using (Stream stream = ex.Response.GetResponseStream())
                        {
                            if (stream != null)
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                    body = reader.ReadToEnd();
                            }
                        }
                    }

                    string error = ReadJsonString(body, "error", "");
                    string description = ReadJsonString(body, "error_description", "");
                    if (error.Length > 0)
                        throw new MicrosoftOAuthException(error, description);
                    throw;
                }
            }
        }

        private static string EncodeForm(Dictionary<string, string> fields)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> field in fields)
            {
                if (builder.Length > 0)
                    builder.Append('&');
                builder.Append(Uri.EscapeDataString(field.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(field.Value));
            }
            return builder.ToString();
        }

        private static string ReadJsonString(string json, string key, string fallback)
        {
            if (json == null)
                return fallback;
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
            if (!match.Success)
                return fallback;
            return Regex.Unescape(match.Groups[1].Value);
        }

        private static int ReadJsonInt(string json, string key, int fallback)
        {
            if (json == null)
                return fallback;
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return fallback;
            int value;
            return int.TryParse(match.Groups[1].Value, out value) ? value : fallback;
        }
    }

    internal sealed class MicrosoftOAuthException : Exception
    {
        public readonly string Error;
        public readonly string Description;

        public MicrosoftOAuthException(string error, string description)
            : base(description.Length == 0 ? error : description)
        {
            Error = error;
            Description = description;
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
        public int OverlayScale = 100;
        public bool EditMode = false;
        public bool OnlyShowInWorld = true;
        public bool VisualLowFire = true;
        public bool VisualNoBobber = true;
        public bool VisualLowShield = true;
        public bool VisualSmallTotem = true;
        public bool VisualSmallTotemPop = true;
        public int VisualShieldSize = 58;
        public int VisualTotemSize = 62;
        public int VisualTotemPopSize = 48;
        public bool VisualCleanPumpkin = true;
        public bool VisualClearVignette = true;
        public string MicrosoftClientId = "";
        public string AccountName = "";
        public string AccountEmail = "";
        public string AccountSignedInAt = "";

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
                else if (key.Equals("overlay.scale", StringComparison.OrdinalIgnoreCase))
                {
                    int scale;
                    if (int.TryParse(value, out scale))
                        settings.OverlayScale = Math.Max(60, Math.Min(180, scale));
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
                else if (key.Equals("visual.lowShield", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualLowShield = ParseBool(value, true);
                }
                else if (key.Equals("visual.smallTotem", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualSmallTotem = ParseBool(value, true);
                }
                else if (key.Equals("visual.smallTotemPop", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualSmallTotemPop = ParseBool(value, true);
                }
                else if (key.Equals("visual.shieldSize", StringComparison.OrdinalIgnoreCase))
                {
                    int size;
                    if (int.TryParse(value, out size))
                        settings.VisualShieldSize = Math.Max(20, Math.Min(100, size));
                }
                else if (key.Equals("visual.totemSize", StringComparison.OrdinalIgnoreCase))
                {
                    int size;
                    if (int.TryParse(value, out size))
                        settings.VisualTotemSize = Math.Max(20, Math.Min(100, size));
                }
                else if (key.Equals("visual.totemPopSize", StringComparison.OrdinalIgnoreCase))
                {
                    int size;
                    if (int.TryParse(value, out size))
                        settings.VisualTotemPopSize = Math.Max(20, Math.Min(100, size));
                }
                else if (key.Equals("visual.cleanPumpkin", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualCleanPumpkin = ParseBool(value, true);
                }
                else if (key.Equals("visual.clearVignette", StringComparison.OrdinalIgnoreCase))
                {
                    settings.VisualClearVignette = ParseBool(value, true);
                }
                else if (key.Equals("account.microsoftClientId", StringComparison.OrdinalIgnoreCase))
                {
                    settings.MicrosoftClientId = value;
                }
                else if (key.Equals("account.name", StringComparison.OrdinalIgnoreCase))
                {
                    settings.AccountName = value;
                }
                else if (key.Equals("account.email", StringComparison.OrdinalIgnoreCase))
                {
                    settings.AccountEmail = value;
                }
                else if (key.Equals("account.signedInAt", StringComparison.OrdinalIgnoreCase))
                {
                    settings.AccountSignedInAt = value;
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
            lines.Add("overlay.scale=" + OverlayScale.ToString());
            lines.Add("edit.mode=" + EditMode.ToString());
            lines.Add("overlay.onlyShowInWorld=" + OnlyShowInWorld.ToString());
            lines.Add("visual.lowFire=" + VisualLowFire.ToString());
            lines.Add("visual.noBobber=" + VisualNoBobber.ToString());
            lines.Add("visual.lowShield=" + VisualLowShield.ToString());
            lines.Add("visual.smallTotem=" + VisualSmallTotem.ToString());
            lines.Add("visual.smallTotemPop=" + VisualSmallTotemPop.ToString());
            lines.Add("visual.shieldSize=" + VisualShieldSize.ToString());
            lines.Add("visual.totemSize=" + VisualTotemSize.ToString());
            lines.Add("visual.totemPopSize=" + VisualTotemPopSize.ToString());
            lines.Add("visual.cleanPumpkin=" + VisualCleanPumpkin.ToString());
            lines.Add("visual.clearVignette=" + VisualClearVignette.ToString());
            lines.Add("account.microsoftClientId=" + MicrosoftClientId);
            lines.Add("account.name=" + AccountName);
            lines.Add("account.email=" + AccountEmail);
            lines.Add("account.signedInAt=" + AccountSignedInAt);

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
        private readonly Dictionary<string, ClientModuleState> moduleStates;
        private List<ServerProfile> serverProfiles;
        private List<ManagedPack> managedPacks;
        private readonly Dictionary<string, Button> toggleButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        private FlowLayoutPanel overlayFlow;
        private TextBox moduleSearchBox;
        private ComboBox moduleCategoryBox;
        private Label statusLabel;
        private Label minecraftLabel;
        private ComboBox launchProfileBox;
        private TextBox customLaunchBox;
        private TextBox microsoftClientIdBox;
        private Label accountNameLabel;
        private Label accountEmailLabel;
        private Label accountSignedInLabel;
        private Button microsoftSignInButton;
        private Button accountPillButton;
        private readonly Dictionary<string, Button> pageButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private Rectangle restoreBounds;
        private Rectangle fullscreenRestoreBounds;
        private bool hasRestoreBounds;
        private bool isWindowedMaximized;
        private bool isFullscreen;
        private bool fullscreenRestoreWasMaximized;
        private TextBox serverHostBox;
        private NumericUpDown serverPortBox;
        private ListBox serverProfileList;
        private TextBox serverProfileNameBox;
        private TextBox serverProfileHostBox;
        private NumericUpDown serverProfilePortBox;
        private ComboBox serverProfileCategoryBox;
        private CheckBox serverProfileFavoriteCheck;
        private TextBox serverProfileNotesBox;
        private TextBox serverProfilePacksBox;
        private Label serverProfileStatusLabel;
        private ListBox managedPackList;
        private TextBox bridgeUrlBox;
        private CheckBox bridgeEnabledCheck;
        private CheckBox bridgeMockCheck;
        private Label bridgeStatusLabel;
        private Label latestInstalledLabel;
        private Label latestAvailableLabel;
        private Label latestStatusLabel;
        private Label latestReleaseDateLabel;
        private TextBox latestChangelogBox;
        private string latestReleaseUrl = "https://github.com/EnderKraken914/bomb-client/releases/latest";
        private TrackBar opacityTrack;
        private Label opacityLabel;
        private TrackBar overlayScaleTrack;
        private Label overlayScaleLabel;
        private CheckBox gameplayOnlyCheck;
        private CheckBox lowFireCheck;
        private CheckBox noBobberCheck;
        private CheckBox lowShieldCheck;
        private CheckBox smallTotemCheck;
        private CheckBox smallTotemPopCheck;
        private TrackBar shieldSizeTrack;
        private TrackBar totemSizeTrack;
        private TrackBar totemPopSizeTrack;
        private Label shieldSizeLabel;
        private Label totemSizeLabel;
        private Label totemPopSizeLabel;
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
            moduleStates = ClientModuleConfigStore.Load();
            SyncModuleStatesFromSettings();
            serverProfiles = ServerProfileStore.Load();
            managedPacks = ManagedPackStore.Load();
            BridgeManager.RefreshStatus();
            OverlayManager.Configure(settings);

            Text = "Bomb Client";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(1180, 680);
            Size = new Size(1180, 700);
            BackColor = bg;
            Font = uiFont;

            Panel shell = new Panel();
            shell.Dock = DockStyle.Fill;
            shell.BackColor = bg;
            Controls.Add(shell);

            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 88;
            topBar.BackColor = Color.FromArgb(5, 8, 12);
            topBar.MouseDown += DragWindow;
            shell.Controls.Add(topBar);

            PictureBox logo = new PictureBox();
            logo.Image = AssetLoader.LoadLogo();
            logo.BackColor = topBar.BackColor;
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Location = new Point(28, 26);
            logo.Size = new Size(44, 44);
            logo.MouseDown += DragWindow;
            topBar.Controls.Add(logo);

            Label title = new Label();
            title.Text = "BOMB CLIENT";
            title.AutoSize = true;
            title.ForeColor = text;
            title.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
            title.Location = new Point(82, 34);
            title.MouseDown += DragWindow;
            topBar.Controls.Add(title);

            Panel navStrip = new Panel();
            navStrip.Size = new Size(456, 48);
            navStrip.BackColor = Color.FromArgb(13, 18, 22);
            navStrip.Location = new Point(420, 20);
            topBar.Controls.Add(navStrip);

            Button navPlay = CreateTopNavButton("Play");
            Button navMods = CreateTopNavButton("Modules");
            Button navVisuals = CreateTopNavButton("Visuals");
            Button navServers = CreateTopNavButton("Servers");
            Button navPacks = CreateTopNavButton("Packs");
            Button navUpdates = CreateTopNavButton("Updates");
            navPlay.Location = new Point(8, 7);
            navMods.Location = new Point(82, 7);
            navVisuals.Location = new Point(156, 7);
            navServers.Location = new Point(230, 7);
            navPacks.Location = new Point(304, 7);
            navUpdates.Location = new Point(378, 7);
            navStrip.Controls.Add(navPlay);
            navStrip.Controls.Add(navMods);
            navStrip.Controls.Add(navVisuals);
            navStrip.Controls.Add(navServers);
            navStrip.Controls.Add(navPacks);
            navStrip.Controls.Add(navUpdates);
            RegisterPageButton("Home", navPlay);
            RegisterPageButton("Overlays", navMods);
            RegisterPageButton("Visual", navVisuals);
            RegisterPageButton("Servers", navServers);
            RegisterPageButton("Packs", navPacks);
            RegisterPageButton("Updates", navUpdates);

            accountPillButton = CreateAccountPillButton();
            UpdateAccountPill();
            accountPillButton.Location = new Point(852, 18);
            accountPillButton.Click += delegate { ShowPage("Account"); };
            topBar.Controls.Add(accountPillButton);
            RegisterPageButton("Account", accountPillButton);

            Button profileIcon = CreateIconButton("ID", false);
            profileIcon.Location = new Point(1016, 26);
            profileIcon.Click += delegate { ShowPage("Profiles"); };
            topBar.Controls.Add(profileIcon);
            RegisterPageButton("Profiles", profileIcon);

            Button settingsIcon = CreateIconButton("⚙", false);
            settingsIcon.Location = new Point(1058, 26);
            settingsIcon.Click += delegate { ShowPage("Settings"); };
            topBar.Controls.Add(settingsIcon);

            Button maximize = CreateIconButton("□", false);
            maximize.Location = new Point(1100, 26);
            maximize.Click += delegate { ToggleWindowedMaximize(); };
            topBar.Controls.Add(maximize);

            Button minimize = CreateIconButton("_", false);
            minimize.Location = new Point(1142, 26);
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            topBar.Controls.Add(minimize);

            Button close = CreateIconButton("X", true);
            close.Location = new Point(1184, 26);
            close.Click += delegate { Close(); };
            topBar.Controls.Add(close);

            Action layoutTopBarControls = delegate
            {
                const int rightMargin = 26;
                const int iconGap = 14;
                const int accountGap = 18;
                close.Left = topBar.Width - rightMargin - close.Width;
                minimize.Left = close.Left - iconGap - minimize.Width;
                maximize.Left = minimize.Left - iconGap - maximize.Width;
                settingsIcon.Left = maximize.Left - iconGap - settingsIcon.Width;
                profileIcon.Left = settingsIcon.Left - iconGap - profileIcon.Width;
                accountPillButton.Left = profileIcon.Left - accountGap - accountPillButton.Width;

                int centeredNav = (topBar.Width - navStrip.Width) / 2;
                int maxNavLeft = accountPillButton.Left - navStrip.Width - accountGap;
                navStrip.Left = Math.Max(260, Math.Min(centeredNav, maxNavLeft));
            };
            topBar.Resize += delegate { layoutTopBarControls(); };
            layoutTopBarControls();

            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = bg;
            content.Padding = new Padding(26, 4, 26, 18);
            shell.Controls.Add(content);
            topBar.BringToFront();

            Panel homePage = BuildHomePage();
            Panel overlaysPage = BuildOverlaysPage();
            Panel profilesPage = BuildProfilesPage();
            Panel accountPage = BuildAccountPage();
            Panel visualPage = BuildVisualPage();
            Panel serversPage = BuildServersPage();
            Panel packsPage = BuildPacksPage();
            Panel updatesPage = BuildUpdatesPage();
            Panel settingsPage = BuildSettingsPage();
            pages["Home"] = homePage;
            pages["Overlays"] = overlaysPage;
            pages["Profiles"] = profilesPage;
            pages["Account"] = accountPage;
            pages["Visual"] = visualPage;
            pages["Servers"] = serversPage;
            pages["Packs"] = packsPage;
            pages["Updates"] = updatesPage;
            pages["Settings"] = settingsPage;
            content.Controls.Add(homePage);
            content.Controls.Add(overlaysPage);
            content.Controls.Add(profilesPage);
            content.Controls.Add(accountPage);
            content.Controls.Add(visualPage);
            content.Controls.Add(serversPage);
            content.Controls.Add(packsPage);
            content.Controls.Add(updatesPage);
            content.Controls.Add(settingsPage);

            navPlay.Click += delegate { ShowPage("Home"); };
            navMods.Click += delegate { ShowPage("Overlays"); };
            navVisuals.Click += delegate { ShowPage("Visual"); };
            navServers.Click += delegate { ShowPage("Servers"); };
            navPacks.Click += delegate { ShowPage("Packs"); };
            navUpdates.Click += delegate { ShowPage("Updates"); };
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
                ClientModuleConfigStore.Save(moduleStates);
                ServerProfileStore.Save(serverProfiles);
                ManagedPackStore.Save(managedPacks);
            };
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            if (isFullscreen)
                return;
            if (isWindowedMaximized)
                RestoreFromWindowedMaximize();
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleFullscreen();
                return true;
            }
            if (keyData == Keys.Escape && isFullscreen)
            {
                ToggleFullscreen();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ToggleWindowedMaximize()
        {
            if (isFullscreen)
                ToggleFullscreen();

            if (isWindowedMaximized)
            {
                RestoreFromWindowedMaximize();
                return;
            }

            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;

            restoreBounds = Bounds;
            hasRestoreBounds = true;
            Rectangle working = Screen.FromControl(this).WorkingArea;
            Bounds = working;
            isWindowedMaximized = true;
            TopMost = false;
        }

        private void RestoreFromWindowedMaximize()
        {
            if (hasRestoreBounds && restoreBounds.Width > 0 && restoreBounds.Height > 0)
                Bounds = restoreBounds;
            isWindowedMaximized = false;
            TopMost = false;
        }

        private void ToggleFullscreen()
        {
            if (isFullscreen)
            {
                Bounds = fullscreenRestoreBounds;
                isFullscreen = false;
                isWindowedMaximized = fullscreenRestoreWasMaximized;
                TopMost = false;
                return;
            }

            fullscreenRestoreBounds = Bounds;
            fullscreenRestoreWasMaximized = isWindowedMaximized;
            isFullscreen = true;
            isWindowedMaximized = false;
            TopMost = true;
            Bounds = Screen.FromControl(this).Bounds;
        }

        private void RegisterPageButton(string page, Button button)
        {
            pageButtons[page] = button;
        }

        private Button CreateTopNavButton(string label)
        {
            Button button = new Button();
            button.Text = label;
            button.Size = new Size(70, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(13, 18, 22);
            button.ForeColor = muted;
            button.Font = new Font("Segoe UI Semibold", 8.4f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Button CreateAccountPillButton()
        {
            Button button = new Button();
            button.Size = new Size(160, 52);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(9, 36, 20);
            button.ForeColor = text;
            button.Font = new Font("Segoe UI Semibold", 8.7f, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleRight;
            button.Padding = new Padding(12, 0, 10, 0);
            button.Cursor = Cursors.Hand;
            UpdateAccountPill();
            return button;
        }

        private Button CreateIconButton(string label, bool danger)
        {
            Button button = new Button();
            button.Text = label;
            button.Size = new Size(38, 36);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = danger ? Color.FromArgb(42, 12, 17) : Color.FromArgb(14, 19, 27);
            button.ForeColor = danger ? Color.FromArgb(242, 70, 76) : text;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private void UpdateNavigationState(string active)
        {
            foreach (KeyValuePair<string, Button> entry in pageButtons)
            {
                bool selected = string.Equals(entry.Key, active, StringComparison.OrdinalIgnoreCase);
                Button button = entry.Value;
                if (button == accountPillButton)
                {
                    button.BackColor = selected ? Color.FromArgb(10, 58, 29) : Color.FromArgb(9, 36, 20);
                    continue;
                }
                button.BackColor = selected ? Color.FromArgb(30, 35, 44) : Color.FromArgb(13, 18, 22);
                button.ForeColor = selected ? text : muted;
            }
        }

        private void UpdateAccountPill()
        {
            if (accountPillButton == null)
                return;
            string name = settings.AccountName.Length == 0 ? "Profile" : settings.AccountName;
            accountPillButton.Text = "Signed in as\r\n" + ShortenText(name, 18);
        }

        private static string ShortenText(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= max)
                return value;
            return value.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        private Panel BuildHomePage()
        {
            Panel page = CreatePage();
            page.AutoScroll = false;

            Panel hero = new Panel();
            hero.BackColor = Color.FromArgb(13, 18, 23);
            hero.Paint += DrawHeroPanel;
            page.Controls.Add(hero);

            Label version = new Label();
            version.Text = "Minecraft Bedrock";
            version.ForeColor = text;
            version.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
            version.TextAlign = ContentAlignment.MiddleCenter;
            version.BackColor = Color.Transparent;
            hero.Controls.Add(version);

            Button changeVersion = CreateDarkHeroButton("Change Version");
            changeVersion.Click += delegate { ShowPage("Profiles"); };
            hero.Controls.Add(changeVersion);

            Button quickOverlays = CreateHeroSideButton("HUD");
            quickOverlays.Click += delegate { ShowPage("Overlays"); };
            hero.Controls.Add(quickOverlays);

            Button quickAccount = CreateHeroSideButton("ID");
            quickAccount.Click += delegate { ShowPage("Account"); };
            hero.Controls.Add(quickAccount);

            Button quickConsole = CreateHeroSideButton(">");
            quickConsole.BackColor = red;
            quickConsole.Click += delegate { LaunchMinecraft(); };
            hero.Controls.Add(quickConsole);

            Button launch = CreatePrimaryButton("LAUNCH MINECRAFT");
            launch.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
            launch.Click += delegate { LaunchMinecraft(); };
            page.Controls.Add(launch);

            Panel quickCard = CreateCard(0, 0, 430, 210);
            page.Controls.Add(quickCard);
            Label quickTitle = CreateCardTitle("QUICK ACTIONS");
            quickTitle.Location = new Point(20, 18);
            quickCard.Controls.Add(quickTitle);

            Button startOverlays = CreatePrimaryButton("Start Overlays");
            startOverlays.Location = new Point(22, 58);
            startOverlays.Size = new Size(158, 40);
            startOverlays.Click += delegate
            {
                PersistSettingsFromUi();
                OverlayManager.ApplyConfiguredOverlays();
                SetStatus("Enabled overlays started.");
            };
            quickCard.Controls.Add(startOverlays);

            Button stopOverlays = CreateSecondaryButton("Stop Overlays");
            stopOverlays.Location = new Point(196, 58);
            stopOverlays.Size = new Size(136, 40);
            stopOverlays.Click += delegate
            {
                OverlayManager.CloseAll();
                SetStatus("Overlays stopped.");
            };
            quickCard.Controls.Add(stopOverlays);

            Button applyVisuals = CreateSecondaryButton("Apply Visuals");
            applyVisuals.Location = new Point(22, 114);
            applyVisuals.Size = new Size(136, 40);
            applyVisuals.Click += delegate { ApplyClientVisuals(true); };
            quickCard.Controls.Add(applyVisuals);

            Button visualSettings = CreateSecondaryButton("Visuals");
            visualSettings.Location = new Point(174, 114);
            visualSettings.Size = new Size(136, 40);
            visualSettings.Click += delegate { ShowPage("Visual"); };
            quickCard.Controls.Add(visualSettings);

            minecraftLabel = CreateMutedLabel("Minecraft status");
            minecraftLabel.Location = new Point(22, 168);
            minecraftLabel.Size = new Size(360, 24);
            quickCard.Controls.Add(minecraftLabel);

            Panel serversCard = CreateCard(0, 0, 430, 210);
            page.Controls.Add(serversCard);
            Label serversTitle = CreateCardTitle("PARTNER SERVERS");
            serversTitle.Location = new Point(20, 18);
            serversCard.Controls.Add(serversTitle);

            AddServerRow(serversCard, "CubeCraft", "play.cubecraft.net", "630", 56);
            AddServerRow(serversCard, "The Hive", "geo.hivebedrock.network", "273", 92);
            AddServerRow(serversCard, "NetherGames", "play.nethergames.org", "200", 128);
            AddServerRow(serversCard, "Lifeboat", "play.lbsg.net", "161", 164);

            Panel rightRail = new Panel();
            rightRail.BackColor = Color.FromArgb(14, 18, 23);
            rightRail.Paint += DrawRightRailPanel;
            page.Controls.Add(rightRail);

            PictureBox railLogo = new PictureBox();
            railLogo.Image = AssetLoader.LoadLogo();
            railLogo.SizeMode = PictureBoxSizeMode.Zoom;
            railLogo.BackColor = Color.Transparent;
            rightRail.Controls.Add(railLogo);

            Label railTitle = new Label();
            railTitle.Text = "BOMB CLIENT";
            railTitle.ForeColor = Color.FromArgb(156, 164, 174);
            railTitle.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
            railTitle.AutoSize = true;
            rightRail.Controls.Add(railTitle);

            statusLabel = CreateMutedLabel("Ready");
            statusLabel.Size = new Size(250, 52);
            rightRail.Controls.Add(statusLabel);

            page.Resize += delegate
            {
                LayoutFeatherHome(page, hero, version, changeVersion, quickOverlays, quickAccount, quickConsole, launch, quickCard, serversCard, rightRail, railLogo, railTitle);
            };
            LayoutFeatherHome(page, hero, version, changeVersion, quickOverlays, quickAccount, quickConsole, launch, quickCard, serversCard, rightRail, railLogo, railTitle);

            return page;
        }

        private Button CreateDarkHeroButton(string label)
        {
            Button button = CreateSecondaryButton(label);
            button.BackColor = Color.Black;
            button.ForeColor = text;
            button.FlatAppearance.BorderColor = Color.Black;
            return button;
        }

        private Button CreateHeroSideButton(string label)
        {
            Button button = CreateIconButton(label, false);
            button.Size = new Size(46, 42);
            button.BackColor = Color.FromArgb(32, 36, 43);
            return button;
        }

        private void LayoutFeatherHome(Panel page, Panel hero, Label version, Button changeVersion, Button quickOverlays, Button quickAccount, Button quickConsole, Button launch, Panel quickCard, Panel serversCard, Panel rightRail, PictureBox railLogo, Label railTitle)
        {
            int viewport = Math.Max(900, page.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            int railWidth = viewport >= 1050 ? 270 : 0;
            int gap = railWidth > 0 ? 22 : 0;
            int mainWidth = viewport - railWidth - gap;

            hero.Location = new Point(0, 0);
            hero.Size = new Size(Math.Max(620, mainWidth), 290);
            launch.Location = new Point(0, hero.Bottom + 14);
            launch.Size = new Size(hero.Width, 52);

            int cardTop = launch.Bottom + 16;
            int cardGap = 16;
            int cardWidth = (hero.Width - cardGap) / 2;
            quickCard.Location = new Point(0, cardTop);
            quickCard.Size = new Size(cardWidth, 214);
            serversCard.Location = new Point(cardWidth + cardGap, cardTop);
            serversCard.Size = new Size(hero.Width - cardWidth - cardGap, 214);

            rightRail.Visible = railWidth > 0;
            if (railWidth > 0)
            {
                rightRail.Location = new Point(mainWidth + gap, 0);
                rightRail.Size = new Size(railWidth, Math.Max(520, page.ClientSize.Height - 8));
                railLogo.Size = new Size(126, 126);
                railLogo.Location = new Point((rightRail.Width - railLogo.Width) / 2, Math.Max(150, rightRail.Height / 2 - 108));
                railTitle.Location = new Point((rightRail.Width - railTitle.Width) / 2, railLogo.Bottom + 14);
                statusLabel.Location = new Point(20, rightRail.Height - 72);
            }

            version.Size = new Size(260, 34);
            version.Location = new Point((hero.Width - version.Width) / 2, 112);
            changeVersion.Size = new Size(190, 42);
            changeVersion.Location = new Point((hero.Width - changeVersion.Width) / 2, version.Bottom + 22);

            quickOverlays.Location = new Point(hero.Width - 62, hero.Height - 160);
            quickAccount.Location = new Point(hero.Width - 62, hero.Height - 108);
            quickConsole.Location = new Point(hero.Width - 62, hero.Height - 56);
            hero.Invalidate();
            rightRail.Invalidate();
        }

        private void AddServerRow(Panel parent, string name, string host, string count, int y)
        {
            Panel row = new Panel();
            row.Location = new Point(20, y);
            row.Size = new Size(parent.Width - 40, 30);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.BackColor = Color.FromArgb(13, 18, 22);
            row.Paint += delegate(object sender, PaintEventArgs e)
            {
                DrawRoundedPanel(e.Graphics, row.ClientRectangle, Color.FromArgb(13, 18, 22), 6);
            };
            parent.Controls.Add(row);

            Label title = new Label();
            title.Text = name;
            title.ForeColor = text;
            title.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            title.Location = new Point(12, 6);
            title.Size = new Size(150, 18);
            row.Controls.Add(title);

            Label ping = new Label();
            ping.Text = count;
            ping.ForeColor = Color.FromArgb(130, 230, 115);
            ping.TextAlign = ContentAlignment.MiddleRight;
            ping.Location = new Point(row.Width - 58, 6);
            ping.Size = new Size(46, 18);
            ping.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(ping);
        }

        private void DrawHeroPanel(object sender, PaintEventArgs e)
        {
            Panel hero = sender as Panel;
            if (hero == null)
                return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, hero.Width - 1, hero.Height - 1);
            using (GraphicsPath path = RoundedRect(bounds, 8))
            using (LinearGradientBrush sky = new LinearGradientBrush(bounds, Color.FromArgb(28, 38, 47), Color.FromArgb(8, 12, 18), LinearGradientMode.Vertical))
            {
                g.FillPath(sky, path);
                g.SetClip(path);
                DrawPixelScene(g, bounds);
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(178, 0, 0, 0)))
                    g.FillRectangle(shade, bounds);
                g.ResetClip();
                using (Pen border = new Pen(Color.FromArgb(31, 38, 47)))
                    g.DrawPath(border, path);
            }
        }

        private void DrawPixelScene(Graphics g, Rectangle bounds)
        {
            using (SolidBrush water = new SolidBrush(Color.FromArgb(38, 70, 92)))
                g.FillRectangle(water, 0, bounds.Height - 110, bounds.Width, 96);
            using (SolidBrush ground = new SolidBrush(Color.FromArgb(88, 78, 58)))
                g.FillRectangle(ground, 0, bounds.Height - 36, bounds.Width, 36);
            using (SolidBrush hill = new SolidBrush(Color.FromArgb(44, 61, 42)))
            {
                g.FillRectangle(hill, 0, 42, bounds.Width / 3, 150);
                g.FillRectangle(hill, bounds.Width / 2, 28, bounds.Width / 2, 162);
            }
            using (SolidBrush player = new SolidBrush(Color.FromArgb(128, 146, 160)))
                g.FillRectangle(player, bounds.Width / 2 - 42, bounds.Height - 116, 52, 88);
            using (SolidBrush mob = new SolidBrush(Color.FromArgb(64, 104, 70)))
            {
                g.FillRectangle(mob, bounds.Width - 190, bounds.Height - 104, 44, 72);
                g.FillRectangle(mob, 126, bounds.Height - 98, 42, 66);
            }
        }

        private void DrawRightRailPanel(object sender, PaintEventArgs e)
        {
            Panel rail = sender as Panel;
            if (rail == null)
                return;
            DrawRoundedPanel(e.Graphics, rail.ClientRectangle, Color.FromArgb(14, 18, 23), 8);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen arc = new Pen(Color.FromArgb(18, 255, 255, 255), 70))
                e.Graphics.DrawArc(arc, new Rectangle(-120, 10, rail.Width + 190, rail.Height - 80), 220, 190);
            using (SolidBrush plus = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
            {
                e.Graphics.FillRectangle(plus, rail.Width - 46, 170, 28, 6);
                e.Graphics.FillRectangle(plus, rail.Width - 35, 159, 6, 28);
                e.Graphics.FillRectangle(plus, 42, rail.Height - 190, 28, 6);
                e.Graphics.FillRectangle(plus, 53, rail.Height - 201, 6, 28);
            }
        }

        private Panel BuildOverlaysPage()
        {
            Panel page = CreatePage();
            Label heading = CreateHeading("Modules");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            moduleSearchBox = new TextBox();
            moduleSearchBox.Location = new Point(0, 50);
            moduleSearchBox.Size = new Size(260, 24);
            moduleSearchBox.BackColor = Color.FromArgb(30, 37, 51);
            moduleSearchBox.ForeColor = text;
            moduleSearchBox.BorderStyle = BorderStyle.FixedSingle;
            moduleSearchBox.TextChanged += delegate { RefreshModuleCards(); };
            page.Controls.Add(moduleSearchBox);

            moduleCategoryBox = new ComboBox();
            moduleCategoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            moduleCategoryBox.BackColor = Color.FromArgb(30, 37, 51);
            moduleCategoryBox.ForeColor = text;
            moduleCategoryBox.FlatStyle = FlatStyle.Flat;
            moduleCategoryBox.Location = new Point(280, 50);
            moduleCategoryBox.Size = new Size(150, 24);
            foreach (string category in ClientModuleCatalog.Categories)
                moduleCategoryBox.Items.Add(category);
            if (moduleCategoryBox.Items.Count > 0)
                moduleCategoryBox.SelectedIndex = 0;
            moduleCategoryBox.SelectedIndexChanged += delegate { RefreshModuleCards(); };
            page.Controls.Add(moduleCategoryBox);

            Button resetLayout = CreateSecondaryButton("Reset Layout");
            resetLayout.Location = new Point(450, 44);
            resetLayout.Size = new Size(126, 36);
            resetLayout.Click += delegate { ResetOverlayLayout(); };
            page.Controls.Add(resetLayout);

            Button export = CreateSecondaryButton("Export");
            export.Location = new Point(592, 44);
            export.Size = new Size(90, 36);
            export.Click += delegate { ExportSettings(); };
            page.Controls.Add(export);

            Button import = CreateSecondaryButton("Import");
            import.Location = new Point(696, 44);
            import.Size = new Size(90, 36);
            import.Click += delegate { ImportSettings(); };
            page.Controls.Add(import);

            Panel safeCard = CreateCard(0, 92, 786, 70);
            page.Controls.Add(safeCard);
            Label safeTitle = CreateCardTitle("Safe External Client");
            safeTitle.Location = new Point(18, 12);
            safeCard.Controls.Add(safeTitle);
            Label safeText = CreateMutedLabel("Bomb Client modules are external: no DLL injection, memory editing, packet manipulation, traffic modification, or installed Bedrock file edits.");
            safeText.Location = new Point(20, 40);
            safeText.Size = new Size(730, 24);
            safeCard.Controls.Add(safeText);

            overlayFlow = new FlowLayoutPanel();
            overlayFlow.Location = new Point(0, 180);
            overlayFlow.Size = new Size(page.Width, page.Height - 180);
            overlayFlow.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            overlayFlow.AutoScroll = true;
            overlayFlow.WrapContents = true;
            overlayFlow.FlowDirection = FlowDirection.LeftToRight;
            overlayFlow.BackColor = bg;
            overlayFlow.Resize += delegate { ResizeOverlayCards(); };
            page.Controls.Add(overlayFlow);

            foreach (ClientModuleDefinition module in ClientModuleCatalog.All)
                overlayFlow.Controls.Add(CreateModuleCard(module));

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
            Label clientNote = CreateMutedLabel("Bomb Client launches profiles, keeps overlay/server presets, manages optional packs, and starts external HUD modules around Bedrock. External clients cannot safely replace Bedrock's built-in version system the same way Feather manages Java installations, but profiles make the workflow similar.");
            clientNote.Location = new Point(22, 58);
            clientNote.Size = new Size(620, 78);
            clientCard.Controls.Add(clientNote);

            return page;
        }

        private Panel CreateModuleCard(ClientModuleDefinition module)
        {
            Panel card = new Panel();
            card.Width = 420;
            card.Height = 150;
            card.Margin = new Padding(0, 0, 16, 16);
            card.BackColor = panel;
            card.Tag = module;
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                DrawRoundedPanel(e.Graphics, card.ClientRectangle, panel, 8);
            };

            Label title = CreateCardTitle(module.DisplayName);
            title.Location = new Point(18, 14);
            title.Size = new Size(250, 26);
            card.Controls.Add(title);

            Label category = CreateMutedLabel(module.Category);
            category.Location = new Point(20, 42);
            category.Size = new Size(160, 22);
            card.Controls.Add(category);

            Label desc = CreateMutedLabel(module.Description);
            desc.Location = new Point(20, 66);
            desc.Size = new Size(270, 46);
            card.Controls.Add(desc);

            Label settingsSummary = CreateMutedLabel(ModuleSettingsSummary(module));
            settingsSummary.Location = new Point(20, 114);
            settingsSummary.Size = new Size(270, 22);
            card.Controls.Add(settingsSummary);

            Button toggle = CreateToggleButton(IsModuleEnabled(module));
            toggle.Location = new Point(308, 38);
            toggle.Click += delegate
            {
                bool next = !IsModuleEnabled(module);
                SetModuleEnabled(module, next);
                UpdateToggleButton(toggle, next);
            };
            toggleButtons[module.Id] = toggle;
            card.Controls.Add(toggle);

            Button settingsButton = CreateSecondaryButton("Settings");
            settingsButton.Location = new Point(308, 88);
            settingsButton.Size = new Size(90, 34);
            settingsButton.Click += delegate { OpenModuleSettings(module); };
            card.Controls.Add(settingsButton);

            card.Resize += delegate
            {
                toggle.Location = new Point(Math.Max(224, card.Width - 104), 38);
                settingsButton.Location = new Point(Math.Max(224, card.Width - 104), 88);
                title.Size = new Size(Math.Max(180, card.Width - 144), 26);
                desc.Size = new Size(Math.Max(180, card.Width - 150), 46);
                settingsSummary.Size = new Size(Math.Max(180, card.Width - 150), 22);
                card.Invalidate();
            };

            return card;
        }

        private string ModuleSettingsSummary(ClientModuleDefinition module)
        {
            if (module.Settings.Length == 0)
                return "Settings: saved automatically";

            List<string> names = new List<string>();
            foreach (ModuleSettingDefinition setting in module.Settings)
                names.Add(setting.DisplayName);
            return "Settings: " + string.Join(", ", names.ToArray());
        }

        private bool IsModuleEnabled(ClientModuleDefinition module)
        {
            string overlayId = ClientModuleCatalog.OverlayIdFromModule(module.Id);
            if (overlayId.Length > 0)
                return IsOverlayEnabled(overlayId);
            return ClientModuleConfigStore.IsEnabled(moduleStates, module);
        }

        private void SetModuleEnabled(ClientModuleDefinition module, bool enabled)
        {
            ClientModuleConfigStore.SetEnabled(moduleStates, module, enabled);
            string overlayId = ClientModuleCatalog.OverlayIdFromModule(module.Id);
            if (overlayId.Length > 0)
            {
                settings.Overlays[overlayId] = enabled;
                settings.Save();
                OverlayManager.SetOverlay(overlayId, enabled);
            }
            ClientModuleConfigStore.Save(moduleStates);
            SetStatus(module.DisplayName + (enabled ? " enabled." : " disabled."));
        }

        private void RefreshModuleCards()
        {
            if (overlayFlow == null)
                return;

            string search = moduleSearchBox == null ? "" : moduleSearchBox.Text.Trim();
            string category = moduleCategoryBox == null || moduleCategoryBox.SelectedItem == null ? "All" : moduleCategoryBox.SelectedItem.ToString();
            foreach (Control control in overlayFlow.Controls)
            {
                ClientModuleDefinition module = control.Tag as ClientModuleDefinition;
                if (module == null)
                    continue;

                bool matchesCategory = category == "All" || string.Equals(module.Category, category, StringComparison.OrdinalIgnoreCase);
                bool matchesSearch = search.Length == 0 ||
                    module.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    module.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    module.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                control.Visible = matchesCategory && matchesSearch;
            }
        }

        private void OpenModuleSettings(ClientModuleDefinition module)
        {
            string overlayId = ClientModuleCatalog.OverlayIdFromModule(module.Id);
            if (overlayId == "clientvisuals")
            {
                ShowPage("Visual");
                return;
            }
            if (module.Category == "Servers")
            {
                ShowPage("Servers");
                return;
            }
            if (module.Category == "Packs")
            {
                ShowPage("Packs");
                return;
            }
            if (module.Category == "Updates")
            {
                ShowPage("Updates");
                return;
            }
            if (module.Category == "Profiles")
            {
                ShowPage("Profiles");
                return;
            }
            if (module.Category == "Account")
            {
                ShowPage("Account");
                return;
            }

            MessageBox.Show(
                module.DisplayName + "\n\n" +
                module.Description + "\n\n" +
                ModuleSettingsSummary(module),
                "Bomb Client Module",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ResetOverlayLayout()
        {
            settings.Positions.Clear();
            settings.Save();
            OverlayManager.CloseAll();
            OverlayManager.Configure(settings);
            OverlayManager.ApplyConfiguredOverlays();
            SetStatus("Overlay layout reset.");
        }

        private void ExportSettings()
        {
            PersistSettingsFromUi();
            settings.Save();
            ClientModuleConfigStore.Save(moduleStates);

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Bomb Client settings (*.ini)|*.ini|All files (*.*)|*.*";
            dialog.FileName = "BombClient-settings.ini";
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            File.Copy(AppPaths.SettingsFile, dialog.FileName, true);
            SetStatus("Settings exported.");
        }

        private void ImportSettings()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Bomb Client settings (*.ini)|*.ini|All files (*.*)|*.*";
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.Copy(dialog.FileName, AppPaths.SettingsFile, true);
            SetStatus("Settings imported. Restart Bomb Client to reload all fields.");
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

        private async Task BeginMicrosoftAccountSignIn()
        {
            if (microsoftClientIdBox != null)
                settings.MicrosoftClientId = microsoftClientIdBox.Text.Trim();

            if (settings.MicrosoftClientId.Length == 0)
            {
                MessageBox.Show(
                    "Microsoft requires an OAuth app client ID before Bomb Client can read your profile.\n\n" +
                    "Create a Microsoft identity platform app registration for personal Microsoft accounts, enable public client/device-code flow, then paste the Application (client) ID into this tab.",
                    "Bomb Client",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (microsoftSignInButton != null)
                microsoftSignInButton.Enabled = false;

            try
            {
                settings.Save();
                SetStatus("Starting Microsoft sign-in...");

                MicrosoftAccountProfile profile = await Task.Run(delegate
                {
                    return MicrosoftAccountAuthenticator.SignIn(settings.MicrosoftClientId, delegate(string message)
                    {
                        if (!IsDisposed && IsHandleCreated)
                            BeginInvoke(new Action(delegate { SetStatus(message); }));
                    });
                });

                settings.AccountName = profile.DisplayName;
                settings.AccountEmail = profile.Email;
                settings.AccountSignedInAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                settings.Save();
                RefreshAccountLabels();
                SetStatus("Microsoft account connected.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Microsoft sign-in failed.\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("Microsoft sign-in failed.");
            }
            finally
            {
                if (microsoftSignInButton != null)
                    microsoftSignInButton.Enabled = true;
            }
        }

        private void RefreshAccountLabels()
        {
            if (accountNameLabel == null || accountEmailLabel == null || accountSignedInLabel == null)
                return;

            bool connected = settings.AccountName.Length > 0 || settings.AccountEmail.Length > 0;
            accountNameLabel.Text = connected ? "Minecraft / Microsoft name: " + EmptyFallback(settings.AccountName, "Unknown") : "Minecraft / Microsoft name: Not connected";
            accountEmailLabel.Text = connected ? "Email: " + EmptyFallback(settings.AccountEmail, "Unavailable") : "Email: Not connected";
            accountSignedInLabel.Text = connected
                ? "Connected: " + EmptyFallback(settings.AccountSignedInAt, "Unknown") + ". Bomb Client stores only this display info, not Microsoft password or tokens."
                : "Click Sign in with Microsoft to open the official Microsoft authentication page and connect this tab.";
            UpdateAccountPill();
        }

        private void SyncModuleStatesFromSettings()
        {
            foreach (OverlayDefinition overlay in OverlayCatalog.All)
            {
                ClientModuleDefinition module = ClientModuleCatalog.Find("overlay." + overlay.Id);
                if (module == null)
                    continue;

                bool enabled = overlay.DefaultEnabled;
                settings.Overlays.TryGetValue(overlay.Id, out enabled);
                ClientModuleConfigStore.SetEnabled(moduleStates, module, enabled);
            }
            ClientModuleConfigStore.Save(moduleStates);
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private Panel BuildAccountPage()
        {
            Panel page = CreatePage();
            page.AutoScroll = true;
            Label heading = CreateHeading("Account");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel signInCard = CreateCard(0, 54, 740, 306);
            page.Controls.Add(signInCard);
            Label title = CreateCardTitle("Microsoft Account");
            title.Location = new Point(20, 18);
            signInCard.Controls.Add(title);

            Label note = CreateMutedLabel("Use official Microsoft authentication to connect a profile. Bomb Client opens Microsoft in your browser and saves only the display name and email shown below.");
            note.Location = new Point(22, 56);
            note.Size = new Size(660, 44);
            signInCard.Controls.Add(note);

            Label clientIdLabel = CreateMutedLabel("Microsoft OAuth Client ID");
            clientIdLabel.Location = new Point(22, 112);
            clientIdLabel.Size = new Size(210, 24);
            signInCard.Controls.Add(clientIdLabel);

            microsoftClientIdBox = new TextBox();
            microsoftClientIdBox.BackColor = Color.FromArgb(30, 37, 51);
            microsoftClientIdBox.ForeColor = text;
            microsoftClientIdBox.BorderStyle = BorderStyle.FixedSingle;
            microsoftClientIdBox.Location = new Point(230, 112);
            microsoftClientIdBox.Size = new Size(310, 24);
            microsoftClientIdBox.Text = settings.MicrosoftClientId;
            signInCard.Controls.Add(microsoftClientIdBox);

            Button saveClientId = CreateSecondaryButton("Save ID");
            saveClientId.Location = new Point(558, 106);
            saveClientId.Size = new Size(128, 36);
            saveClientId.Click += delegate
            {
                settings.MicrosoftClientId = microsoftClientIdBox.Text.Trim();
                settings.Save();
                SetStatus("Microsoft client ID saved.");
            };
            signInCard.Controls.Add(saveClientId);

            microsoftSignInButton = CreatePrimaryButton("Sign in with Microsoft");
            microsoftSignInButton.Location = new Point(22, 162);
            microsoftSignInButton.Size = new Size(230, 42);
            microsoftSignInButton.Click += async delegate { await BeginMicrosoftAccountSignIn(); };
            signInCard.Controls.Add(microsoftSignInButton);

            Button signOut = CreateSecondaryButton("Forget Account");
            signOut.Location = new Point(270, 162);
            signOut.Size = new Size(150, 42);
            signOut.Click += delegate
            {
                settings.AccountName = "";
                settings.AccountEmail = "";
                settings.AccountSignedInAt = "";
                settings.Save();
                RefreshAccountLabels();
                SetStatus("Saved Microsoft account details cleared.");
            };
            signInCard.Controls.Add(signOut);

            Button xboxSignIn = CreateSecondaryButton("Open Xbox App");
            xboxSignIn.Location = new Point(438, 162);
            xboxSignIn.Size = new Size(150, 42);
            xboxSignIn.Click += delegate { OpenAccountTarget("xbox:", "Xbox app opened."); };
            signInCard.Controls.Add(xboxSignIn);

            Button windowsAccounts = CreateSecondaryButton("Windows Accounts");
            windowsAccounts.Location = new Point(606, 162);
            windowsAccounts.Size = new Size(118, 42);
            windowsAccounts.Click += delegate { OpenAccountTarget("ms-settings:emailandaccounts", "Windows account settings opened."); };
            signInCard.Controls.Add(windowsAccounts);

            Button minecraftWeb = CreateSecondaryButton("Minecraft Account Page");
            minecraftWeb.Location = new Point(22, 224);
            minecraftWeb.Size = new Size(190, 42);
            minecraftWeb.Click += delegate { OpenAccountTarget("https://www.minecraft.net/msaprofile", "Minecraft account page opened."); };
            signInCard.Controls.Add(minecraftWeb);

            Button microsoftWeb = CreateSecondaryButton("Microsoft Account");
            microsoftWeb.Location = new Point(230, 224);
            microsoftWeb.Size = new Size(150, 42);
            microsoftWeb.Click += delegate { OpenAccountTarget("https://account.microsoft.com/", "Microsoft account page opened."); };
            signInCard.Controls.Add(microsoftWeb);

            Button store = CreateSecondaryButton("Microsoft Store");
            store.Location = new Point(398, 224);
            store.Size = new Size(170, 42);
            store.Click += delegate { OpenAccountTarget("ms-windows-store://home", "Microsoft Store opened."); };
            signInCard.Controls.Add(store);

            Button appSetup = CreateSecondaryButton("OAuth Setup");
            appSetup.Location = new Point(586, 224);
            appSetup.Size = new Size(138, 42);
            appSetup.Click += delegate { OpenAccountTarget("https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app", "Microsoft app registration guide opened."); };
            signInCard.Controls.Add(appSetup);

            Label hint = CreateMutedLabel("The sign-in button uses Microsoft device-code auth. If the Client ID is empty, create a public client app registration and paste its Application ID here.");
            hint.Location = new Point(24, 274);
            hint.Size = new Size(674, 24);
            signInCard.Controls.Add(hint);

            Panel statusCard = CreateCard(0, 390, 740, 188);
            page.Controls.Add(statusCard);
            Label statusTitle = CreateCardTitle("Account Status");
            statusTitle.Location = new Point(20, 18);
            statusCard.Controls.Add(statusTitle);

            accountNameLabel = CreateMutedLabel("");
            accountNameLabel.Location = new Point(22, 58);
            accountNameLabel.Size = new Size(660, 28);
            statusCard.Controls.Add(accountNameLabel);

            accountEmailLabel = CreateMutedLabel("");
            accountEmailLabel.Location = new Point(22, 92);
            accountEmailLabel.Size = new Size(660, 28);
            statusCard.Controls.Add(accountEmailLabel);

            accountSignedInLabel = CreateMutedLabel("");
            accountSignedInLabel.Location = new Point(22, 126);
            accountSignedInLabel.Size = new Size(660, 44);
            statusCard.Controls.Add(accountSignedInLabel);
            RefreshAccountLabels();

            return page;
        }

        private Panel BuildServersPage()
        {
            Panel page = CreatePage();
            page.AutoScroll = true;
            Label heading = CreateHeading("Servers");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel listCard = CreateCard(0, 54, 350, 420);
            page.Controls.Add(listCard);
            Label listTitle = CreateCardTitle("Server Profiles");
            listTitle.Location = new Point(20, 18);
            listCard.Controls.Add(listTitle);

            serverProfileList = new ListBox();
            serverProfileList.Location = new Point(20, 56);
            serverProfileList.Size = new Size(310, 250);
            serverProfileList.BackColor = Color.FromArgb(14, 18, 24);
            serverProfileList.ForeColor = text;
            serverProfileList.BorderStyle = BorderStyle.FixedSingle;
            serverProfileList.SelectedIndexChanged += delegate { LoadSelectedServerProfile(); };
            listCard.Controls.Add(serverProfileList);

            Button newServer = CreateSecondaryButton("New");
            newServer.Location = new Point(20, 322);
            newServer.Size = new Size(88, 36);
            newServer.Click += delegate { NewServerProfile(); };
            listCard.Controls.Add(newServer);

            Button deleteServer = CreateSecondaryButton("Delete");
            deleteServer.Location = new Point(122, 322);
            deleteServer.Size = new Size(88, 36);
            deleteServer.Click += delegate { DeleteSelectedServerProfile(); };
            listCard.Controls.Add(deleteServer);

            Button useServer = CreatePrimaryButton("Use");
            useServer.Location = new Point(224, 322);
            useServer.Size = new Size(88, 36);
            useServer.Click += delegate { UseSelectedServerProfile(); };
            listCard.Controls.Add(useServer);

            Label listNote = CreateMutedLabel("Public servers use only saved info, safe ping/status checks, local stats, overlays, notes, and user presets.");
            listNote.Location = new Point(22, 372);
            listNote.Size = new Size(300, 36);
            listCard.Controls.Add(listNote);

            Panel editCard = CreateCard(374, 54, 530, 420);
            page.Controls.Add(editCard);
            Label editTitle = CreateCardTitle("Profile Details");
            editTitle.Location = new Point(20, 18);
            editCard.Controls.Add(editTitle);

            Label nameLabel = CreateMutedLabel("Name");
            nameLabel.Location = new Point(22, 62);
            nameLabel.Size = new Size(90, 24);
            editCard.Controls.Add(nameLabel);
            serverProfileNameBox = CreateDarkTextBox(120, 60, 250);
            editCard.Controls.Add(serverProfileNameBox);

            serverProfileFavoriteCheck = CreateCheck("Favorite", false);
            serverProfileFavoriteCheck.Location = new Point(392, 62);
            editCard.Controls.Add(serverProfileFavoriteCheck);

            Label hostLabel = CreateMutedLabel("IP / Host");
            hostLabel.Location = new Point(22, 102);
            hostLabel.Size = new Size(90, 24);
            editCard.Controls.Add(hostLabel);
            serverProfileHostBox = CreateDarkTextBox(120, 100, 250);
            editCard.Controls.Add(serverProfileHostBox);

            Label portLabel = CreateMutedLabel("Port");
            portLabel.Location = new Point(392, 102);
            portLabel.Size = new Size(60, 24);
            editCard.Controls.Add(portLabel);
            serverProfilePortBox = new NumericUpDown();
            serverProfilePortBox.Minimum = 1;
            serverProfilePortBox.Maximum = 65535;
            serverProfilePortBox.Value = 19132;
            serverProfilePortBox.Location = new Point(438, 100);
            serverProfilePortBox.Size = new Size(70, 24);
            serverProfilePortBox.BackColor = Color.FromArgb(30, 37, 51);
            serverProfilePortBox.ForeColor = text;
            editCard.Controls.Add(serverProfilePortBox);

            Label categoryLabel = CreateMutedLabel("Category");
            categoryLabel.Location = new Point(22, 142);
            categoryLabel.Size = new Size(90, 24);
            editCard.Controls.Add(categoryLabel);
            serverProfileCategoryBox = new ComboBox();
            serverProfileCategoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
            serverProfileCategoryBox.BackColor = Color.FromArgb(30, 37, 51);
            serverProfileCategoryBox.ForeColor = text;
            serverProfileCategoryBox.FlatStyle = FlatStyle.Flat;
            serverProfileCategoryBox.Location = new Point(120, 140);
            serverProfileCategoryBox.Size = new Size(160, 24);
            foreach (string category in ServerProfileStore.Categories)
                serverProfileCategoryBox.Items.Add(category);
            serverProfileCategoryBox.SelectedIndex = 4;
            editCard.Controls.Add(serverProfileCategoryBox);

            Label packsLabel = CreateMutedLabel("Recommended packs");
            packsLabel.Location = new Point(22, 182);
            packsLabel.Size = new Size(130, 24);
            editCard.Controls.Add(packsLabel);
            serverProfilePacksBox = CreateDarkTextBox(160, 180, 348);
            editCard.Controls.Add(serverProfilePacksBox);

            Label notesLabel = CreateMutedLabel("Notes");
            notesLabel.Location = new Point(22, 222);
            notesLabel.Size = new Size(90, 24);
            editCard.Controls.Add(notesLabel);
            serverProfileNotesBox = CreateDarkTextBox(120, 220, 388);
            serverProfileNotesBox.Multiline = true;
            serverProfileNotesBox.Height = 80;
            editCard.Controls.Add(serverProfileNotesBox);

            Button saveServer = CreatePrimaryButton("Save Profile");
            saveServer.Location = new Point(22, 320);
            saveServer.Size = new Size(140, 38);
            saveServer.Click += delegate { SaveSelectedServerProfile(); };
            editCard.Controls.Add(saveServer);

            Button pingServer = CreateSecondaryButton("Safe Ping");
            pingServer.Location = new Point(180, 320);
            pingServer.Size = new Size(120, 38);
            pingServer.Click += delegate { PingSelectedServerProfile(); };
            editCard.Controls.Add(pingServer);

            serverProfileStatusLabel = CreateMutedLabel("Status: not checked");
            serverProfileStatusLabel.Location = new Point(22, 372);
            serverProfileStatusLabel.Size = new Size(470, 28);
            editCard.Controls.Add(serverProfileStatusLabel);

            Panel bridgeCard = CreateCard(0, 500, 904, 292);
            page.Controls.Add(bridgeCard);
            Label bridgeTitle = CreateCardTitle("Bomb Server Bridge");
            bridgeTitle.Location = new Point(20, 18);
            bridgeCard.Controls.Add(bridgeTitle);

            Label bridgeNote = CreateMutedLabel("Optional cooperating-server API. Disabled by default. No packet sniffing, traffic modification, injection, memory reading, or fake server-side data.");
            bridgeNote.Location = new Point(22, 50);
            bridgeNote.Size = new Size(820, 34);
            bridgeCard.Controls.Add(bridgeNote);

            bridgeEnabledCheck = CreateCheck("Enable bridge", BridgeManager.Settings.Enabled);
            bridgeEnabledCheck.Location = new Point(22, 96);
            bridgeCard.Controls.Add(bridgeEnabledCheck);

            bridgeMockCheck = CreateCheck("Mock/test mode", BridgeManager.Settings.MockMode);
            bridgeMockCheck.Location = new Point(160, 96);
            bridgeCard.Controls.Add(bridgeMockCheck);

            Label urlLabel = CreateMutedLabel("HTTP/WebSocket URL");
            urlLabel.Location = new Point(22, 138);
            urlLabel.Size = new Size(150, 24);
            bridgeCard.Controls.Add(urlLabel);
            bridgeUrlBox = CreateDarkTextBox(176, 136, 420);
            bridgeUrlBox.Text = BridgeManager.Settings.Url;
            bridgeCard.Controls.Add(bridgeUrlBox);

            Button saveBridge = CreatePrimaryButton("Save Bridge");
            saveBridge.Location = new Point(616, 130);
            saveBridge.Size = new Size(130, 38);
            saveBridge.Click += delegate { SaveBridgeSettingsFromUi(); };
            bridgeCard.Controls.Add(saveBridge);

            Button startMock = CreateSecondaryButton("Start Mock");
            startMock.Location = new Point(22, 184);
            startMock.Size = new Size(120, 38);
            startMock.Click += delegate
            {
                BridgeManager.StartMock();
                RefreshBridgeLabels();
                SetStatus("Bridge mock mode connected.");
            };
            bridgeCard.Controls.Add(startMock);

            Button stopBridge = CreateSecondaryButton("Stop");
            stopBridge.Location = new Point(158, 184);
            stopBridge.Size = new Size(90, 38);
            stopBridge.Click += delegate
            {
                BridgeManager.Stop();
                RefreshBridgeLabels();
                SetStatus("Bridge stopped.");
            };
            bridgeCard.Controls.Add(stopBridge);

            bridgeStatusLabel = CreateMutedLabel("Bridge status: " + BridgeManager.Status);
            bridgeStatusLabel.Location = new Point(270, 192);
            bridgeStatusLabel.Size = new Size(320, 28);
            bridgeCard.Controls.Add(bridgeStatusLabel);

            TextBox example = CreateDarkTextBox(616, 184, 250);
            example.Multiline = true;
            example.Height = 82;
            example.ReadOnly = true;
            example.Text = BridgeManager.ExampleJson();
            bridgeCard.Controls.Add(example);

            RefreshServerProfileList();
            RefreshBridgeLabels();
            return page;
        }

        private Panel BuildPacksPage()
        {
            Panel page = CreatePage();
            page.AutoScroll = true;
            Label heading = CreateHeading("Packs");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel card = CreateCard(0, 54, 760, 430);
            page.Controls.Add(card);
            Label title = CreateCardTitle("Add-On / Resource Pack Manager");
            title.Location = new Point(20, 18);
            card.Controls.Add(title);

            Label note = CreateMutedLabel("Packs are optional managed files for worlds or servers that intentionally support them. Bomb Client visuals stay in the external client layer and installed Bedrock files are never edited.");
            note.Location = new Point(22, 50);
            note.Size = new Size(690, 38);
            card.Controls.Add(note);

            managedPackList = new ListBox();
            managedPackList.Location = new Point(22, 104);
            managedPackList.Size = new Size(470, 250);
            managedPackList.BackColor = Color.FromArgb(14, 18, 24);
            managedPackList.ForeColor = text;
            managedPackList.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(managedPackList);

            Button importPack = CreatePrimaryButton("Import Pack");
            importPack.Location = new Point(520, 104);
            importPack.Size = new Size(160, 38);
            importPack.Click += delegate { ImportManagedPack(); };
            card.Controls.Add(importPack);

            Button togglePack = CreateSecondaryButton("Enable / Disable");
            togglePack.Location = new Point(520, 156);
            togglePack.Size = new Size(160, 38);
            togglePack.Click += delegate { ToggleSelectedManagedPack(); };
            card.Controls.Add(togglePack);

            Button openManaged = CreateSecondaryButton("Open Managed");
            openManaged.Location = new Point(520, 208);
            openManaged.Size = new Size(160, 38);
            openManaged.Click += delegate { OpenFolder(ManagedPackStore.ManagedRoot); };
            card.Controls.Add(openManaged);

            Button openResources = CreateSecondaryButton("Resource Folder");
            openResources.Location = new Point(520, 260);
            openResources.Size = new Size(160, 38);
            openResources.Click += delegate { OpenFolder(ManagedPackStore.ResourceDevelopmentFolder()); };
            card.Controls.Add(openResources);

            Button openBehaviors = CreateSecondaryButton("Behavior Folder");
            openBehaviors.Location = new Point(520, 312);
            openBehaviors.Size = new Size(160, 38);
            openBehaviors.Click += delegate { OpenFolder(ManagedPackStore.BehaviorDevelopmentFolder()); };
            card.Controls.Add(openBehaviors);

            Button generate = CreateSecondaryButton("Generate Optional Pack");
            generate.Location = new Point(22, 372);
            generate.Size = new Size(190, 38);
            generate.Click += delegate { GenerateOptionalVisualPack(); };
            card.Controls.Add(generate);

            Label limit = CreateMutedLabel("Generated packs are optional local Bedrock packs. Public-server support still comes from Bomb Client's external overlay/profile systems.");
            limit.Location = new Point(230, 378);
            limit.Size = new Size(480, 26);
            card.Controls.Add(limit);

            RefreshManagedPackList();
            return page;
        }

        private Panel BuildUpdatesPage()
        {
            Panel page = CreatePage();
            page.AutoScroll = true;
            Label heading = CreateHeading("Latest Updates");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel card = CreateCard(0, 54, 820, 500);
            page.Controls.Add(card);
            Label title = CreateCardTitle("Bomb Client Update Hub");
            title.Location = new Point(20, 18);
            card.Controls.Add(title);

            latestInstalledLabel = CreateMutedLabel("");
            latestInstalledLabel.Location = new Point(22, 58);
            latestInstalledLabel.Size = new Size(360, 26);
            card.Controls.Add(latestInstalledLabel);

            latestAvailableLabel = CreateMutedLabel("");
            latestAvailableLabel.Location = new Point(22, 90);
            latestAvailableLabel.Size = new Size(360, 26);
            card.Controls.Add(latestAvailableLabel);

            latestReleaseDateLabel = CreateMutedLabel("");
            latestReleaseDateLabel.Location = new Point(22, 122);
            latestReleaseDateLabel.Size = new Size(360, 26);
            card.Controls.Add(latestReleaseDateLabel);

            latestStatusLabel = CreateMutedLabel("");
            latestStatusLabel.Location = new Point(22, 154);
            latestStatusLabel.Size = new Size(720, 34);
            card.Controls.Add(latestStatusLabel);

            latestChangelogBox = CreateDarkTextBox(22, 204, 760);
            latestChangelogBox.Multiline = true;
            latestChangelogBox.ReadOnly = true;
            latestChangelogBox.ScrollBars = ScrollBars.Vertical;
            latestChangelogBox.Height = 210;
            card.Controls.Add(latestChangelogBox);

            Button refresh = CreatePrimaryButton("Refresh");
            refresh.Location = new Point(22, 438);
            refresh.Size = new Size(120, 38);
            refresh.Click += delegate { RefreshLatestUpdates(); };
            card.Controls.Add(refresh);

            Button openRelease = CreateSecondaryButton("Open Release");
            openRelease.Location = new Point(160, 438);
            openRelease.Size = new Size(140, 38);
            openRelease.Click += delegate { OpenAccountTarget(latestReleaseUrl, "GitHub release page opened."); };
            card.Controls.Add(openRelease);

            DisplayUpdateManifest(UpdateChecker.ParseManifest(LocalUpdateJson()));
            return page;
        }

        private TextBox CreateDarkTextBox(int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.Location = new Point(x, y);
            box.Size = new Size(width, 24);
            box.BackColor = Color.FromArgb(30, 37, 51);
            box.ForeColor = text;
            box.BorderStyle = BorderStyle.FixedSingle;
            return box;
        }

        private void RefreshServerProfileList()
        {
            if (serverProfileList == null)
                return;
            serverProfileList.Items.Clear();
            foreach (ServerProfile profile in serverProfiles)
                serverProfileList.Items.Add(profile);
            if (serverProfileList.Items.Count > 0 && serverProfileList.SelectedIndex < 0)
                serverProfileList.SelectedIndex = 0;
        }

        private ServerProfile SelectedServerProfile()
        {
            if (serverProfileList == null)
                return null;
            return serverProfileList.SelectedItem as ServerProfile;
        }

        private void LoadSelectedServerProfile()
        {
            ServerProfile profile = SelectedServerProfile();
            if (profile == null)
                return;
            serverProfileNameBox.Text = profile.Name;
            serverProfileHostBox.Text = profile.Host;
            serverProfilePortBox.Value = Math.Max(serverProfilePortBox.Minimum, Math.Min(serverProfilePortBox.Maximum, profile.Port));
            serverProfileFavoriteCheck.Checked = profile.Favorite;
            SelectComboText(serverProfileCategoryBox, profile.Category);
            serverProfilePacksBox.Text = profile.RecommendedPacks;
            serverProfileNotesBox.Text = profile.Notes;
            serverProfileStatusLabel.Text = "Status: " + profile.Host + ":" + profile.Port.ToString();
        }

        private void SaveSelectedServerProfile()
        {
            ServerProfile profile = SelectedServerProfile();
            if (profile == null)
                return;
            profile.Name = serverProfileNameBox.Text.Trim().Length == 0 ? "New Server" : serverProfileNameBox.Text.Trim();
            profile.Host = serverProfileHostBox.Text.Trim().Length == 0 ? "play.cubecraft.net" : serverProfileHostBox.Text.Trim();
            profile.Port = (int)serverProfilePortBox.Value;
            profile.Category = serverProfileCategoryBox.SelectedItem == null ? "Custom" : serverProfileCategoryBox.SelectedItem.ToString();
            profile.Favorite = serverProfileFavoriteCheck.Checked;
            profile.RecommendedPacks = serverProfilePacksBox.Text.Trim();
            profile.Notes = serverProfileNotesBox.Text.Trim();
            ServerProfileStore.Save(serverProfiles);
            RefreshServerProfileList();
            SetStatus("Server profile saved.");
        }

        private void NewServerProfile()
        {
            ServerProfile profile = new ServerProfile();
            profile.Name = "Custom Server";
            serverProfiles.Add(profile);
            ServerProfileStore.Save(serverProfiles);
            RefreshServerProfileList();
            serverProfileList.SelectedItem = profile;
            SetStatus("New server profile created.");
        }

        private void DeleteSelectedServerProfile()
        {
            ServerProfile profile = SelectedServerProfile();
            if (profile == null)
                return;
            serverProfiles.Remove(profile);
            ServerProfileStore.Save(serverProfiles);
            RefreshServerProfileList();
            SetStatus("Server profile deleted.");
        }

        private void UseSelectedServerProfile()
        {
            ServerProfile profile = SelectedServerProfile();
            if (profile == null)
                return;
            settings.ServerHost = profile.Host;
            settings.ServerPort = profile.Port;
            if (serverHostBox != null)
                serverHostBox.Text = settings.ServerHost;
            if (serverPortBox != null)
                serverPortBox.Value = settings.ServerPort;
            profile.LastJoinedUtc = DateTime.UtcNow;
            ServerProfileStore.Save(serverProfiles);
            settings.Save();
            OverlayManager.Configure(settings);
            SetStatus("Active server set to " + profile.Host + ":" + profile.Port.ToString() + ". Launch Bedrock and join normally from the server list.");
        }

        private void PingSelectedServerProfile()
        {
            ServerProfile profile = SelectedServerProfile();
            if (profile == null)
                return;
            serverProfileStatusLabel.Text = "Status: checking...";
            ServerStatusResult result = ServerProfileStore.CheckStatus(profile);
            serverProfileStatusLabel.Text = "Status: " + result.Message;
        }

        private void SelectComboText(ComboBox combo, string value)
        {
            if (combo == null)
                return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = combo.Items.Count - 1;
        }

        private void SaveBridgeSettingsFromUi()
        {
            BridgeSettings next = new BridgeSettings();
            next.Enabled = bridgeEnabledCheck != null && bridgeEnabledCheck.Checked;
            next.MockMode = bridgeMockCheck != null && bridgeMockCheck.Checked;
            next.Url = bridgeUrlBox == null || bridgeUrlBox.Text.Trim().Length == 0 ? "http://localhost:39132/bomb-client" : bridgeUrlBox.Text.Trim();
            BridgeManager.SaveSettings(next);
            BridgeManager.RefreshStatus();
            RefreshBridgeLabels();
            SetStatus("Bridge settings saved.");
        }

        private void RefreshBridgeLabels()
        {
            if (bridgeEnabledCheck != null)
                bridgeEnabledCheck.Checked = BridgeManager.Settings.Enabled;
            if (bridgeMockCheck != null)
                bridgeMockCheck.Checked = BridgeManager.Settings.MockMode;
            if (bridgeUrlBox != null)
                bridgeUrlBox.Text = BridgeManager.Settings.Url;
            if (bridgeStatusLabel != null)
                bridgeStatusLabel.Text = "Bridge status: " + BridgeManager.Status;
        }

        private void RefreshManagedPackList()
        {
            if (managedPackList == null)
                return;
            managedPackList.Items.Clear();
            foreach (ManagedPack pack in managedPacks)
                managedPackList.Items.Add(pack);
        }

        private ManagedPack SelectedManagedPack()
        {
            if (managedPackList == null)
                return null;
            return managedPackList.SelectedItem as ManagedPack;
        }

        private void ImportManagedPack()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Bedrock packs (*.mcpack;*.mcaddon;*.zip)|*.mcpack;*.mcaddon;*.zip|All files (*.*)|*.*";
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                ManagedPack pack = ManagedPackStore.Import(dialog.FileName);
                managedPacks.Add(pack);
                ManagedPackStore.Save(managedPacks);
                RefreshManagedPackList();
                SetStatus("Pack imported into Bomb Client.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Pack import failed.\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ToggleSelectedManagedPack()
        {
            ManagedPack pack = SelectedManagedPack();
            if (pack == null)
                return;
            pack.Enabled = !pack.Enabled;
            ManagedPackStore.Save(managedPacks);
            RefreshManagedPackList();
            SetStatus(pack.Name + (pack.Enabled ? " enabled." : " disabled."));
        }

        private void GenerateOptionalVisualPack()
        {
            try
            {
                PersistSettingsFromUi();
                string path = ResourcePackBuilder.Build(settings);
                ManagedPack pack = ManagedPackStore.Import(path);
                pack.Name = "Bomb Client Optional Visual Pack";
                managedPacks.Add(pack);
                ManagedPackStore.Save(managedPacks);
                RefreshManagedPackList();
                SetStatus("Optional local visual pack generated.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not generate optional visual pack.\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open folder.\n\n" + ex.Message, "Bomb Client", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshLatestUpdates()
        {
            UpdateManifest manifest = UpdateChecker.FetchManifest();
            if (manifest == null)
                manifest = UpdateChecker.ParseManifest(LocalUpdateJson());
            DisplayUpdateManifest(manifest);
        }

        private void DisplayUpdateManifest(UpdateManifest manifest)
        {
            if (manifest == null)
                manifest = UpdateChecker.ParseManifest(LocalUpdateJson());

            latestReleaseUrl = manifest.ReleasePage;
            if (latestInstalledLabel != null)
                latestInstalledLabel.Text = "Installed version: " + AppInfo.Version;
            if (latestAvailableLabel != null)
                latestAvailableLabel.Text = "Latest available: " + manifest.LatestVersion;
            if (latestReleaseDateLabel != null)
                latestReleaseDateLabel.Text = "Release date: " + (manifest.ReleaseDate.Length == 0 ? "Unknown" : manifest.ReleaseDate);

            string status = "Bomb Client is up to date.";
            if (manifest.RequiresUpdate())
                status = "A required update is available. Players must update before continuing.";
            else if (manifest.HasOptionalUpdate())
                status = "A newer optional update is available.";

            if (latestStatusLabel != null)
                latestStatusLabel.Text = "Update status: " + status;
            if (latestChangelogBox != null)
            {
                string title = manifest.ReleaseTitle.Length == 0 ? "Release notes" : manifest.ReleaseTitle;
                string body = manifest.Changelog.Length == 0 ? manifest.Notes : manifest.Changelog;
                latestChangelogBox.Text = title + Environment.NewLine + Environment.NewLine + body;
            }
        }

        private string LocalUpdateJson()
        {
            return "{\"latest_version\":\"" + AppInfo.Version + "\"," +
                "\"required_version\":\"" + AppInfo.Version + "\"," +
                "\"force_update\":true," +
                "\"release_date\":\"2026-05-29\"," +
                "\"release_title\":\"Bomb Client 2.0 Core\"," +
                "\"changelog\":\"Module architecture, server profiles, pack manager, optional bridge foundation, overlay data API, and Latest Updates tab.\"," +
                "\"github_release_url\":\"https://github.com/EnderKraken914/bomb-client/releases/tag/v2.0.0\"," +
                "\"release_page\":\"https://github.com/EnderKraken914/bomb-client/releases/tag/v2.0.0\"," +
                "\"download_url\":\"" + AppInfo.ReleaseDownloadUrl + "\"}";
        }

        private Panel BuildVisualPage()
        {
            Panel page = CreatePage();
            page.AutoScroll = true;
            Label heading = CreateHeading("Client Visuals");
            heading.Location = new Point(0, 0);
            page.Controls.Add(heading);

            Panel card = CreateCard(0, 54, 720, 590);
            page.Controls.Add(card);
            Label title = CreateCardTitle("Bomb Client Visual Layer");
            title.Location = new Point(20, 18);
            card.Controls.Add(title);

            Label note = CreateMutedLabel("Server-safe external visuals drawn by Bomb Client itself. No .mcpack or server resource-pack slot is used.");
            note.Location = new Point(22, 48);
            note.Size = new Size(650, 34);
            card.Controls.Add(note);

            lowFireCheck = CreateCheck("Low fire", settings.VisualLowFire);
            lowFireCheck.Location = new Point(22, 94);
            card.Controls.Add(lowFireCheck);

            noBobberCheck = CreateCheck("No bobber marker", settings.VisualNoBobber);
            noBobberCheck.Location = new Point(22, 134);
            card.Controls.Add(noBobberCheck);

            lowShieldCheck = CreateCheck("Low shield", settings.VisualLowShield);
            lowShieldCheck.Location = new Point(22, 174);
            card.Controls.Add(lowShieldCheck);

            smallTotemCheck = CreateCheck("Small totem", settings.VisualSmallTotem);
            smallTotemCheck.Location = new Point(22, 214);
            card.Controls.Add(smallTotemCheck);

            smallTotemPopCheck = CreateCheck("Small totem pop", settings.VisualSmallTotemPop);
            smallTotemPopCheck.Location = new Point(22, 254);
            card.Controls.Add(smallTotemPopCheck);

            cleanPumpkinCheck = CreateCheck("Clean pumpkin frame", settings.VisualCleanPumpkin);
            cleanPumpkinCheck.Location = new Point(300, 94);
            card.Controls.Add(cleanPumpkinCheck);

            clearVignetteCheck = CreateCheck("Clear vignette guard", settings.VisualClearVignette);
            clearVignetteCheck.Location = new Point(300, 134);
            card.Controls.Add(clearVignetteCheck);

            Label sizeTitle = CreateCardTitle("Visual Sizes");
            sizeTitle.Location = new Point(20, 304);
            card.Controls.Add(sizeTitle);

            Label shieldLabel = CreateMutedLabel("Shield");
            shieldLabel.Location = new Point(24, 338);
            shieldLabel.Size = new Size(110, 20);
            card.Controls.Add(shieldLabel);
            shieldSizeTrack = CreatePercentTrack(settings.VisualShieldSize);
            shieldSizeTrack.Location = new Point(22, 360);
            shieldSizeTrack.Scroll += delegate { settings.VisualShieldSize = shieldSizeTrack.Value; shieldSizeLabel.Text = settings.VisualShieldSize.ToString() + "%"; };
            card.Controls.Add(shieldSizeTrack);
            shieldSizeLabel = CreateMutedLabel(settings.VisualShieldSize.ToString() + "%");
            shieldSizeLabel.Location = new Point(400, 362);
            shieldSizeLabel.Size = new Size(80, 20);
            card.Controls.Add(shieldSizeLabel);

            Label totemLabel = CreateMutedLabel("Totem item");
            totemLabel.Location = new Point(24, 398);
            totemLabel.Size = new Size(140, 20);
            card.Controls.Add(totemLabel);
            totemSizeTrack = CreatePercentTrack(settings.VisualTotemSize);
            totemSizeTrack.Location = new Point(22, 420);
            totemSizeTrack.Scroll += delegate { settings.VisualTotemSize = totemSizeTrack.Value; totemSizeLabel.Text = settings.VisualTotemSize.ToString() + "%"; };
            card.Controls.Add(totemSizeTrack);
            totemSizeLabel = CreateMutedLabel(settings.VisualTotemSize.ToString() + "%");
            totemSizeLabel.Location = new Point(400, 422);
            totemSizeLabel.Size = new Size(80, 20);
            card.Controls.Add(totemSizeLabel);

            Label popLabel = CreateMutedLabel("Totem pop");
            popLabel.Location = new Point(24, 458);
            popLabel.Size = new Size(140, 20);
            card.Controls.Add(popLabel);
            totemPopSizeTrack = CreatePercentTrack(settings.VisualTotemPopSize);
            totemPopSizeTrack.Location = new Point(22, 480);
            totemPopSizeTrack.Scroll += delegate { settings.VisualTotemPopSize = totemPopSizeTrack.Value; totemPopSizeLabel.Text = settings.VisualTotemPopSize.ToString() + "%"; };
            card.Controls.Add(totemPopSizeTrack);
            totemPopSizeLabel = CreateMutedLabel(settings.VisualTotemPopSize.ToString() + "%");
            totemPopSizeLabel.Location = new Point(400, 482);
            totemPopSizeLabel.Size = new Size(80, 20);
            card.Controls.Add(totemPopSizeLabel);

            Button apply = CreatePrimaryButton("Apply Client Visuals");
            apply.Location = new Point(22, 520);
            apply.Size = new Size(190, 42);
            apply.Click += delegate { ApplyClientVisuals(true); };
            card.Controls.Add(apply);

            Button stop = CreateSecondaryButton("Stop Visuals");
            stop.Location = new Point(230, 520);
            stop.Size = new Size(140, 42);
            stop.Click += delegate { ApplyClientVisuals(false); };
            card.Controls.Add(stop);

            Label limit = CreateMutedLabel("External visuals can draw over Bedrock, but cannot erase Bedrock pixels or replace item models without game hooks.");
            limit.Location = new Point(388, 520);
            limit.Size = new Size(300, 42);
            card.Controls.Add(limit);

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

            Panel hudCard = CreateCard(0, 288, 620, 230);
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

            Label scaleTitle = CreateMutedLabel("Overlay size");
            scaleTitle.Location = new Point(22, 120);
            scaleTitle.Size = new Size(120, 20);
            hudCard.Controls.Add(scaleTitle);

            overlayScaleTrack = new TrackBar();
            overlayScaleTrack.Minimum = 60;
            overlayScaleTrack.Maximum = 180;
            overlayScaleTrack.TickFrequency = 20;
            overlayScaleTrack.Value = settings.OverlayScale;
            overlayScaleTrack.Location = new Point(20, 146);
            overlayScaleTrack.Width = 360;
            overlayScaleTrack.BackColor = panel;
            overlayScaleTrack.Scroll += delegate
            {
                settings.OverlayScale = overlayScaleTrack.Value;
                OverlayManager.SetScale(settings.OverlayScale);
                overlayScaleLabel.Text = settings.OverlayScale.ToString() + "%";
            };
            hudCard.Controls.Add(overlayScaleTrack);

            overlayScaleLabel = CreateMutedLabel(settings.OverlayScale.ToString() + "%");
            overlayScaleLabel.Location = new Point(400, 153);
            overlayScaleLabel.Size = new Size(80, 24);
            hudCard.Controls.Add(overlayScaleLabel);

            Panel behaviorCard = CreateCard(0, 542, 620, 132);
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
            UpdateNavigationState(name);
        }

        private void PersistSettingsFromUi()
        {
            if (serverHostBox != null)
                settings.ServerHost = serverHostBox.Text.Trim().Length == 0 ? "play.cubecraft.net" : serverHostBox.Text.Trim();
            if (serverPortBox != null)
                settings.ServerPort = (int)serverPortBox.Value;
            if (opacityTrack != null)
                settings.OverlayOpacity = opacityTrack.Value;
            if (overlayScaleTrack != null)
                settings.OverlayScale = overlayScaleTrack.Value;
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
            if (microsoftClientIdBox != null)
                settings.MicrosoftClientId = microsoftClientIdBox.Text.Trim();
            if (lowFireCheck != null)
                settings.VisualLowFire = lowFireCheck.Checked;
            if (noBobberCheck != null)
                settings.VisualNoBobber = noBobberCheck.Checked;
            if (lowShieldCheck != null)
                settings.VisualLowShield = lowShieldCheck.Checked;
            if (smallTotemCheck != null)
                settings.VisualSmallTotem = smallTotemCheck.Checked;
            if (smallTotemPopCheck != null)
                settings.VisualSmallTotemPop = smallTotemPopCheck.Checked;
            if (shieldSizeTrack != null)
                settings.VisualShieldSize = shieldSizeTrack.Value;
            if (totemSizeTrack != null)
                settings.VisualTotemSize = totemSizeTrack.Value;
            if (totemPopSizeTrack != null)
                settings.VisualTotemPopSize = totemPopSizeTrack.Value;
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

        private void ApplyClientVisuals(bool enabled)
        {
            PersistSettingsFromUi();
            settings.Overlays["clientvisuals"] = enabled;
            settings.Save();
            OverlayManager.Configure(settings);
            OverlayManager.SetOverlay("clientvisuals", enabled);
            SetStatus(enabled ? "Client visual layer enabled." : "Client visual layer stopped.");
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

        private TrackBar CreatePercentTrack(int value)
        {
            TrackBar track = new TrackBar();
            track.Minimum = 20;
            track.Maximum = 100;
            track.TickFrequency = 10;
            track.Value = Math.Max(track.Minimum, Math.Min(track.Maximum, value));
            track.Width = 360;
            track.BackColor = panel;
            return track;
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
            new OverlayDefinition("clientvisuals", "Client Visuals", "Server-safe Bomb Client visual layer, no resource pack required.", true, 0, 0),
            new OverlayDefinition("clock", "Clock", "Compact local time display.", false, 22, 378),
            new OverlayDefinition("session", "Session Timer", "Timer for your current Bomb Client session.", false, 22, 420),
            new OverlayDefinition("memory", "Minecraft RAM", "Memory usage for the Bedrock process when it is running.", true, 22, 462),
            new OverlayDefinition("system", "System Stats", "PC memory and CPU load readout.", false, 22, 504),
            new OverlayDefinition("server", "Server Info", "Current configured Bedrock server target.", false, 22, 546),
            new OverlayDefinition("status", "Game Status", "Shows whether Minecraft Bedrock is running.", false, 22, 588),
            new OverlayDefinition("armor", "Armor HUD", "Armor-style panel with durability slots for HUD positioning.", false, 1710, 712),
            new OverlayDefinition("potions", "Potion Effects", "Potion-style list matching the reference HUD style.", false, 22, 650),
            new OverlayDefinition("hotbar", "Hotbar Preview", "Compact hotbar-adjacent item panel for external HUD layout.", false, 1220, 870),
            new OverlayDefinition("shulker", "Shulker Preview", "Shift / Shift+Alt shulker preview overlay shell.", false, 800, 420)
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

            if (id == "crosshair" || id == "clientvisuals")
            {
                form.Bounds = Screen.PrimaryScreen.Bounds;
            }
            else
            {
                form.Location = pos;
            }

            form.EditMode = settings.EditMode;
            form.Opacity = Math.Max(0.4, Math.Min(1.0, settings.OverlayOpacity / 100.0));
            form.SetScale(settings.OverlayScale);
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

        public static void SetScale(int scale)
        {
            if (settings != null)
                settings.OverlayScale = scale;
            foreach (BaseOverlayForm form in open.Values)
                form.SetScale(scale);
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
            if (id == "clientvisuals")
                return new ClientVisualsOverlayForm(id, settings);
            if (id == "clock")
                return new TextOverlayForm(id, 130, 36, delegate { return DateTime.Now.ToString("h:mm:ss tt"); });
            if (id == "session")
                return new TextOverlayForm(id, 138, 36, delegate { return "SESSION " + SessionClock.ElapsedText; });
            if (id == "memory")
                return new TextOverlayForm(id, 178, 36, delegate { return OverlayDataContext.Current(settings).MemoryStatus; });
            if (id == "system")
                return new TextOverlayForm(id, 180, 36, delegate { return OverlayDataContext.Current(settings).SystemStatus; });
            if (id == "server")
                return new TextOverlayForm(id, 270, 36, delegate { return OverlayDataContext.Current(settings).ServerInfoText(); });
            if (id == "status")
                return new TextOverlayForm(id, 230, 36, delegate { OverlayDataSnapshot data = OverlayDataContext.Current(settings); return (data.MinecraftRunning ? "BEDROCK RUNNING" : "BEDROCK CLOSED") + " | BRIDGE " + data.BridgeStatus.ToUpperInvariant(); });
            if (id == "armor")
                return new ArmorHudOverlayForm(id);
            if (id == "potions")
                return new PotionEffectsOverlayForm(id);
            if (id == "hotbar")
                return new HotbarOverlayForm(id);
            if (id == "shulker")
                return new ShulkerPreviewOverlayForm(id);
            return null;
        }
    }

    internal abstract class BaseOverlayForm : Form
    {
        private bool editMode;
        private Point dragStart;
        private bool dragging;
        private readonly int baseWidth;
        private readonly int baseHeight;
        protected readonly string ModuleId;
        protected readonly Timer Timer;
        protected int ScalePercent = 100;
        public event Action<string, Point> PositionChanged;

        protected BaseOverlayForm(string moduleId, int width, int height)
        {
            ModuleId = moduleId;
            baseWidth = width;
            baseHeight = height;
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

        public virtual void SetScale(int percent)
        {
            ScalePercent = Math.Max(60, Math.Min(180, percent));
            Width = S(baseWidth);
            Height = S(baseHeight);
            Invalidate();
        }

        protected int S(int value)
        {
            return Math.Max(1, (int)Math.Round(value * (ScalePercent / 100.0)));
        }

        protected float SF(float value)
        {
            return (float)(value * (ScalePercent / 100.0));
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
            using (Font font = new Font("Segoe UI Semibold", SF(10.5f), FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, value, font, new Rectangle(S(10), S(7), Width - S(20), Height - S(12)), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            base.OnPaint(e);
        }
    }

    internal sealed class PingOverlayForm : BaseOverlayForm
    {
        private readonly AppSettings settings;
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
            using (Font font = new Font("Segoe UI Semibold", SF(10.5f), FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, current, font, new Rectangle(S(10), S(7), Width - S(20), Height - S(12)), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
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
            using (Font font = new Font("Segoe UI Semibold", label.Length > 1 ? 8.5f : 10f, FontStyle.Bold))
                DrawKeyBox(g, label, down, x, y, w, h, font);
        }

        private void DrawMouse(Graphics g, string label, bool down, int x, int y, int w, int h)
        {
            using (Font font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold))
                DrawKeyBox(g, label, down, x, y, w, h, font);
        }

        private void DrawKeyBox(Graphics g, string label, bool down, int x, int y, int w, int h, Font font)
        {
            Rectangle rect = new Rectangle(S(x), S(y), S(w), S(h));
            using (GraphicsPath path = RoundedRect(rect, 6))
            using (SolidBrush brush = new SolidBrush(down ? Color.FromArgb(255, 164, 58) : Color.FromArgb(37, 44, 58)))
            using (Pen pen = new Pen(down ? Color.FromArgb(255, 203, 104) : Color.FromArgb(64, 75, 94)))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
            using (Font scaled = new Font(font.FontFamily, SF(font.Size), font.Style))
                TextRenderer.DrawText(g, label, scaled, rect, down ? Color.FromArgb(20, 20, 24) : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class CrosshairOverlayForm : BaseOverlayForm
    {
        public CrosshairOverlayForm(string moduleId)
            : base(moduleId, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        {
            TopMost = true;
        }

        public override void SetScale(int percent)
        {
            ScalePercent = Math.Max(60, Math.Min(180, percent));
            Bounds = Screen.PrimaryScreen.Bounds;
            Invalidate();
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

    internal sealed class ClientVisualsOverlayForm : BaseOverlayForm
    {
        private readonly AppSettings settings;

        public ClientVisualsOverlayForm(string moduleId, AppSettings appSettings)
            : base(moduleId, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        {
            settings = appSettings;
            Timer.Interval = 60;
        }

        public override void SetScale(int percent)
        {
            ScalePercent = Math.Max(60, Math.Min(180, percent));
            Bounds = Screen.PrimaryScreen.Bounds;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (settings.VisualLowFire)
                DrawLowFireCover(g);
            if (settings.VisualCleanPumpkin || settings.VisualClearVignette)
                DrawCleanScreenFrame(g);
            if (settings.VisualLowShield)
                DrawShieldMarker(g);
            if (settings.VisualSmallTotem)
                DrawTotemMarker(g, false);
            if (settings.VisualSmallTotemPop)
                DrawTotemMarker(g, true);
            if (settings.VisualNoBobber)
                DrawNoBobberMarker(g);

            if (EditMode)
            {
                using (Pen pen = new Pen(Color.FromArgb(190, 255, 164, 58), 2f))
                    g.DrawRectangle(pen, 8, 8, Width - 17, Height - 17);
                using (Font font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "Bomb Client visual layer", font, new Rectangle(18, 16, 260, 28), Color.FromArgb(255, 164, 58));
            }

            base.OnPaint(e);
        }

        private void DrawLowFireCover(Graphics g)
        {
            int coverHeight = Math.Max(48, Height / 9);
            Rectangle cover = new Rectangle(0, Height - coverHeight, Width, coverHeight);
            using (LinearGradientBrush brush = new LinearGradientBrush(cover, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(210, 2, 5, 8), LinearGradientMode.Vertical))
                g.FillRectangle(brush, cover);

            int flameBase = Height - S(22);
            int flameTop = Height - Math.Max(S(54), coverHeight / 2);
            int flameWidth = S(18);
            int spacing = S(28);
            int start = Width / 2 - spacing * 5;
            using (SolidBrush redBrush = new SolidBrush(Color.FromArgb(120, 230, 44, 30)))
            using (SolidBrush orangeBrush = new SolidBrush(Color.FromArgb(105, 255, 142, 36)))
            {
                for (int i = 0; i < 11; i++)
                {
                    int x = start + i * spacing;
                    Point[] flame = new Point[]
                    {
                        new Point(x, flameBase),
                        new Point(x + flameWidth / 2, flameTop + (i % 3) * S(5)),
                        new Point(x + flameWidth, flameBase)
                    };
                    g.FillPolygon(i % 2 == 0 ? orangeBrush : redBrush, flame);
                }
            }
        }

        private void DrawCleanScreenFrame(Graphics g)
        {
            using (Pen frame = new Pen(Color.FromArgb(55, 255, 255, 255), S(2)))
            {
                g.DrawLine(frame, S(22), S(22), Width - S(22), S(22));
                g.DrawLine(frame, S(22), Height - S(22), Width - S(22), Height - S(22));
            }
        }

        private void DrawShieldMarker(Graphics g)
        {
            int size = Math.Max(S(34), S(settings.VisualShieldSize));
            int x = S(42);
            int y = Height - size - S(42);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(new Point[]
                {
                    new Point(x + size / 2, y),
                    new Point(x + size, y + size / 5),
                    new Point(x + size - size / 7, y + size - size / 8),
                    new Point(x + size / 2, y + size),
                    new Point(x + size / 7, y + size - size / 8),
                    new Point(x, y + size / 5)
                });
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(92, 120, 92, 62)))
                using (Pen pen = new Pen(Color.FromArgb(130, 220, 190, 130), S(2)))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawTotemMarker(Graphics g, bool pop)
        {
            int sizeSetting = pop ? settings.VisualTotemPopSize : settings.VisualTotemSize;
            int size = Math.Max(S(28), S(sizeSetting));
            int x = Width - size - S(pop ? 118 : 58);
            int y = Height - size - S(pop ? 104 : 48);

            if (pop)
            {
                using (Pen ray = new Pen(Color.FromArgb(125, 255, 231, 95), S(3)))
                {
                    int cx = x + size / 2;
                    int cy = y + size / 2;
                    g.DrawLine(ray, cx, cy - size / 2 - S(18), cx, cy - size / 2);
                    g.DrawLine(ray, cx + size / 2, cy, cx + size / 2 + S(18), cy);
                    g.DrawLine(ray, cx - size / 2, cy, cx - size / 2 - S(18), cy);
                }
            }

            using (SolidBrush gold = new SolidBrush(Color.FromArgb(150, 245, 226, 83)))
            using (SolidBrush green = new SolidBrush(Color.FromArgb(135, 74, 190, 111)))
            using (Pen edge = new Pen(Color.FromArgb(150, 64, 88, 38), S(2)))
            {
                Rectangle body = new Rectangle(x + size / 3, y + size / 4, size / 3, size / 2);
                Rectangle head = new Rectangle(x + size / 3, y, size / 3, size / 4);
                g.FillRectangle(gold, head);
                g.FillRectangle(gold, body);
                g.FillRectangle(green, x + size / 8, y + size / 3, size / 4, size / 4);
                g.FillRectangle(green, x + size - size / 3, y + size / 3, size / 4, size / 4);
                g.DrawRectangle(edge, x + size / 4, y, size / 2, size - 1);
            }
        }

        private void DrawNoBobberMarker(Graphics g)
        {
            int x = Width - S(92);
            int y = S(118);
            using (Pen line = new Pen(Color.FromArgb(150, 245, 62, 62), S(3)))
            using (SolidBrush dot = new SolidBrush(Color.FromArgb(110, 245, 245, 245)))
            {
                g.FillEllipse(dot, x, y, S(18), S(18));
                g.DrawLine(line, x - S(4), y + S(22), x + S(24), y - S(6));
            }
        }
    }

    internal sealed class ArmorHudOverlayForm : BaseOverlayForm
    {
        public ArmorHudOverlayForm(string moduleId)
            : base(moduleId, 94, 246)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawOverlayBack(e.Graphics, ClientRectangle);
            if (BridgeManager.CurrentData.ArmorHud.Length > 0)
            {
                string[] lines = BridgeManager.CurrentData.ArmorHud.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                using (Font font = new Font("Segoe UI Semibold", SF(9f), FontStyle.Bold))
                {
                    for (int i = 0; i < lines.Length && i < 6; i++)
                        TextRenderer.DrawText(e.Graphics, lines[i], font, new Rectangle(S(10), S(10 + (i * 28)), S(78), S(24)), Color.White, TextFormatFlags.Left);
                }
                base.OnPaint(e);
                return;
            }

            string[] names = new string[] { "H", "C", "L", "B" };
            for (int i = 0; i < names.Length; i++)
                DrawArmorSlot(e.Graphics, names[i], i, 6 + (i * 56));

            using (Font font = new Font("Segoe UI Semibold", SF(7.5f), FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, "external", font, new Rectangle(S(12), S(226), S(70), S(16)), Color.FromArgb(152, 163, 180), TextFormatFlags.HorizontalCenter);
            base.OnPaint(e);
        }

        private void DrawArmorSlot(Graphics g, string label, int index, int y)
        {
            Rectangle rect = new Rectangle(S(12), S(y), S(70), S(48));
            using (GraphicsPath path = RoundedRect(rect, S(10)))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(38, 30, 58)))
            using (Pen pen = new Pen(Color.FromArgb(105, 76, 160)))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            using (Font icon = new Font("Segoe UI Semibold", SF(13f), FontStyle.Bold))
                TextRenderer.DrawText(g, label, icon, rect, Color.FromArgb(165, 120, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int durability = 100 - (index * 13);
            Rectangle bar = new Rectangle(S(18), S(y + 38), S(58), S(5));
            using (SolidBrush back = new SolidBrush(Color.FromArgb(17, 22, 30)))
                g.FillRectangle(back, bar);
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(51, 222, 137)))
                g.FillRectangle(fill, new Rectangle(bar.X, bar.Y, (int)(bar.Width * (durability / 100.0)), bar.Height));
        }
    }

    internal sealed class PotionEffectsOverlayForm : BaseOverlayForm
    {
        public PotionEffectsOverlayForm(string moduleId)
            : base(moduleId, 252, 154)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawOverlayBack(e.Graphics, ClientRectangle);
            string bridgeEffects = BridgeManager.CurrentData.PotionEffects;
            using (Font effectFont = new Font("Segoe UI Semibold", SF(15f), FontStyle.Bold))
            using (Font timeFont = new Font("Segoe UI", SF(10f), FontStyle.Bold))
            {
                if (bridgeEffects.Length > 0)
                {
                    string[] lines = bridgeEffects.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length && i < 4; i++)
                        DrawEffect(e.Graphics, effectFont, timeFont, 14 + (i * 34), lines[i], "bridge");
                }
                else
                {
                    DrawEffect(e.Graphics, effectFont, timeFont, 14, "Potion effects", "unavailable");
                    DrawEffect(e.Graphics, effectFont, timeFont, 58, "Bridge/mock data", "required");
                }
            }
            base.OnPaint(e);
        }

        private void DrawEffect(Graphics g, Font effectFont, Font timeFont, int y, string name, string time)
        {
            TextRenderer.DrawText(g, name, effectFont, new Rectangle(S(14), S(y), S(224), S(28)), Color.White, TextFormatFlags.Left);
            TextRenderer.DrawText(g, time, timeFont, new Rectangle(S(14), S(y + 26), S(120), S(20)), Color.White, TextFormatFlags.Left);
        }
    }

    internal sealed class HotbarOverlayForm : BaseOverlayForm
    {
        public HotbarOverlayForm(string moduleId)
            : base(moduleId, 292, 48)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawOverlayBack(e.Graphics, ClientRectangle);
            string hotbar = BridgeManager.CurrentData.Hotbar;
            if (hotbar.Length > 0)
            {
                string[] items = hotbar.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < 9; i++)
                    DrawSlot(e.Graphics, i, i < items.Length ? items[i].Trim() : (i + 1).ToString());
            }
            else
            {
                for (int i = 0; i < 9; i++)
                    DrawSlot(e.Graphics, i, i == 0 ? "N/A" : (i + 1).ToString());
            }
            base.OnPaint(e);
        }

        private void DrawSlot(Graphics g, int index, string label)
        {
            int x = 8 + (index * 31);
            Rectangle rect = new Rectangle(S(x), S(8), S(28), S(32));
            using (SolidBrush brush = new SolidBrush(index == 0 ? Color.FromArgb(70, 88, 112) : Color.FromArgb(31, 38, 50)))
            using (Pen pen = new Pen(Color.FromArgb(72, 83, 104)))
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect);
            }

            using (Font font = new Font("Segoe UI Semibold", SF(9f), FontStyle.Bold))
                TextRenderer.DrawText(g, ShortHotbarLabel(label), font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private string ShortHotbarLabel(string value)
        {
            if (value == null || value.Length == 0)
                return "";
            return value.Length <= 3 ? value : value.Substring(0, 3);
        }
    }

    internal sealed class ShulkerPreviewOverlayForm : BaseOverlayForm
    {
        public ShulkerPreviewOverlayForm(string moduleId)
            : base(moduleId, 384, 232)
        {
            Timer.Interval = 80;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool shift = InputTracker.IsDown(Keys.ShiftKey) || InputTracker.IsDown(Keys.LShiftKey) || InputTracker.IsDown(Keys.RShiftKey);
            bool alt = InputTracker.IsDown(Keys.Menu) || InputTracker.IsDown(Keys.LMenu) || InputTracker.IsDown(Keys.RMenu);

            if (!shift)
            {
                DrawOverlayBack(e.Graphics, new Rectangle(0, 0, S(244), S(44)));
                using (Font font = new Font("Segoe UI Semibold", SF(9f), FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, "Hold LShift over shulker", font, new Rectangle(S(12), S(10), S(220), S(24)), Color.White, TextFormatFlags.Left);
                base.OnPaint(e);
                return;
            }

            DrawOverlayBack(e.Graphics, ClientRectangle);
            string title = alt ? "Shulker Contents" : "Shulker Counts";
            using (Font titleFont = new Font("Segoe UI Semibold", SF(13f), FontStyle.Bold))
            using (Font noteFont = new Font("Segoe UI", SF(8.5f), FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, title, titleFont, new Rectangle(S(14), S(10), S(250), S(28)), Color.White, TextFormatFlags.Left);
                DrawGrid(e.Graphics, alt);
                string bridge = BridgeManager.CurrentData.ShulkerPreview;
                string note = bridge.Length > 0 ? bridge : "External mode: Bedrock item NBT is unavailable without bridge/mock data.";
                TextRenderer.DrawText(e.Graphics, note, noteFont, new Rectangle(S(14), S(194), S(354), S(26)), Color.FromArgb(255, 179, 70), TextFormatFlags.Left);
            }
            base.OnPaint(e);
        }

        private void DrawGrid(Graphics g, bool full)
        {
            Color boxColor = full ? Color.FromArgb(210, 28, 208, 217) : Color.FromArgb(210, 126, 40, 210);
            using (SolidBrush outer = new SolidBrush(boxColor))
                g.FillRectangle(outer, new Rectangle(S(16), S(44), S(352), S(132)));

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    Rectangle cell = new Rectangle(S(24 + (col * 38)), S(54 + (row * 38)), S(34), S(34));
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(42, 22, 48)))
                    using (Pen pen = new Pen(Color.FromArgb(15, 15, 20)))
                    {
                        g.FillRectangle(brush, cell);
                        g.DrawRectangle(pen, cell);
                    }
                    if (full && (row + col) % 2 == 0)
                    {
                        using (Font font = new Font("Segoe UI Semibold", SF(8f), FontStyle.Bold))
                            TextRenderer.DrawText(g, "64", font, cell, Color.White, TextFormatFlags.Right | TextFormatFlags.Bottom);
                    }
                }
            }
        }
    }

    internal static class InputTracker
    {
        private static Timer pollTimer;
        private static readonly bool[] keyDown = new bool[256];
        private static readonly Queue<DateTime> leftClicks = new Queue<DateTime>();
        private static readonly Queue<DateTime> rightClicks = new Queue<DateTime>();
        private static readonly object sync = new object();
        private static DateTime lastLeftClick = DateTime.MinValue;
        private static int comboCount;
        private static bool previousLeftDown;
        private static bool previousRightDown;
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
            if (pollTimer != null)
                return;

            pollTimer = new Timer();
            pollTimer.Interval = 16;
            pollTimer.Tick += delegate { PollForegroundInput(); };
            pollTimer.Start();
        }

        public static void Uninstall()
        {
            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
                pollTimer = null;
            }

            lock (sync)
            {
                Array.Clear(keyDown, 0, keyDown.Length);
                LeftDown = false;
                RightDown = false;
                previousLeftDown = false;
                previousRightDown = false;
            }
        }

        public static bool IsDown(Keys key)
        {
            int index = (int)key;
            if (index < 0 || index >= keyDown.Length)
                return false;
            return keyDown[index];
        }

        private static void PollForegroundInput()
        {
            bool active = MinecraftInfo.IsMinecraftForeground();
            DateTime now = DateTime.UtcNow;

            lock (sync)
            {
                if (!active)
                {
                    Array.Clear(keyDown, 0, keyDown.Length);
                    LeftDown = false;
                    RightDown = false;
                    previousLeftDown = false;
                    previousRightDown = false;
                    Trim(leftClicks);
                    Trim(rightClicks);
                    return;
                }

                for (int i = 0; i < keyDown.Length; i++)
                    keyDown[i] = IsAsyncKeyDown(i);

                keyDown[(int)Keys.ShiftKey] = keyDown[(int)Keys.LShiftKey] || keyDown[(int)Keys.RShiftKey] || keyDown[(int)Keys.ShiftKey];
                keyDown[(int)Keys.Menu] = keyDown[(int)Keys.LMenu] || keyDown[(int)Keys.RMenu] || keyDown[(int)Keys.Menu];

                LeftDown = IsAsyncKeyDown((int)Keys.LButton);
                RightDown = IsAsyncKeyDown((int)Keys.RButton);

                if (LeftDown && !previousLeftDown)
                {
                    leftClicks.Enqueue(now);
                    if ((now - lastLeftClick).TotalMilliseconds > 1400)
                        comboCount = 0;
                    comboCount++;
                    lastLeftClick = now;
                }

                if (RightDown && !previousRightDown)
                    rightClicks.Enqueue(now);

                previousLeftDown = LeftDown;
                previousRightDown = RightDown;
                Trim(leftClicks);
                Trim(rightClicks);
            }
        }

        private static bool IsAsyncKeyDown(int virtualKey)
        {
            return (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
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
            Directory.CreateDirectory(Path.Combine(working, "textures", "items"));
            Directory.CreateDirectory(Path.Combine(working, "textures", "misc"));
            Directory.CreateDirectory(Path.Combine(working, "textures", "particle"));
            Directory.CreateDirectory(Path.Combine(working, "particles"));

            File.WriteAllText(Path.Combine(working, "manifest.json"), BuildManifest(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(working, "textures", "item_texture.json"), BuildItemTextureJson(), Encoding.UTF8);
            SavePackIcon(Path.Combine(working, "pack_icon.png"));

            if (settings.VisualLowFire)
                WriteLowFireTextures(working);
            if (settings.VisualNoBobber)
                WriteTransparentTextures(working, new string[]
                {
                    Path.Combine("textures", "entity", "fishing_hook.png"),
                    Path.Combine("textures", "entity", "fishing_hook_marker.png")
                }, 32, 32);
            if (settings.VisualLowShield)
                WriteLowShieldTextures(working, settings.VisualShieldSize);
            if (settings.VisualSmallTotem)
                WriteSmallTotemTexture(working, settings.VisualTotemSize);
            if (settings.VisualSmallTotemPop)
                WriteSmallTotemPop(working, settings.VisualTotemPopSize);
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

        private static string BuildItemTextureJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"resource_pack_name\": \"bomb_client_pvp_pack\",");
            sb.AppendLine("  \"texture_name\": \"atlas.items\",");
            sb.AppendLine("  \"texture_data\": {");
            sb.AppendLine("    \"totem\": { \"textures\": \"textures/items/totem\" },");
            sb.AppendLine("    \"totem_of_undying\": { \"textures\": \"textures/items/totem\" },");
            sb.AppendLine("    \"shield\": { \"textures\": \"textures/items/shield\" }");
            sb.AppendLine("  }");
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

        private static void WriteLowShieldTextures(string working, int sizePercent)
        {
            using (Bitmap shield = CreateShieldTexture(sizePercent))
            {
                shield.Save(Path.Combine(working, "textures", "items", "shield.png"), System.Drawing.Imaging.ImageFormat.Png);
                shield.Save(Path.Combine(working, "textures", "entity", "shield.png"), System.Drawing.Imaging.ImageFormat.Png);
                shield.Save(Path.Combine(working, "textures", "entity", "shield_base_nopattern.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static Bitmap CreateShieldTexture(int sizePercent)
        {
            int canvas = 64;
            Bitmap bmp = new Bitmap(canvas, canvas);
            int shieldW = Math.Max(12, canvas * Math.Max(20, Math.Min(100, sizePercent)) / 100);
            int shieldH = Math.Max(16, (int)(shieldW * 1.18));
            int x = (canvas - shieldW) / 2;
            int y = Math.Min(canvas - shieldH - 1, canvas - shieldH / 2 - 4);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.None;
                Point[] shape = new Point[]
                {
                    new Point(x + shieldW / 2, y),
                    new Point(x + shieldW - 2, y + shieldH / 5),
                    new Point(x + shieldW - 6, y + shieldH - 8),
                    new Point(x + shieldW / 2, y + shieldH - 1),
                    new Point(x + 5, y + shieldH - 8),
                    new Point(x + 2, y + shieldH / 5)
                };
                using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 92, 62, 42)))
                    g.FillPolygon(b, shape);
                using (Pen edge = new Pen(Color.FromArgb(255, 42, 31, 24), Math.Max(1, shieldW / 14)))
                    g.DrawPolygon(edge, shape);
                using (SolidBrush stripe = new SolidBrush(Color.FromArgb(230, 190, 160, 108)))
                    g.FillRectangle(stripe, x + shieldW / 2 - Math.Max(1, shieldW / 12), y + shieldH / 6, Math.Max(2, shieldW / 6), shieldH - shieldH / 4);
            }
            return bmp;
        }

        private static void WriteSmallTotemTexture(string working, int sizePercent)
        {
            using (Bitmap totem = CreateTotemTexture(sizePercent, false))
                totem.Save(Path.Combine(working, "textures", "items", "totem.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        private static void WriteSmallTotemPop(string working, int sizePercent)
        {
            using (Bitmap particle = CreateTotemTexture(sizePercent, true))
            {
                particle.Save(Path.Combine(working, "textures", "particle", "totem_particle.png"), System.Drawing.Imaging.ImageFormat.Png);
                particle.Save(Path.Combine(working, "textures", "particle", "totem.png"), System.Drawing.Imaging.ImageFormat.Png);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"format_version\": \"1.10.0\",");
            sb.AppendLine("  \"particle_effect\": {");
            sb.AppendLine("    \"description\": {");
            sb.AppendLine("      \"identifier\": \"minecraft:totem_particle\",");
            sb.AppendLine("      \"basic_render_parameters\": {");
            sb.AppendLine("        \"material\": \"particles_alpha\",");
            sb.AppendLine("        \"texture\": \"textures/particle/totem_particle\"");
            sb.AppendLine("      }");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(working, "particles", "totem_particle.json"), sb.ToString(), Encoding.UTF8);
        }

        private static Bitmap CreateTotemTexture(int sizePercent, bool pop)
        {
            int canvas = pop ? 128 : 64;
            Bitmap bmp = new Bitmap(canvas, canvas);
            int size = Math.Max(canvas / 5, canvas * Math.Max(20, Math.Min(100, sizePercent)) / 100);
            int x = (canvas - size) / 2;
            int y = (canvas - size) / 2 + (pop ? canvas / 8 : 0);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.None;
                Color gold = Color.FromArgb(245, 226, 83);
                Color green = Color.FromArgb(74, 190, 111);
                Color dark = Color.FromArgb(84, 110, 46);
                using (SolidBrush b = new SolidBrush(gold))
                {
                    g.FillRectangle(b, x + size / 3, y, size / 3, size / 4);
                    g.FillRectangle(b, x + size / 4, y + size / 4, size / 2, size / 3);
                    g.FillRectangle(b, x + size / 3, y + size / 2, size / 3, size / 2);
                }
                using (SolidBrush b = new SolidBrush(green))
                {
                    g.FillRectangle(b, x + size / 6, y + size / 4, size / 5, size / 3);
                    g.FillRectangle(b, x + size - size / 3, y + size / 4, size / 5, size / 3);
                    g.FillRectangle(b, x + size / 3, y + size - size / 6, size / 7, size / 6);
                    g.FillRectangle(b, x + size / 2, y + size - size / 6, size / 7, size / 6);
                }
                using (SolidBrush b = new SolidBrush(dark))
                {
                    g.FillRectangle(b, x + size / 3, y + size / 9, size / 12, size / 12);
                    g.FillRectangle(b, x + size / 2 + size / 12, y + size / 9, size / 12, size / 12);
                }
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
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int CURSOR_SHOWING = 0x00000001;
        public const int WM_NCLBUTTONDOWN = 0x00A1;
        public const int HTCAPTION = 0x0002;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

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

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

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
