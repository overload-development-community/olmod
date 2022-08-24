using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    // Allow boss2b (unused boss) to trigger exit sequence when destroyed during a boss lockdown.
    [HarmonyPatch(typeof(Robot), "ExplodeNow")]
    public class Robot_ExplodeNow_Boss2B
    {
        static void Postfix(Robot __instance)
        {
            if(__instance.m_is_boss && __instance.robot_type == Overload.EnemyType.BOSS2B)
            {
                Overload.GameplayManager.LockdownBossDestroyed();
            }
        }
    }
}