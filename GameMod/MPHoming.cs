using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

// original implementation by terminal

namespace GameMod
{
    static class MPHoming
    {
        public static bool UseNonFixedHoming(Projectile proj)
        {
            //return (proj.m_type == ProjPrefab.missile_hunter || proj.m_type == ProjPrefab.missile_pod || proj.m_type == ProjPrefab.missile_smart_mini) && !GameplayManager.IsDedicatedServer() && GameplayManager.IsMultiplayer && MenuManager.m_mp_lan_match;
            Weapon weapon = MPWeapons.WeaponLookup[(int)proj.m_type];
            return weapon != null && !weapon.MineHoming && !GameplayManager.IsDedicatedServer() && GameplayManager.IsMultiplayer && MenuManager.m_mp_lan_match;
        }
    }

    [HarmonyPatch(typeof(Projectile), "SteerTowardsTarget")]
    class MPHomingSteer
    {
        public static float MaxDegrees(Projectile proj, float m_homing_cur_strength)
        {
            return MPHoming.UseNonFixedHoming(proj) ?
                m_homing_cur_strength * RUtility.FRAMETIME_GAME / RUtility.FRAMETIME_FIXED * 1.8f + 0.08f :
                m_homing_cur_strength * RUtility.FIXED_FT_RATIO;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = init, 1 = after first cur_strength, 2 = after second cur_strength, 3 = after mul
            foreach (var c in codes)
                if (state < 2 && c.opcode == OpCodes.Ldfld && ((FieldInfo)c.operand).Name == "m_homing_cur_strength" && ++state == 2) {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return c; // load m_homing_cur_strength
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPHomingSteer), "MaxDegrees")); // this already on stack
                } else if (state == 2) { // skip until Mul after second m_homing_cur_strength
                    if (c.opcode == OpCodes.Mul)
                        state = 3;
                } else {
                    yield return c;
                }
        }
    }

    [HarmonyPatch(typeof(Projectile), "FixedUpdateDynamic")]
    class MPHomingFixed
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = init, 1 = after first m_acceleration, 2 = after first m_type
            Label l = new Label();
            CodeInstruction last = null;
            foreach (var c in codes)
            {
                if (state == 0 && c.opcode == OpCodes.Ldfld && ((FieldInfo)c.operand).Name == "m_acceleration")
                {
                    // this is still before the Ldarg_0 of m_acceleration since the Ldarg_0 is in last
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPHoming), "UseNonFixedHoming"));
                    yield return new CodeInstruction(OpCodes.Brtrue, l);
                    state = 1;
                }
                else if (state == 1 && c.opcode == OpCodes.Ldfld && ((FieldInfo)c.operand).Name == "m_type")
                {
                    last.labels.Add(l); // add label to Ldarg_0 before Ldfld
                    state = 2;
                }
                if (last != null)
                    yield return last;
                last = c;
            }
            if (last != null)
                yield return last;
        }
    }

    [HarmonyPatch(typeof(Projectile), "UpdateDynamic")]
    class MPHomingNonFixed
    {
        static void Prefix(Projectile __instance, ref Transform ___m_cur_target, ref float ___m_homing_cur_strength,
            ref float ___m_target_timer, ref Robot ___m_cur_target_robot, ref Player ___m_cur_target_player)
        {
            var proj = __instance;
            if (!MPHoming.UseNonFixedHoming(proj))
                return;
            var frametime_GAME = RUtility.FRAMETIME_GAME;
            if (proj.m_acceleration != 0f)
            {
                Vector3 b = proj.c_transform.forward * (proj.m_acceleration * frametime_GAME);
                proj.c_rigidbody.velocity += b;
            }
            if (proj.m_homing_strength > 0f)
            {
                if (___m_cur_target == null)
                {
                    ___m_homing_cur_strength = 0f;
                    if (proj.m_homing_strength > 0f && ___m_target_timer <= 0f)
                    {
                        proj.FindATarget();
                        ___m_target_timer = 0.05f;
                    }
                }
                else
                {
                    ___m_homing_cur_strength = Mathf.Min(___m_homing_cur_strength + frametime_GAME * proj.m_homing_acquire_speed, proj.m_homing_strength);
                    if (proj.TargetAlive())
                    {
                        if (proj.m_homing_min_dot < -0.9f)
                        {
                            proj.MoveTowardsTarget();
                        }
                        else
                        {
                            proj.SteerTowardsTarget();
                        }
                        if (___m_target_timer <= 0f)
                        {
                            proj.FindATarget();
                            ___m_target_timer = 0.15f;
                        }
                    }
                    else
                    {
                        ___m_cur_target = null;
                        ___m_cur_target_robot = null;
                        ___m_cur_target_player = null;
                        ___m_homing_cur_strength = 0f;
                    }
                }
            }
        }
    }
}
