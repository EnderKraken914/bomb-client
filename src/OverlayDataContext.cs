using System;

namespace BombClient
{
    internal sealed class OverlayDataSnapshot
    {
        public string ServerHost = "";
        public int ServerPort;
        public string BridgeStatus = "Offline";
        public BridgeDataSnapshot BridgeData = new BridgeDataSnapshot();
        public bool MinecraftRunning;
        public bool MinecraftForeground;
        public string SystemStatus = "";
        public string MemoryStatus = "";

        public string ServerInfoText()
        {
            string server = ServerHost + ":" + ServerPort.ToString();
            if (BridgeData != null && BridgeData.ArenaName.Length > 0)
                return server + " | " + BridgeData.ArenaName;
            return server;
        }
    }

    internal static class OverlayDataContext
    {
        public static OverlayDataSnapshot Current(AppSettings settings)
        {
            OverlayDataSnapshot snapshot = new OverlayDataSnapshot();
            if (settings != null)
            {
                snapshot.ServerHost = settings.ServerHost;
                snapshot.ServerPort = settings.ServerPort;
            }
            snapshot.BridgeStatus = BridgeManager.Status;
            snapshot.BridgeData = BridgeManager.CurrentData;
            snapshot.MinecraftRunning = MinecraftInfo.IsMinecraftRunning();
            snapshot.MinecraftForeground = MinecraftInfo.IsMinecraftForeground();
            snapshot.SystemStatus = SystemInfo.StatusText();
            snapshot.MemoryStatus = MinecraftInfo.MemoryText();
            return snapshot;
        }
    }
}
