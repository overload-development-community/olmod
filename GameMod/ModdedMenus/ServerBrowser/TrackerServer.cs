using GameMod.Metadata;

namespace GameMod.ModdedMenus.ServerBrowser {
    [Mod(Mods.ServerBrowser)]
    public class TrackerServer {
        public string ip { get; set; }
        public int port { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string serverNotes { get; set; }
        public bool online { get; set; }
    }
}
