using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    // If a cloaking device expires during the exit cutscene,
    // cockpit visibility (which is also used to show the ship in cutscenes)
    // reverts to the profile setting even though we want it forced on.
    [HarmonyPatch(typeof(Overload.PlayerShip), "get_IsCockpitVisible")]
    public class PlayerShip_IsCockpitVisible_DuringExit
    {
        static bool Prefix(ref bool __result)
        {
            if (Overload.GameplayManager.m_gameplay_state == Overload.GameplayState.EXIT)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}