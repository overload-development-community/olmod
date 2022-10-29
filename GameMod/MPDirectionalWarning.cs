using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

/// <summary>
/// Converts the 2D homing weapon warning cues into 3D ones coming from the actual projectile angle
/// </summary>
namespace GameMod
{
    public static class MPDirectionalWarning
    {
        public static Transform homingPos;

        // calculates direction and plays cues in 3D if it's enabled in the menu setting, otherwise use the 2D cue call
        public static void PlayCueWarning(SFXCue sfx_type, float vol_mod = 1f, float pitch_mod = 0f, float delay = 0f, bool reverb = false)
        {
            if (Menus.mms_directional_warnings)
            {
                Vector3 homingDir = Vector3.MoveTowards(GameManager.m_player_ship.c_transform_position, homingPos.localPosition, 0.7f);
                SFXCueManager.PlayCuePos(sfx_type, homingDir, vol_mod, pitch_mod, false, delay, 1f);
            }
            else
            {
                SFXCueManager.PlayCue2D(sfx_type, vol_mod, pitch_mod, delay, reverb);
            }
        }
    }

    // stores the Transform of the homing projectile in MPDirectionalWarning for use during 3D cue playback
    [HarmonyPatch(typeof(Projectile), "SteerTowardsTarget")]
    internal class MPDirectionalWarning_Projectile_SteerTowardsTarget
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                yield return code;

                if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(Projectile), "PlayerLockOnMinDistanceSq"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), "c_transform"));
                    yield return new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(MPDirectionalWarning), "homingPos"));
                }
            }
        }
    }

    // same as above for creepers
    [HarmonyPatch(typeof(Projectile), "MoveTowardsTarget")]
    internal class MPDirectionalWarning_Projectile_MoveTowardsTarget
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                yield return code;

                if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(Projectile), "PlayerLockOnMinDistanceSq"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Projectile), "c_transform"));
                    yield return new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(MPDirectionalWarning), "homingPos"));
                }
            }
        }
    }

    // swaps out the call for the homing cues to the new method that determines which one should be used
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    internal class MPDirectionalWarning_PlayerShip_Update
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int count = -1;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(Projectile), "PlayerLockOnCreeper"))
                {
                    count = 0;
                    yield return code;
                }
                else if (count >= 0 && count < 2 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(SFXCueManager), "PlayCue2D"))
                {
                    count++;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPDirectionalWarning), "PlayCueWarning"));
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
}