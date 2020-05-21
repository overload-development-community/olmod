using Harmony;
using Overload;

namespace GameMod
{
    [HarmonyPatch(typeof(NetworkSpawnPlayer), "Respawn")]
    class MPSpawnBoostClear
    {
        static void Prefix(PlayerShip player_ship)
        {
            player_ship.m_boost_heat = 0;
        }
    }
}
