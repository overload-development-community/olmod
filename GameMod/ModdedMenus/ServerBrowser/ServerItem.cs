using System;
using System.Net;
using GameMod.Metadata;
using static GameMod.ModdedMenus.ServerBrowser.ServerBrowser;

namespace GameMod.ModdedMenus.ServerBrowser {
    [Mod(Mods.ServerBrowser)]
    public class ServerItem {
        public string ip { get; set; }
        public int port { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string serverNotes { get; set; }
        public bool online { get; set; }
        public DateTime? gameStarted { get; set; }
        public int currentPlayers { get; set; }
        public int maxPlayers { get; set; }
        public int matchLength { get; set; }
        public string mapName { get; set; }
        public string mode { get; set; }
        public bool jip { get; set; }
        public bool hasPassword { get; set; }
        public int ping { get; set; }
        public IPAddress addr { get; set; }
        public DateTime lastPingRequest { get; set; }
        public string matchNotes { get; set; }
        public bool inLobby { get; set; }
        public BrowserItemStatus status { get; set; }

        public string statusText {
            get {
                if (!online)
                    return "OFFLINE";

                switch (status) {
                    case BrowserItemStatus.INLOBBY:
                        return $"IN LOBBY ({currentPlayers}/{maxPlayers})";
                    case BrowserItemStatus.PLAYING:
                        return $"IN MATCH ({currentPlayers}/{maxPlayers})";
                    case BrowserItemStatus.READY:
                        return "READY";
                    default:
                        return "ERROR";
                }
            }
        }

        public string actionText {
            get {
                if (online && status == BrowserItemStatus.READY) {
                    return "CREATE";
                } else if (online && status == BrowserItemStatus.PLAYING && !jip) {
                    return "CLOSED";
                } else if (online && currentPlayers == maxPlayers) {
                    return "FULL";
                } else if (online && (status == BrowserItemStatus.INLOBBY || status == BrowserItemStatus.PLAYING) && currentPlayers < maxPlayers) {
                    if (hasPassword) {
                        return "JOIN (PW)";
                    } else {
                        return "JOIN";
                    }
                }

                return "ERROR";
            }
        }
    }
}
