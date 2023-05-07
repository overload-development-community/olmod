using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;


namespace GameMod
{
    public class Thunderbolt : PrimaryWeapon
    {
		private float m_thunder_sound_timer = 0f;

		private static int m_charge_loop_index = -1;
        private static float m_tb_overchargedamage_multiplier = 4f; // 4.0dps self-damage instead of stock 1.0dps)

        public Thunderbolt()
        {
            displayName = "THUNDERBOLT";
            Tag2A = "MX";
            Tag2B = "RT";
            UsesEnergy = true;
        }

        public override void Fire(float refire_multiplier)
        {
            ProjPrefab type = ProjPrefab.proj_thunderbolt;
            Quaternion localRotation = player.c_player_ship.c_transform.localRotation;
            player.c_player_ship.m_thunder_power = Mathf.Min((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 1f : 1.15f, player.c_player_ship.m_thunder_power);
            // originally ProjectileManager.PlayerFire()
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_right.position, localRotation, player.c_player_ship.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], true, 0);
            MPSniperPackets.MaybePlayerFire(player, type, player.c_player_ship.m_muzzle_left.position, localRotation, player.c_player_ship.m_thunder_power, player.m_weapon_level[(int)player.m_weapon_type], false, 1);
            if (GameplayManager.IsMultiplayerActive)
            {
                player.c_player_ship.m_refire_time += 0.5f * refire_multiplier;
            }
            else
            {
                player.c_player_ship.m_refire_time += ((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.45f : 0.5f) * refire_multiplier;
            }
            if (!GameplayManager.IsMultiplayer)
            {
                player.c_player_ship.c_rigidbody.AddForce(player.c_player_ship.c_forward * (UnityEngine.Random.Range(-300f, -350f) * (0.5f + player.c_player_ship.m_thunder_power * 1.2f) * player.c_player_ship.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
                Vector3 vector4 = RUtility.RandomUnitVector();
                player.c_player_ship.c_rigidbody.AddTorque((vector4 + UnityEngine.Random.onUnitSphere * 0.2f) * (UnityEngine.Random.Range(1000f, 1500f) * (0.5f + player.c_player_ship.m_thunder_power * 1.2f) * RUtility.FIXED_FT_INVERTED));
            }
            player.PlayCameraShake(CameraShakeType.FIRE_THUNDER, 1f + player.c_player_ship.m_thunder_power * 2f, 1f + player.c_player_ship.m_thunder_power);
            if (MPSniperPackets.AlwaysUseEnergy())
            {
                player.UseEnergy(2f + player.c_player_ship.m_thunder_power * 3f);
            }
            player.c_player_ship.m_thunder_power = 0f;
			m_thunder_sound_timer = 0f;
            StopThunderboltSelfDamageLoop();
        }

        // *******************************************************
        // TEMPORARY UNTIL PROCESSFIRINGCONTROLS IS FULLY REDONE
        // *******************************************************

        [HarmonyPatch(typeof(PlayerShip), "ProcessFiringControls")]
        class Thunderbolt_PlayerShip_ProcessFiringControls
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                foreach (var code in codes)
                {
                    if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(PlayerShip), "ThunderCharge"))
                    {
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPShips), "GetShip"));
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "primaries"));
                        yield return new CodeInstruction(OpCodes.Ldc_I4_6);
                        yield return new CodeInstruction(OpCodes.Ldelem_Ref);
                        yield return new CodeInstruction(OpCodes.Castclass, typeof(Thunderbolt));
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Thunderbolt), "ThunderCharge"));
                    }
                    else
                    {
                        yield return code;
                    }
                }
            }
        }

        // *******************************************************
        // TEMPORARY UNTIL PROCESSFIRINGCONTROLS IS FULLY REDONE
        // *******************************************************

        private void ThunderCharge()
		{
            if (!(ps.m_refire_time <= 0f))
			{
				return;
			}
            else if (ps.m_thunder_power == 0f)
            {
                StopThunderboltSelfDamageLoop();
            }

			float num = ((!GameplayManager.IsMultiplayerActive) ? RUtility.FRAMETIME_GAME : RUtility.FRAMETIME_FIXED);
			if (num == 0f)
			{
				return;
			}
			if (player.m_weapon_level[(int)player.m_weapon_type] == WeaponUnlock.LEVEL_2A)
			{
				ps.m_thunder_power += num / 1.75f * ((!player.m_overdrive) ? 1f : 2f);
			}
			else
			{
				ps.m_thunder_power += num / 2f * ((!player.m_overdrive) ? 1f : 2f);
			}
			float num2 = Mathf.Min((player.m_weapon_level[(int)player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 1f : 1.15f, ps.m_thunder_power);
			if (ps.m_thunder_power > 2f && NetworkManager.IsServer())
			{
				DamageInfo di = default(DamageInfo);
				di.type = DamageType.ENERGY;
				di.owner = ps.c_go;
				di.push_dir = Vector3.zero;
				di.pos = ps.c_transform_position;
                //di.damage = ((!(ps.m_thunder_power > 3f)) ? (ps.m_thunder_power - 2f) : 1f) * num;
                di.damage = ((!(ps.m_thunder_power > 3f)) ? (ps.m_thunder_power - 2f) : 1f) * num * (GameplayManager.IsMultiplayer ? m_tb_overchargedamage_multiplier : 1f) * (ship.triTB ? 1.5f : 1f);
                di.stun_multiplier = 0f;
				if (!player.m_invulnerable)
				{
					ps.ApplyDamage(di);
				}
			}
			if (!ps.m_light_tb_enabled)
			{
				ps.m_light_tb_enabled = true;
				ps.c_lights[4].enabled = ps.m_light_tb_enabled;
			}
			ps.c_lights[4].intensity = Mathf.Min(ps.m_thunder_power, 1.25f) * UnityEngine.Random.Range(1.5f, 1.8f);
			player.PlayCameraShake(CameraShakeType.CHARGE_THUNDER, 0.1f + 0.5f * num2 * num * 100f, 0.8f);
			if (!GameplayManager.IsMultiplayer)
			{
				ps.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (1f * num2 / num));
			}
			m_thunder_sound_timer -= num;
			if (m_thunder_sound_timer <= 0f)
			{
				if (ps.isLocalPlayer)
				{
					GameManager.m_audio.PlayCue2D(381, 0.35f + num2 * UnityEngine.Random.Range(0.4f, 0.5f), -0.25f + num2 * 0.3f, 0f, reverb: true);

                    if (ps.m_thunder_power >= 2f && m_charge_loop_index == -1)
                    {
                        m_charge_loop_index = GameManager.m_audio.PlayCue2DLoop((int)SoundEffect.cine_sfx_warning_loop, 1f, 0f, 0f, true);
                    }
                }
				else
				{
					GameManager.m_audio.PlayCuePos(381, ps.c_transform_position, 0.35f + num2 * UnityEngine.Random.Range(0.4f, 0.5f), -0.25f + num2 * 0.3f);
				}
				m_thunder_sound_timer = 0.22f - num2 * 0.08f;
			}
        }

        public static void StopThunderboltSelfDamageLoop()
        {
            if (m_charge_loop_index != -1)
            {
                GameManager.m_audio.StopSound(m_charge_loop_index);
                m_charge_loop_index = -1;
            }
        }
    }


    [HarmonyPatch(typeof(PlayerShip), "OnDestroy")]
    class MPWeaponBehavior_Thunderbolt_PlayerShip_OnDestroy
    {
        static void Postfix()
        {
            Thunderbolt.StopThunderboltSelfDamageLoop();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "OnDisable")]
    class MPWeaponBehavior_Thunderbolt_PlayerShip_OnDisable
    {
        static void Postfix()
        {
            Thunderbolt.StopThunderboltSelfDamageLoop();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class MPWeaponBehavior_Thunderbolt_PlayerShip_Update
    {
        static void Postfix(PlayerShip __instance)
        {
            if ((__instance.m_boosting || __instance.m_dead || __instance.m_dying) && GameplayManager.IsMultiplayerActive && __instance.isLocalPlayer)
            {
                Thunderbolt.StopThunderboltSelfDamageLoop();
            }
        }
    }
}