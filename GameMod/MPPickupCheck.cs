using System;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    class MPPickupCheck
    {
        public static bool PickupCheck = true;
    }

    // Allow picking up items in MP when item is adjacent to an inverted segment. Items also no longer get stuck inside grates. by Terminal
    [HarmonyPatch(typeof(Item), "ItemIsReachable")]
    internal class MPItemInvertedSegment
    {
        private static bool Prefix(ref bool __result)
        {
            //Debug.Log("ItemIsReachable " + (Overload.NetworkManager.IsServer() ? "server" : "conn " + NetworkMatch.m_my_lobby_id) + " PickupCheck=" + MPPickupCheck.PickupCheck);
            if (GameplayManager.IsMultiplayerActive && !MPPickupCheck.PickupCheck)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
