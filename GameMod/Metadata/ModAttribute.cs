using System;
using System.Collections.Generic;
using System.Linq;

namespace GameMod.Metadata {
    internal class ModAttribute : Attribute {
        public List<Mods> Mods { get; set; }
        public Version Version { get; set; }

        public ModAttribute(Mods mod) : this(new Mods[] { mod }, VersionHandling.OlmodVersion.RunningVersion) { }

        public ModAttribute(Mods mod, Version version) : this(new Mods[] { mod }, version) { }

        public ModAttribute(Mods[] mods) : this(mods, VersionHandling.OlmodVersion.RunningVersion) { }

        public ModAttribute(Mods[] mods, Version version) {
            Mods = new List<Mods>(mods);
            Version = version;
        }
    }
}
