using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    public static class MPWeapons
    {
        public static float vol = 1f;
        public static int idx = 1;
        public static float refire = 2.3f;

        public static void AddForceAndTorque(PlayerShip ps, DamageInfo di)
        {
            if (di.weapon != ProjPrefab.proj_beam || MPShips.allowed == 0) // for now
                return;

            Vector3 force = di.push_force * di.push_dir / 5f; // *1000f in the Projectile stuff, gotta undo it somewhat

            ps.c_rigidbody.AddForceAtPosition(force, Vector3.LerpUnclamped(ps.c_transform_position, di.pos, 10f), ForceMode.Impulse);
        }

        public static void LancerMDSound(PlayerShip ps)
        {
            if (MPShips.allowed != 0)
            {
                GameManager.m_audio.PlayCueTransform((int)NewSounds.LancerCharge2s5, ps.c_transform, vol);
            }
        }
    }

    // thus the first block of the foundation is laid -- *this* one is going to take a while.
    public abstract class Weapon
    {
        public float dmg;
        public float minspeed;

        public abstract void FirePressed();

        public abstract void FireReleased();

    }


    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    static class MPWeapons_PlayerShip_ApplyDamage
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {

            foreach (var code in codes)
            {
                yield return code;
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(PlayerShip), "CallRpcApplyDamageEffects"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons), "AddForceAndTorque"));
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPWeapons_PlayerShip_MaybeFireWeapon
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes, ILGenerator gen)
        {
            int found = 0;
            int count = -1;

            Label lbl = gen.DefineLabel();

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 14)
                {
                    yield return code;
                    found++;
                    count = 0;
                }
                else if (found == 1)
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "c_player"));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "m_energy"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CodeStage.AntiCheat.ObscuredTypes.ObscuredFloat), "op_Implicit", new System.Type[] { typeof(CodeStage.AntiCheat.ObscuredTypes.ObscuredFloat) })); 
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    yield return new CodeInstruction(OpCodes.Ble, lbl);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.95f);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PlayerShip), "FiringVolumeModifier"));
                    found++;
                }
                else if (count >= 0 && count < 2 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Player), "PlayCameraShake"))
                {
                    yield return code;
                    count++;
                }
                else if (count == 3) // we're modifying the command immediately following the count == 2 stuff down below
                {
                    code.labels.Add(lbl);
                    yield return code;
                    count++;
                }
                else
                {
                    yield return code;
                }

                if (count == 2) // we're injecting after the second PlayCameraShake
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "c_player"));
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 24f);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Player), "UseEnergy"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "m_refire_time"));
                    //yield return new CodeInstruction(OpCodes.Ldc_R4, 3f);
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeapons), "refire"));
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(PlayerShip), "m_refire_time"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeapons), "LancerMDSound"));
                    count++;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Player), "AddEnergyDefault")]
    public static class MPWeapons_Player_AddEnergyDefault
    {
        public static bool Prefix(Player __instance)
        {
            if (__instance.m_weapon_type == WeaponType.LANCER)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
