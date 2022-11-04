using System;
using GameMod.Metadata;

namespace GameMod.Objects {
    [Mod(Mods.SuddenDeath)]
    public static class SuddenDeath {
        public static bool SuddenDeathMenuEnabled = false;
        public static bool SuddenDeathMatchEnabled = false;
        public static bool InOvertime = false;

        public static int GetTimer() {
            if (NetworkMatch.m_match_time_limit_seconds == int.MaxValue || !SuddenDeathMatchEnabled) {
                return NetworkMatch.m_match_time_remaining;
            }

            return Math.Abs(NetworkMatch.m_match_time_limit_seconds - (int)NetworkMatch.m_match_elapsed_seconds);
        }
    }
}
