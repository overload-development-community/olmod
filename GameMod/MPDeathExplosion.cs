using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(PlayerShip), "DyingUpdate")]
    class MPDeathExplosion_PlayerShip_DyingUpdate
    {
        static void CreateExitMovingExplosion(Vector3 vector)
        {
            if (!GameplayManager.IsMultiplayer || !Menus.mms_reduced_ship_explosions)
            {
                ExplosionManager.CreateExitMovingExplosion(vector);
            }
        }

        static void StartAndEmitParticleInstant(ParticleSubManager psm, int pp_idx, Vector3 pos, Quaternion rot, int emit_count)
        {
            if (!GameplayManager.IsMultiplayer || !Menus.mms_reduced_ship_explosions)
            {
                psm.StartAndEmitParticleInstant(pp_idx, pos, rot, emit_count);
            }
        }

        static void CreateExpElementFromResourcesAndEmit(FXExpElement exp_element, Transform transform, int emit_count, bool all = false)
        {
            if (!GameplayManager.IsMultiplayer || !Menus.mms_reduced_ship_explosions)
            {
                ExplosionManager.CreateExpElementFromResourcesAndEmit(exp_element, transform, emit_count, all);
            }
        }

        static void CreateMinorExplosion(Vector3 vector)
        {
            if (!GameplayManager.IsMultiplayer || !Menus.mms_reduced_ship_explosions)
            {
                ExplosionManager.CreateMinorExplosion(vector);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(ExplosionManager), "CreateMinorExplosion", new Type[] { typeof(Vector3) }))
                    code.operand = AccessTools.Method(typeof(MPDeathExplosion_PlayerShip_DyingUpdate), "CreateMinorExplosion");

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(ExplosionManager), "CreateExitMovingExplosion"))
                    code.operand = AccessTools.Method(typeof(MPDeathExplosion_PlayerShip_DyingUpdate), "CreateExitMovingExplosion");

                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(ParticleSubManager), "StartAndEmitParticleInstant"))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MPDeathExplosion_PlayerShip_DyingUpdate), "StartAndEmitParticleInstant");
                }

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(ExplosionManager), "CreateExpElementFromResourcesAndEmit", new Type[] { typeof(FXExpElement), typeof(Transform), typeof(int), typeof(bool) }))
                    code.operand = AccessTools.Method(typeof(MPDeathExplosion_PlayerShip_DyingUpdate), "CreateExpElementFromResourcesAndEmit");

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "StartDying")]
    class MPDeathExplosion_PlayerShip_StartDying
    {
        static ParticleElement CreateRandomExplosionSimple(Transform transform, float scale = 1f, float sim_speed = 1f)
        {
            if (!GameplayManager.IsMultiplayer || !Menus.mms_reduced_ship_explosions)
            {
                return ExplosionManager.CreateRandomExplosionSimple(transform, scale, sim_speed);
            }

            return null;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(ParticleSubManager), "StartAndEmitParticleInstant"))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(MPDeathExplosion_PlayerShip_DyingUpdate), "StartAndEmitParticleInstant");
                }

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(ExplosionManager), "CreateRandomExplosionSimple"))
                    code.operand = AccessTools.Method(typeof(MPDeathExplosion_PlayerShip_StartDying), "CreateRandomExplosionSimple");

                yield return code;
            }
        }
    }
}
