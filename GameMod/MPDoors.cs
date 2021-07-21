using HarmonyLib;
using Overload;
using UnityEngine;

// by terminal
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

    [HarmonyPatch(typeof(DoorAnimating), "Start")]
    internal class MPTouchDoors
    {
        //Removes the proximity sensor which is replaced by a collision in MPShootDoors
        private static void Postfix(GameObject __instance, GameObject ___m_player_trigger)
        {
            if (GameplayManager.IsMultiplayerActive && __instance != null && ___m_player_trigger.GetComponentInChildren<BoxCollider>() != null &&
                NetworkMatch.m_client_server_location != "OLMOD 0.2.5") // keep old behaviour on current server for now
            {
                ___m_player_trigger.GetComponentInChildren<BoxCollider>().size = default(Vector3);
            }
        }
    }

    //Prevent RobotManager from removing doors in multiplayer
    [HarmonyPatch(typeof(RobotManager), "DoorInRelevantSegment")]
    internal class MPDoorVisible
    {
        private static void Postfix(DoorBase door, ref bool __result)
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                __result = true;
            }
        }
    }
}
