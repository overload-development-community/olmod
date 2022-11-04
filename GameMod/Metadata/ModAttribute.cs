using System;
using System.Collections.Generic;

namespace GameMod.Metadata {
    internal class ModAttribute : Attribute {
        public List<Mods> Mods { get; set; }
        public Version Version { get; set; }

        public ModAttribute(Mods mod) : this(new Mods[] { mod }, null) { }

        public ModAttribute(Mods mod, Version version) : this(new Mods[] { mod }, version) { }

        public ModAttribute(Mods[] mods) : this(mods, null) { }

        public ModAttribute(Mods[] mods, Version version) {
            Mods = new List<Mods>(mods);
            Version = version;
        }
    }
}
