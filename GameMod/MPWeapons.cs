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
        public static PrimaryWeapon[] primaries = new PrimaryWeapon[8]
        {
            new Impulse(),
            new Cyclone(),
            new Reflex(),
            new Crusher(),
            new Driller(),
            new Flak(),
            new Thunderbolt(),
            new Lancer()
        };

        public static SecondaryWeapon[] secondaries = new SecondaryWeapon[8];

        public static bool NeedsUpdate = true;

        // THIS NEEDS FINISHING
        public static bool CycleWeapon(Player p, bool prev = false)
        {
            int curr = (int)p.m_weapon_type;
            int next = curr;

            for (int i = 0; i < 9; i++) // try all 8 slots then give up and go back to the first
            {
                next = (next + ((!prev) ? 1 : 7)) % 8;

                //if (primaries[MPWeaponCycling.pPos[i]])
            }
            next = (next + ((!prev) ? 1 : 7)) % 8;

            return false;
        }

        public static void SetWeapon(Player p, WeaponType wt)
        {
            p.m_weapon_type_prev = p.m_weapon_type;
            p.Networkm_weapon_type = wt;
            p.CallCmdSetCurrentWeapon(p.m_weapon_type);
            p.c_player_ship.WeaponSelectFX();
            p.UpdateCurrentWeaponName();
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
        public string Tag2A;
        public string Tag2B;

        public Ship ship;
        public PlayerShip ps;
        public Player player;

        //protected FieldInfo c_right_Field = AccessTools.Field(typeof(PlayerShip), "c_right");
        //protected FieldInfo c_up_Field = AccessTools.Field(typeof(PlayerShip), "c_up");

        //temporary
        public abstract void Fire(float refire_multiplier);
        //public abstract void ServerFire(Player player, float refire_multiplier);


        //public abstract void FirePressed();

        //public abstract void FireReleased();

        public void SetShip(Ship s)
        {
            ship = s;
            ps = s.ps;
            player = s.player;
        }

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

        // makes a shallow copy of the Weapon object for use with a specific player
        public Weapon Copy()
        {
            return (Weapon)MemberwiseClone();
        }
    }

    public abstract class PrimaryWeapon : Weapon
    {
        public bool UsesAmmo;
        public bool UsesEnergy;
    }

    public abstract class SecondaryWeapon : Weapon
    {
        public string displayNamePlural;
        public int ammo;
        public int ammoUp;
        public int ammoSuper;
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Utility Functions
    // ====================================================================
    //
    //
    // ====================================================================


    // CCF VERIFIED
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPWeapons_PlayerShip_MaybeFireWeapon
    {
        static bool Prefix(PlayerShip __instance, Vector3 ___c_forward, Vector3 ___c_up, Vector3 ___c_right, ref int ___flak_fire_count)
        {
            if (!(__instance.m_refire_time <= 0f) || __instance.c_player.m_spectator)
            {
                return false;
            }

            Player player = __instance.c_player;

            // get the Ship reference and update the replacement fields
            Ship ship = MPShips.GetShip(__instance);
            ship.c_forward = ___c_forward;
            ship.c_up = ___c_up;
            ship.c_right = ___c_right;
            ship.flak_fire_count = ___flak_fire_count;

            bool flag = false;
            if (!player.CanFireWeaponAmmo())
            {
                if ((float)player.m_energy <= 0f)
                {
                    if ((int)player.m_ammo <= 0)
                    {
                        if (player.WeaponUsesAmmo(player.m_weapon_type))
                        {
                            player.SwitchToEnergyWeapon();
                        }
                        flag = true;
                    }
                    else if (!player.SwitchToAmmoWeapon())
                    {
                        flag = true;
                    }
                }
                else
                {
                    player.SwitchToEnergyWeapon();
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

            ship.primaries[(int)player.m_weapon_type].Fire(refire_multiplier);

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

            // update the flak counter since it's used elsewhere
            ___flak_fire_count = ship.flak_fire_count;

            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "WeaponUsesAmmo")]
    static class MPWeapons_Player_WeaponUsesAmmo
    {
        public static bool Prefix(ref bool __result, Player __instance, WeaponType wt)
        {
            if (wt == WeaponType.NUM)
            {
                wt = __instance.m_weapon_type;
            }

            __result = MPWeapons.primaries[(int)wt].UsesAmmo;

            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "WeaponUsesAmmo2")]
    static class MPWeapons_Player_WeaponUsesAmmo2
    {
        public static bool Prefix(ref bool __result, WeaponType wt)
        {
            __result = MPWeapons.primaries[(int)wt].UsesAmmo;

            return false;
        }
    }


    //CCF NEEDS REBUILDING STILL - WILL NEED TO REDO AUTOSELECT TO DO THIS PROPERLY AAAERGFGGGHHH
    /*
    // Rebuilt to handle modular weapons, autoselect ordering, and weapon exclusion
    [HarmonyPatch(typeof(Player), "NextWeapon")]
    static class MPWeapons_Player_NextWeapon
    {
        public static bool Prefix(Player __instance, bool prev = false)
        {
            if (__instance.CanFireWeapon())
            {
            }

            return false;
        }
    }
    */


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "CanFireWeapon")]
    static class MPWeapons_Player_CanFireWeapon
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            if ((int)__instance.m_ammo > 0 && __instance.AnyAmmoWeapons())
            {
                if (__instance.WeaponUsesAmmo(__instance.m_weapon_type))
                {
                    __result = true;
                    return false;
                }
                __result = (float)__instance.m_energy > 0f;
                return false;
            }
            if (__instance.WeaponUsesAmmo(__instance.m_weapon_type))
            {
                __result = false;
                return false;
            }
            __result = true;
            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "CanFireWeaponAmmo")]
    static class MPWeapons_Player_CanFireWeaponAmmo
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            if (__instance.m_overdrive || Player.CheatUnlimited)
            {
                __result = true;
                return false;
            }
            if (MPWeapons.primaries[(int)__instance.m_weapon_type].UsesEnergy && __instance.m_energy > 0f)
            {
                __result = true;
                return false;
            }
            else if (MPWeapons.primaries[(int)__instance.m_weapon_type].UsesAmmo && __instance.m_ammo > 0f)
            {
                __result = true;
                return false;
            }
            else
            {
                __result = false;
                return false;
            }
        }
    }

    // THESE 2 METHODS NEED AN OVERHAUL -- NEXTWEAPON NEEDS REIMPLEMENTING WITH AUTOSELECT STUFF REWRITTEN
    /*
    // TODO - bring in changes from MPAutoSelection
    // - bring in changes from MPSniperPackets
    // - bring in changes from MPWeaponCycling
    // NEEDS VERIFYING AGAIN
    [HarmonyPatch(typeof(Player), "SwitchToAmmoWeapon")]
    static class MPWeapons_Player_SwitchToAmmoWeapon
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            __result = false;
            MPWeaponCycling.PBypass = true;

            for (int i = 0; i < 8; i++)
            {
                if (MPWeapons.primaries[i].UsesAmmo)
                {
                    __instance.Networkm_weapon_type = (WeaponType)i;
                    __instance.NextWeapon();
                    __result = true;
                    break;
                }
            }

            MPWeaponCycling.PBypass = false;

            return false;

            for (int i = 0; i < 8; i++)
            {


            }
        }
    }


    // TODO - bring in changes from MPAutoSelection
    // - bring in changes from MPSniperPackets
    // - bring in changes from MPWeaponCycling
    // NEEDS VERIFYING AGAIN
    [HarmonyPatch(typeof(Player), "SwitchToEnergyWeapon")]
    static class MPWeapons_Player_SwitchToEnergyWeapon
    {
        public static bool Prefix(Player __instance)
        {
            MPWeaponCycling.PBypass = true;

            for (int i = 7; i >= 0; i--)
            {
                if (MPWeapons.primaries[i].UsesEnergy)
                {
                    __instance.Networkm_weapon_type = (WeaponType)i;
                }
                __instance.NextWeapon();
                break;
            }

            MPWeaponCycling.PBypass = false;

            return false;
        }
    }
    */

    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "AnyAmmoWeapons")]
    static class MPWeapons_Player_AnyAmmoWeapons
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            __result = false;
            for (int i = 0; i < 8; i++)
            {
                if (MPWeapons.primaries[i].UsesAmmo && __instance.m_weapon_level[i] != WeaponUnlock.LOCKED)
                {
                    __result = true;
                    break;
                }
            }

            return false;
        }
    }


    // CCF VERIFIED
    [HarmonyPatch(typeof(Player), "OnlyAmmoWeapons")]
    static class MPWeapons_Player_OnlyAmmoWeapons
    {
        public static bool Prefix(ref bool __result, Player __instance)
        {
            __result = true;
            for (int i = 0; i < 8; i++)
            {
                if (MPWeapons.primaries[i].UsesEnergy && __instance.m_weapon_level[i] != WeaponUnlock.LOCKED)
                {
                    __result = false;
                    break;
                }
            }

            return false;
        }
    }


    // hooks in to allow weapons to physically punch ships in multiplayer
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


    // name replacement for primaries
    [HarmonyPatch(typeof(Player.WeaponNamesType), "Refresh")]
    static class MPWeapons_Player_WeaponNamesType_Refresh
    {
        // Postfix instead of Pre(false) to allow the array to be created properly in the original before the values are replaced
        public static void Postfix(ref string[] ___m_values)
        {
            for (int i = 0; i < 8; i++)
            {
                ___m_values[i] = Loc.LS(MPWeapons.primaries[i].displayName);
            }
        }
    }

    /*
    // name replacement for secondaries
    [HarmonyPatch(typeof(Player.MissileNamesType), "Refresh")]
    static class MPWeapons_Player_MissileNamesType_Refresh
    {
        // Postfix instead of Pre(false) to allow the array to be created properly in the original
        public static void Postfix(ref string[] ___m_values)
        {
            for (int i = 0; i < 8; i++)
            {
                ___m_values[i] = Loc.LS(MPWeapons.secondaries[i].displayName);
            }
        }
    }

    // name replacement for secondaries, plural version
    [HarmonyPatch(typeof(Player.MissileNamesPluralType), "Refresh")]
    static class MPWeapons_Player_MissileNamesPluralType_Refresh
    {
        // Postfix instead of Pre(false) to allow the array to be created properly in the original
        public static void Postfix(ref string[] ___m_values)
        {
            for (int i = 0; i < 8; i++)
            {
                ___m_values[i] = Loc.LS(MPWeapons.secondaries[i].displayNamePlural);
            }
        }
    }
    */

    // missile limit and weapon tag changes
    [HarmonyPatch(typeof(Player), MethodType.Constructor)]
    static class MPWeapons_Player_Constructor
    {
        // readonly fields
        static FieldInfo MAX_MISSILE_AMMO = typeof(Player).GetField("MAX_MISSILE_AMMO", BindingFlags.NonPublic | BindingFlags.Static);
        static FieldInfo MAX_MISSILE_AMMO_UP = typeof(Player).GetField("MAX_MISSILE_AMMO_UP", BindingFlags.NonPublic | BindingFlags.Static);
        static FieldInfo SUPER_MISSILE_AMMO_MP = typeof(Player).GetField("SUPER_MISSILE_AMMO_MP", BindingFlags.NonPublic | BindingFlags.Static);

        public static void Postfix()
        {
            if (MPWeapons.NeedsUpdate)
            {
                MPWeapons.NeedsUpdate = false;

                int[] max = new int[8];
                int[] max_up = new int[8];
                int[] super = new int[8];

                for (int i = 0; i < 8; i++)
                {
                    Player.WEAPON_2A_TAG[i] = MPWeapons.primaries[i].Tag2A;
                    Player.WEAPON_2B_TAG[i] = MPWeapons.primaries[i].Tag2B;
                    //Player.MISSILE_2A_TAG[i] = MPWeapons.secondaries[i].Tag2A;
                    //Player.MISSILE_2B_TAG[i] = MPWeapons.secondaries[i].Tag2B;

                    //max[i] = MPWeapons.secondaries[i].ammo;
                    //max_up[i] = MPWeapons.secondaries[i].ammoUp;
                    //super[i] = MPWeapons.secondaries[i].ammoSuper;
                }

                //MAX_MISSILE_AMMO.SetValue(typeof(int[]), max);
                //MAX_MISSILE_AMMO_UP.SetValue(typeof(int[]), max_up);
                //SUPER_MISSILE_AMMO_MP.SetValue(typeof(int[]), super);
            }
        }
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Temp Lancer Functions
    // ====================================================================
    //
    //
    // ====================================================================

    /*
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPWeapons_PlayerShip_MaybeFireWeapon_LANCER
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
    */
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
