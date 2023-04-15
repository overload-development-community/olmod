using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    public static class MPWeapons
    {
        [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
        internal class MPWeapons_PlayerShip_MaybeFireWeapon
        {
            static bool Prefix(PlayerShip __instance)
            {
                Player player = __instance.c_player;

                if (!(__instance.m_refire_time <= 0f) || __instance.c_player.m_spectator)
                {
                    return false;
                }

                Ship ship = MPShips.GetShip(__instance);

                bool flag = false;
                if (!CanFireWeaponAmmo(ship, player))
                {
                    if ((float)player.m_energy <= 0f)
                    {
                        if ((int)player.m_ammo <= 0)
                        {
                            if (WeaponUsesAmmo(ship, player.m_weapon_type))
                            {
                                SwitchToEnergyWeapon(ship, player);
                            }
                            flag = true;
                        }
                        else if (!SwitchToAmmoWeapon(ship, player))
                        {
                            flag = true;
                        }
                    }
                    else
                    {
                        SwitchToEnergyWeapon(ship, player);
                    }
                    if (!flag)
                    {
                        __instance.m_refire_time = 0.5f;
                        return false;
                    }
                }
                if (GameplayManager.IsMultiplayerActive && player.m_spawn_invul_active)
                {
                    player.m_timer_invuln = (float)player.m_timer_invuln - (float)NetworkMatch.m_respawn_shield_seconds;
                }
                __instance.m_alternating_fire = !__instance.m_alternating_fire;
                float refire_multiplier = ((!flag) ? 1f : 3f);
                __instance.FiringVolumeModifier = 1f;
                __instance.FiringPitchModifier = 0f;

                ship.primaries[(int)player.m_weapon_type].Fire(player, refire_multiplier);

                /*
                Client.GetClient().Send(MessageTypes.MsgSniperPacket, new SniperPacketMessage
                {
                    m_player_id = player.netId,
                    m_type = type,
                    m_pos = player.pos,
                    m_rot = player.rot,
                    m_strength = strength,
                    m_upgrade_lvl = upgrade_lvl,
                    m_no_sound = no_sound,
                    m_slot = slot,
                    m_force_id = force_id
                });
                */

                if (__instance.m_refire_time < 0.01f)
                {
                    __instance.m_refire_time = 0.01f;
                }

                return false;
            }
        }


        // references in OLmod all changed - but WeaponUsesAmmo2 needs addressing
        // TODO - references in Overload
        public static bool WeaponUsesAmmo(Ship s, WeaponType wt = WeaponType.NUM)
        {
            if (wt == WeaponType.NUM)
            {
                wt = s.player.m_weapon_type;
            }
            return s.primaries[(int)wt].UsesAmmo;
        }

        // TODO - references in Overload
        public static bool WeaponUsesEnergy(Ship s, WeaponType wt = WeaponType.NUM)
        {
            if (wt == WeaponType.NUM)
            {
                wt = s.player.m_weapon_type;
            }
            return s.primaries[(int)wt].UsesEnergy;
        }

        // TODO - references in Overload
        public static bool CanFireWeaponAmmo(Ship s, Player p)
        {
            if (p.m_overdrive || Player.CheatUnlimited)
            {
                return true;
            }
            if (s.primaries[(int)p.m_weapon_type].UsesEnergy && p.m_energy > 0f)
            {
                return true;
            }
            else if (s.primaries[(int)p.m_weapon_type].UsesAmmo && p.m_ammo > 0f)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // TODO - bring in changes from MPAutoSelection
        // - bring in changes from MPSniperPackets
        // - bring in changes from MPWeaponCycling
        // - references in Overload
        public static bool SwitchToAmmoWeapon(Ship s, Player p)
        {
            bool res = false; 
            MPWeaponCycling.PBypass = true;
            
            for (int i = 0; i < s.primaries.Length; i++)
            {
                if (s.primaries[i].UsesAmmo)
                {
                    p.Networkm_weapon_type = (WeaponType)i;
                    p.NextWeapon();
                    res = true;
                    break;
                }
            }

            MPWeaponCycling.PBypass = false;
            return res;
        }

        // TODO - bring in changes from MPAutoSelection
        // - bring in changes from MPSniperPackets
        // - bring in changes from MPWeaponCycling
        // - references in Overload
        public static void SwitchToEnergyWeapon(Ship s, Player p)
        {
            /*if (MenuManager.opt_primary_autoswitch == 0 && MPAutoSelection.primarySwapFlag)
            {
                if (p == GameManager.m_local_player)
                {

                    MPAutoSelection.maybeSwapPrimary();
                    if (MPAutoSelection.swap_failed)
                    {
                        uConsole.Log("-AUTOSELECT- [EB] swap failed on trying to switch to an energy weapon");
                        MPAutoSelection.swap_failed = false;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }*/

            MPWeaponCycling.PBypass = true;

            for (int i = s.primaries.Length - 1; i >= 0; i--)
            {
                if (s.primaries[i].UsesEnergy)
                {
                    p.Networkm_weapon_type = (WeaponType)i;
                }
                p.NextWeapon();
                break;
            }

            MPWeaponCycling.PBypass = false;
        }

        // MPWeaponCycling - 1 reference to change
        // TODO - Overload references
        public static bool OnlyAmmoWeapons(Ship s, Player p)
        {
            bool res = true;
            for (int i = 0; i < s.primaries.Length; i++)
            {
                if (s.primaries[i].UsesEnergy && p.m_weapon_level[i] != WeaponUnlock.LOCKED)
                {
                    res = false;
                    break;
                }
            }
            return res;
        }

        // ================================================================
        // TEMP STUFF
        // ================================================================

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


    // ====================================================================
    //
    //
    // ====================================================================
    // Weapon Template
    // ====================================================================
    //
    //
    // ====================================================================


    public abstract class Weapon
    {
        public string displayName;

        protected Ship ship;

        public bool UsesAmmo;
        public bool UsesEnergy;


        protected FieldInfo c_right_Field = AccessTools.Field(typeof(PlayerShip), "c_right");
        protected FieldInfo c_up_Field = AccessTools.Field(typeof(PlayerShip), "c_up");

        //temporary
        public abstract void Fire(Player player, float refire_multiplier);
        //public abstract void ServerFire(Player player, float refire_multiplier);


        //public abstract void FirePressed();

        //public abstract void FireReleased();

        protected Quaternion AngleRandomize(Quaternion rot, float angle, Vector3 c_up, Vector3 c_right)
        {
            Vector2 insideUnitCircle = UnityEngine.Random.insideUnitCircle;
            return AngleSpreadY(AngleSpreadX(rot, angle * insideUnitCircle.x, c_up), angle * insideUnitCircle.y, c_right);
        }

        protected Quaternion AngleSpreadX(Quaternion rot, float angle, Vector3 c_up)
        {
            Quaternion quaternion = Quaternion.AngleAxis(angle, c_up);
            return quaternion * rot;
        }

        protected Quaternion AngleSpreadY(Quaternion rot, float angle, Vector3 c_right)
        {
            Quaternion quaternion = Quaternion.AngleAxis(angle, c_right);
            return quaternion * rot;
        }

        protected Quaternion AngleSpreadZ(Quaternion rot, float angle, Vector3 c_forward)
        {
            Quaternion quaternion = Quaternion.AngleAxis(angle, c_forward);
            return quaternion * rot;
        }
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
