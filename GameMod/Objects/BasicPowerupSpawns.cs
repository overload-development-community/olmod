using System.Collections.Generic;
using GameMod.Metadata;

namespace GameMod.Objects {
    [Mod(Mods.BasicPowerupSpawns)]
    public static class BasicPowerupSpawns {
        public struct MultiplayerSpawnablePowerup {
            public int type;
            public float percent;
        }

        public enum PowerupType {
            HEALTH,
            ENERGY,
            AMMO,
            ALIENORB,
            NUM
        }

        public static List<MultiplayerSpawnablePowerup> m_multiplayer_spawnable_powerups = new List<MultiplayerSpawnablePowerup>();
        public static float m_multi_powerup_frequency = 0f;
    }
}
