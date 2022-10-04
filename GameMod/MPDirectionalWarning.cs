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
        public static Vector3 homingDir = Vector3.zero;

        public static void PlayCueWarning(SFXCue sfx_type, float vol_mod = 1f, float pitch_mod = 0f, float delay = 0f, bool reverb = false)
        {
            //Debug.Log("CCC playing homing cue at position " + homingDir + ", ship position is " + GameManager.m_player_ship.transform.localPosition);
            SFXCueManager.PlayCuePos(sfx_type, homingDir, vol_mod, pitch_mod, false, delay, 1f);
        }
    }

    [HarmonyPatch(typeof(Projectile), "SteerTowardsTarget")]
    internal class MPDirectionalWarning_Projectile_SteerTowardsTarget
    {
        static void Postfix(Player ___m_cur_target_player, Transform ___c_transform)
        {

            if (___m_cur_target_player != null && ___m_cur_target_player.isLocalPlayer)
            {
                MPDirectionalWarning.homingDir = Vector3.MoveTowards(___m_cur_target_player.c_player_ship.transform.localPosition, ___c_transform.localPosition, 0.7f);
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "MoveTowardsTarget")]
    internal class MPDirectionalWarning_Projectile_MoveTowardsTarget
    {
        static void Postfix(Player ___m_cur_target_player, Transform ___c_transform)
        {
            if (___m_cur_target_player != null && ___m_cur_target_player.isLocalPlayer)
            {
                MPDirectionalWarning.homingDir = Vector3.MoveTowards(___m_cur_target_player.c_player_ship.transform.localPosition, ___c_transform.localPosition, 0.7f);
            }
        }
    }

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