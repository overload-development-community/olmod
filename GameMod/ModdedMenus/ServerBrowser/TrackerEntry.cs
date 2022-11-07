using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameMod.Metadata;

namespace GameMod.ModdedMenus.ServerBrowser {
    [Mod(Mods.ServerBrowser)]
    public class TrackerEntry {
        public TrackerServer server { get; set; }
        public TrackerGame game { get; set; }

    }
}
