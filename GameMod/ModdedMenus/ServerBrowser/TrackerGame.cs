using System;
using GameMod.Metadata;

namespace GameMod.ModdedMenus.ServerBrowser {
    [Mod(Mods.ServerBrowser)]
    public class TrackerGame {
        public DateTime gameStarted { get; set; }
        public int currentPlayers { get; set; }
        public int maxPlayers { get; set; }
        public int matchLength { get; set; }
        public string mapName { get; set; }
        public string mode { get; set; }
        public bool jip { get; set; }
        public bool hasPassword { get; set; }
        public string matchNotes { get; set; }
        public bool inLobby { get; set; }
    }
}
