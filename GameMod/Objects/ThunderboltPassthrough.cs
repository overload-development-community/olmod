using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameMod.Metadata;

namespace GameMod.Objects {
    [Mod(Mods.ThunderboltPassthrough)]
    public static class ThunderboltPassthrough {
        public static bool Enabled { get; set; } = false;
    }
}
