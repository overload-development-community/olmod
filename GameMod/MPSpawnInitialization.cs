using System.Reflection;
using HarmonyLib;
using Overload;

namespace GameMod
{
    /// <summary>
    /// Does a better job of initializing playership state at spawn, resetting the flak/cyclone fire counter, the thunderbolt power level, and clearing the boost overheat.
    /// </summary>
    [HarmonyPatch(typeof(Player), "RestorePlayerShipDataAfterRespawn")]
    class MPSpawnInitialization
    {
        private static FieldInfo _PlayerShip_flak_fire_count_Field = typeof(PlayerShip).GetField("flak_fire_count", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Prefix(Player __instance) {
            _PlayerShip_flak_fire_count_Field.SetValue(__instance.c_player_ship, 0);
            __instance.c_player_ship.m_thunder_power = 0;
            __instance.c_player_ship.m_boost_heat = 0;
            __instance.c_player_ship.m_boost_overheat_timer = 0f;
        }
    }
}
