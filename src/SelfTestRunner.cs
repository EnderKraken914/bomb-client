using System;
using System.Collections.Generic;
using System.IO;

namespace BombClient
{
    internal static class SelfTestRunner
    {
        public static int Run()
        {
            try
            {
                Assert(ClientModuleCatalog.All.Length >= OverlayCatalog.All.Length, "module catalog loads");
                Assert(OverlayCatalog.Find("fps") != null, "FPS overlay is registered");
                Assert(OverlayCatalog.Find("ping") != null, "Ping overlay is registered");
                Assert(OverlayCatalog.Find("keystrokes") != null, "Keystrokes overlay is registered");
                Assert(OverlayCatalog.Find("armor") != null, "Armor HUD overlay is registered");
                Assert(OverlayCatalog.Find("potions") != null, "Potion Effects overlay is registered");
                Assert(OverlayCatalog.Find("hotbar") != null, "Hotbar Preview overlay is registered");
                Assert(OverlayCatalog.Find("shulker") != null, "Shulker Preview overlay is registered");

                AppSettings settings = AppSettings.Load();
                settings.Save();
                Assert(File.Exists(AppPaths.SettingsFile), "settings can save");

                string temp = Path.Combine(Path.GetTempPath(), "BombClientSelfTest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                try
                {
                    string modulePath = Path.Combine(temp, "modules.ini");
                    Dictionary<string, ClientModuleState> moduleStates = ClientModuleConfigStore.Load(modulePath);
                    ClientModuleConfigStore.Save(moduleStates, modulePath);
                    Assert(ClientModuleConfigStore.Load(modulePath).Count > 0, "module config can load/save");

                    string updateJson = "{\"latest_version\":\"2.0.0\",\"required_version\":\"2.0.0\",\"force_update\":true,\"release_date\":\"2026-05-29\",\"release_title\":\"Bomb Client 2.0 Core\",\"changelog\":\"Self-test changelog\",\"github_release_url\":\"https://github.com/EnderKraken914/bomb-client/releases/tag/v2.0.0\",\"download_url\":\"https://github.com/EnderKraken914/bomb-client/releases/download/v2.0.0/BombClient-Windows-2.0.0.zip\"}";
                    UpdateManifest manifest = UpdateChecker.ParseManifest(updateJson);
                    Assert(manifest.LatestVersion == "2.0.0", "update manifest parsing still works");
                    Assert(manifest.Changelog.IndexOf("Self-test", StringComparison.OrdinalIgnoreCase) >= 0, "latest updates changelog data loads");

                    string profilesPath = Path.Combine(temp, "server-profiles.ini");
                    List<ServerProfile> profiles = ServerProfileStore.DefaultProfiles();
                    ServerProfileStore.Save(profiles, profilesPath);
                    Assert(ServerProfileStore.Load(profilesPath).Count > 0, "server profiles can load/save");

                    string packsPath = Path.Combine(temp, "managed-packs.ini");
                    List<ManagedPack> packs = new List<ManagedPack>();
                    packs.Add(new ManagedPack());
                    ManagedPackStore.Save(packs, packsPath);
                    Assert(ManagedPackStore.Load(packsPath).Count == 1, "pack manager can load/save");

                    BridgeManager.StartMock();
                    Assert(BridgeManager.Status == "Connected" && BridgeManager.CurrentData.HasAnyData(), "bridge mock mode can start");
                    BridgeManager.Stop();
                    Assert(BridgeManager.Status == "Offline", "bridge mock mode can stop");
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(temp))
                            Directory.Delete(temp, true);
                    }
                    catch
                    {
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Bomb Client self-test failed: " + ex.Message);
                return 1;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
