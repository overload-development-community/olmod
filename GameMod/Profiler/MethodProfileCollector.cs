﻿using GameMod.Metadata;

namespace GameMod.Profiler {
    [Mod(Mods.Profiler)]
    public class MethodProfileCollector {
        public const int MaxEntryCount = 7500;
        public MethodProfile[] entry;

        public MethodProfileCollector() {
            entry = new MethodProfile[MaxEntryCount + 1];
        }
    }
}
