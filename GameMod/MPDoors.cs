using Harmony;
using Overload;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(RUtility), "CanObjectOpenDoor")]
    internal class MPShootDoors
    {
        // In Multiplayer, players now open doors by shooting them or touching them.
        private static void Postfix(GameObject go, ref bool __result)
        {
            if (go != null && (go.layer == 13 || go.layer == 9 || go.layer == 31) && GameplayManager.IsMultiplayerActive)
                __result = true;
        }
    }
}
