using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    //Prevent RobotManager from removing triggers in multiplayer
    [HarmonyPatch(typeof(RobotManager), "TriggerInRelevantSegment")]
    internal class MPTriggerVisible
    {
        private static void Postfix(ref bool __result)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                __result = true;
            }
        }
    }
}
