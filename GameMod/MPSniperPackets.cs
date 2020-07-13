using CodeStage.AntiCheat.ObscuredTypes;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Overload;
using Tobii.Gaming;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
	internal class MPSniperPackets
	{
		static internal List<NetworkInstanceId> snipers = new List<NetworkInstanceId>();

		static internal void InitForMatch()
		{
			snipers = new List<NetworkInstanceId>();
		}
	}

	public class SniperPacketMessage : MessageBase
	{
		public override void Serialize(NetworkWriter writer)
		{
			writer.Write((byte)1); // version
			writer.Write(m_player_id);
			writer.Write((byte)m_type);
			writer.Write(m_pos.x);
			writer.Write(m_pos.y);
			writer.Write(m_pos.z);
			writer.Write(m_rot.w);
			writer.Write(m_rot.x);
			writer.Write(m_rot.y);
			writer.Write(m_rot.z);
			writer.Write(m_strength);
			writer.Write((byte)m_upgrade_lvl);
			writer.Write(m_no_sound);
			writer.Write(m_slot);
			writer.Write(m_force_id);
		}
		public override void Deserialize(NetworkReader reader)
		{
			var version = reader.ReadByte();
			m_player_id = reader.ReadNetworkId();
			m_type = (ProjPrefab)reader.ReadByte();
			m_pos = new Vector3();
			m_pos.x = reader.ReadSingle();
			m_pos.y = reader.ReadSingle();
			m_pos.z = reader.ReadSingle();
			m_rot = new Quaternion();
			m_rot.w = reader.ReadSingle();
			m_rot.x = reader.ReadSingle();
			m_rot.y = reader.ReadSingle();
			m_rot.z = reader.ReadSingle();
			m_strength = reader.ReadSingle();
			m_upgrade_lvl = (WeaponUnlock)reader.ReadByte();
			m_no_sound = reader.ReadBoolean();
			m_slot = reader.ReadInt32();
			m_force_id = reader.ReadInt32();
		}

		public NetworkInstanceId m_player_id;
		public ProjPrefab m_type;
		public Vector3 m_pos;
		public Quaternion m_rot;
		public float m_strength;
		public WeaponUnlock m_upgrade_lvl;
		public bool m_no_sound;
		public int m_slot;
		public int m_force_id;
	}

	[HarmonyPatch(typeof(Client), "RegisterHandlers")]
	class MPSniperPacketsHandlers
	{
		private static void OnSniperPacket(NetworkMessage rawMsg)
		{
			if (!Overload.NetworkManager.IsHeadless())
			{
				return;
			}

			var msg = rawMsg.ReadMessage<SniperPacketMessage>();
			var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

			if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
			{
				return;
			}

			MPSniperPackets.snipers.Add(player.netId);

			ProjectileManager.PlayerFire(player, msg.m_type, msg.m_pos, msg.m_rot, msg.m_strength, msg.m_upgrade_lvl, msg.m_no_sound, msg.m_slot, msg.m_force_id);
		}

		static void Postfix()
		{
			if (Client.GetClient() == null)
				return;
			Client.GetClient().RegisterHandler(MessageTypes.MsgSniperPacket, OnSniperPacket);
		}
	}

	[HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
	class MPSniperPacketsInitBeforeEachMatch
	{
		private static void Postfix()
		{
			MPSniperPackets.InitForMatch();
		}
	}

	[HarmonyPatch(typeof(ProjectileManager), "PlayerFire")]
	class MPSniperPacketsPlayerFire
	{
		static void Prefix(Player player, ProjPrefab type, Vector3 pos, Quaternion rot, float strength = 0f, WeaponUnlock upgrade_lvl = WeaponUnlock.LEVEL_0, bool no_sound = false, int slot = -1, int force_id = -1)
		{
			if (GameplayManager.IsMultiplayerActive && !Overload.NetworkManager.IsHeadless())
			{
				Client.GetClient().Send(MessageTypes.MsgSniperPacket, new SniperPacketMessage
				{
					m_player_id = player.netId,
					m_type = type,
					m_pos = pos,
					m_rot = rot,
					m_strength = strength,
					m_upgrade_lvl = upgrade_lvl,
					m_no_sound = no_sound,
					m_slot = slot,
					m_force_id = force_id
				});
			}
		}
	}

	[HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
	class MPSniperPacketsMaybeFireWeapon
	{
		static bool Prefix(PlayerShip __instance, ref int ___flak_fire_count, ref float ___m_thunder_sound_timer)
		{
			if (!GameplayManager.IsMultiplayerActive || !Overload.NetworkManager.IsHeadless() || !MPSniperPackets.snipers.Contains(__instance.c_player.netId))
			{
				return true;
			}

			if (__instance.m_refire_time <= 0f && !__instance.c_player.m_spectator)
			{
				bool flag = false;
				if (!__instance.c_player.CanFireWeaponAmmo())
				{
					if (__instance.c_player.m_energy <= 0f)
					{
						if (__instance.c_player.m_ammo <= 0)
						{
							if (__instance.c_player.WeaponUsesAmmo(__instance.c_player.m_weapon_type))
							{
								__instance.c_player.SwitchToEnergyWeapon();
							}
							flag = true;
						}
						else if (!__instance.c_player.SwitchToAmmoWeapon())
						{
							flag = true;
						}
					}
					else
					{
						__instance.c_player.SwitchToEnergyWeapon();
					}
					if (!flag)
					{
						__instance.m_refire_time = 0.5f;
						return false;
					}
				}
				if (GameplayManager.IsMultiplayerActive && __instance.c_player.m_spawn_invul_active)
				{
					Player player = __instance.c_player;
					player.m_timer_invuln -= (float)NetworkMatch.m_respawn_shield_seconds;
				}
				__instance.m_alternating_fire = !__instance.m_alternating_fire;
				Vector3 a = __instance.c_forward;
				float num = (!flag) ? 1f : 3f;
				__instance.FiringVolumeModifier = 1f;
				__instance.FiringPitchModifier = 0f;
				switch (__instance.c_player.m_weapon_type)
				{
					case WeaponType.IMPULSE:
						{
							__instance.FiringVolumeModifier = 0.75f;
							if (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] == WeaponUnlock.LEVEL_2A || GameplayManager.IsMultiplayerActive)
							{
								__instance.m_refire_time += 0.28f * num;
								if (Server.IsActive())
								{
									__instance.c_player.UseEnergy(0.666667f);
								}
								__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_IMPULSE, 1.3f, 1.2f);
							}
							else
							{
								__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.25f : 0.2f) * num;
								if (Server.IsActive())
								{
									__instance.c_player.UseEnergy((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.33333f);
								}
								__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_IMPULSE, 1f, 1f);
							}
							break;
						}
					case WeaponType.CYCLONE:
						{
							float num2 = 1f - Mathf.Min((float)___flak_fire_count * 0.05f, (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.25f);
							__instance.FiringPitchModifier = ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? (0.6f - num2) : (0.75f - num2)) * 0.25f;
							__instance.FiringVolumeModifier = 0.75f;
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_CYCLONE, 1f, 1f);
							Quaternion localRotation = __instance.c_transform.localRotation;
							if (__instance.IsCockpitVisible)
							{
								ParticleManager.psm[2].StartParticle(8, __instance.m_muzzle_center.position, localRotation, __instance.c_transform, null, false);
							}
							if (Server.IsActive())
							{
								__instance.c_player.UseEnergy((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.4f : ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.3f : 0.3333f));
							}
							if (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
							{
								__instance.m_fire_angle = (__instance.m_fire_angle + 350f) % 360f;
							}
							else
							{
								__instance.m_fire_angle = (__instance.m_fire_angle + 345f) % 360f;
							}
							__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.2f : 0.16f) * num2 * num;
							___flak_fire_count++;
							if (!GameplayManager.IsMultiplayer)
							{
								float num3 = num2 / 1f * RUtility.FIXED_FT_INVERTED;
								__instance.c_rigidbody.AddForce(a * (UnityEngine.Random.Range(-40f, -50f) * __instance.c_rigidbody.mass * num3));
								__instance.c_rigidbody.AddTorque(__instance.c_transform_rotation * new Vector3(1f, 0f, 0f) * (UnityEngine.Random.Range(-150f, -100f) * num3));
							}
							break;
						}
					case WeaponType.REFLEX:
						{
							__instance.FiringVolumeModifier = 0.75f;
							__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.1f : 0.08f) * num;
							if (Server.IsActive())
							{
								__instance.c_player.UseEnergy(0.3f);
							}
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_REFLEX, 1f, 1f);
							break;
						}
					case WeaponType.CRUSHER:
						{
							__instance.FiringVolumeModifier = 0.75f;
							Vector3 position = __instance.m_muzzle_left.position;
							Vector3 position2 = __instance.m_muzzle_right.position;
							Quaternion localRotation = __instance.c_transform.localRotation;
							if (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
							{
								if (__instance.IsCockpitVisible)
								{
									if (__instance.m_alternating_fire)
									{
										ParticleManager.psm[2].StartParticle(6, position2, localRotation, __instance.c_transform, null, false);
									}
									else
									{
										ParticleManager.psm[2].StartParticle(6, position, localRotation, __instance.c_transform, null, false);
									}
								}
								__instance.m_refire_time += 0.2f;
								__instance.c_player.UseAmmo(3);
								if (!GameplayManager.IsMultiplayer)
								{
									__instance.c_rigidbody.AddForce(a * (UnityEngine.Random.Range(-100f, -150f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
									__instance.c_rigidbody.AddTorque(__instance.c_transform_rotation * new Vector3(1f, 0f, 0f) * (UnityEngine.Random.Range(-300f, -200f) * RUtility.FIXED_FT_INVERTED));
								}
								__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_CRUSHER, 1f, 0.8f);
							}
							else
							{
								if (__instance.IsCockpitVisible)
								{
									ParticleManager.psm[2].StartParticle(6, position2, localRotation, __instance.c_transform, null, false);
									ParticleManager.psm[2].StartParticle(6, position, localRotation, __instance.c_transform, null, false);
								}
								if (__instance.c_player.m_overdrive)
								{
									if (GameplayManager.IsMultiplayerActive)
									{
										__instance.m_refire_time += 0.55f;
									}
									else
									{
										__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.5f : 0.35f);
									}
								}
								else if (GameplayManager.IsMultiplayerActive)
								{
									__instance.m_refire_time += 0.45f;
								}
								else
								{
									__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.5f : 0.3f);
								}
								__instance.c_player.UseAmmo(6);
								if (!GameplayManager.IsMultiplayer)
								{
									__instance.c_rigidbody.AddForce(a * (UnityEngine.Random.Range(-150f, -200f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
									__instance.c_rigidbody.AddTorque(__instance.c_transform_rotation * new Vector3(1f, 0f, 0f) * (UnityEngine.Random.Range(-500f, -400f) * RUtility.FIXED_FT_INVERTED));
								}
								__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_CRUSHER, 2f, 1f);
							}
							break;
						}
					case WeaponType.DRILLER:
						__instance.FiringVolumeModifier = 0.75f;
						if (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
						{
							__instance.m_refire_time += 0.11f;
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_DRILLER, 0.7f, 0.7f);
							__instance.c_player.UseAmmo(1);
						}
						else
						{
							__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.26f : 0.22f);
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_DRILLER, 1f, 1f);
							__instance.c_player.UseAmmo(2);
						}
						break;
					case WeaponType.FLAK:
						{
							__instance.FiringVolumeModifier = 0.75f;
							if (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
							{
								if (___flak_fire_count == 0)
								{
									GameManager.m_audio.PlayCue2D(337, 0.7f, 0.5f, 0f, true);
									GameManager.m_audio.PlayCue2D(338, 0.7f, 0.5f, 0f, true);
									___flak_fire_count++;
									__instance.m_refire_time = 0.15f;
								}
								else
								{
									__instance.m_refire_time += 0.08f + (float)Mathf.Max(0, 4 - ___flak_fire_count) * 0.01f;
									if (!GameplayManager.IsMultiplayer)
									{
										__instance.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(20f, 30f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
										__instance.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(2f, 3f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
									}
									__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.9f, 1.1f);
									___flak_fire_count++;
									__instance.c_player.UseAmmo(1);
								}
							}
							else
							{
								if (__instance.m_alternating_fire)
								{
									__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] < WeaponUnlock.LEVEL_1) ? 0.095f : 0.075f);
								}
								else
								{
									__instance.m_refire_time += 0.03f;
									if (!GameplayManager.IsMultiplayer)
									{
										__instance.c_rigidbody.AddForce(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(10f, 20f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
										__instance.c_rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * (UnityEngine.Random.Range(1f, 2f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
									}
									__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_FLAK, 0.6f, 1f);
									__instance.c_player.UseAmmo(1);
								}
							}
							break;
						}
					case WeaponType.THUNDERBOLT:
						{
							__instance.m_thunder_power = Mathf.Min((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 1f : 1.15f, __instance.m_thunder_power);
							if (GameplayManager.IsMultiplayerActive)
							{
								__instance.m_refire_time += 0.5f * num;
							}
							else
							{
								__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.45f : 0.5f) * num;
							}
							if (!GameplayManager.IsMultiplayer)
							{
								__instance.c_rigidbody.AddForce(a * (UnityEngine.Random.Range(-300f, -350f) * (0.5f + __instance.m_thunder_power * 1.2f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
								Vector3 a4 = RUtility.RandomUnitVector();
								__instance.c_rigidbody.AddTorque((a4 + UnityEngine.Random.onUnitSphere * 0.2f) * (UnityEngine.Random.Range(1000f, 1500f) * (0.5f + __instance.m_thunder_power * 1.2f) * RUtility.FIXED_FT_INVERTED));
							}
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_THUNDER, 1f + __instance.m_thunder_power * 2f, 1f + __instance.m_thunder_power);
							if (Server.IsActive())
							{
								__instance.c_player.UseEnergy(2f + __instance.m_thunder_power * 3f);
							}
							__instance.m_thunder_power = 0f;
							___m_thunder_sound_timer = 0f;
							break;
						}
					case WeaponType.LANCER:
						{
							__instance.FiringVolumeModifier = 0.75f;
							if (__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] == WeaponUnlock.LEVEL_2B)
							{
								__instance.m_alternating_fire = !__instance.m_alternating_fire;
								__instance.m_refire_time += 0.133333f * num;
								if (Server.IsActive())
								{
									__instance.c_player.UseEnergy(1f);
								}
								__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 1f, 1f);
							}
							else
							{
								if (GameplayManager.IsMultiplayerActive)
								{
									if (__instance.c_player.m_overdrive)
									{
										__instance.m_refire_time += 0.29f;
									}
									else
									{
										__instance.m_refire_time += 0.23f * num;
									}
								}
								else if (__instance.c_player.m_overdrive)
								{
									__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.28f : 0.2f);
								}
								else
								{
									__instance.m_refire_time += ((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 0.2f : 0.1f) * num;
								}
								if (Server.IsActive())
								{
									if (GameplayManager.IsMultiplayerActive)
									{
										__instance.c_player.UseEnergy(1f);
									}
									else
									{
										__instance.c_player.UseEnergy((__instance.c_player.m_weapon_level[(int)__instance.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2A) ? 2f : 1.5f);
									}
								}
								__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_LANCER, 1.3f, 1.5f);
							}
							break;
						}
				}
				if (__instance.m_refire_time < 0.01f)
				{
					__instance.m_refire_time = 0.01f;
				}
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerShip), "MaybeFireMissile")]
	class MPSniperPacketsMaybeFireMissile
	{
		static bool Prefix(PlayerShip __instance)
		{
			if (!GameplayManager.IsMultiplayerActive || !Overload.NetworkManager.IsHeadless() || !MPSniperPackets.snipers.Contains(__instance.c_player.netId))
			{
				return true;
			}

			if (__instance.m_refire_missile_time <= 0f)
			{
				if (!__instance.c_player.CanFireMissileAmmo(MissileType.NUM))
				{
					__instance.c_player.m_old_missile_type = __instance.c_player.m_missile_type;
					if (__instance.c_player.m_missile_type_prev != MissileType.NUM)
					{
						__instance.c_player.Networkm_missile_type = __instance.c_player.m_missile_type_prev;
						if (__instance.c_player.m_missile_ammo[(int)__instance.c_player.m_missile_type] <= 0)
						{
							__instance.c_player.SwitchToNextMissileWithAmmo(false);
						}
						else
						{
							__instance.MissileSelectFX();
						}
						__instance.c_player.UpdateCurrentMissileName();
					}
					else
					{
						__instance.c_player.SwitchToNextMissileWithAmmo(false);
					}
					__instance.c_player.FindBestPrevMissile(false);
					__instance.m_refire_missile_time = 0.5f;
					return false;
				}
				if (GameplayManager.IsMultiplayerActive && __instance.c_player.m_spawn_invul_active)
				{
					Player player = __instance.c_player;
					player.m_timer_invuln -= (float)NetworkMatch.m_respawn_shield_seconds;
				}
				Vector3 direction = __instance.c_forward;
				Quaternion localRotation = __instance.c_transform.localRotation;
				Vector2 zero = Vector2.zero;
				if (!GameplayManager.IsMultiplayer && MenuManager.opt_use_tobii_secondaryaim && UIManager.GetEyeTrackingActivePos(ref zero, false))
				{
					direction = __instance.c_camera.ScreenPointToRay(TobiiAPI.GetGazePoint().Screen).direction;
					localRotation.SetLookRotation(direction, __instance.c_transform_rotation * new Vector3(0f, 1f, 0f));
				}
				if (!Player.CheatUnlimited)
				{
					ObscuredInt[] missile_ammo = __instance.c_player.m_missile_ammo;
					MissileType missile_type = __instance.c_player.m_missile_type;
					missile_ammo[(int)missile_type] = missile_ammo[(int)missile_type] - 1;
				}
				__instance.FiringVolumeModifier = 1f;
				switch (__instance.c_player.m_missile_type)
				{
					case MissileType.FALCON:
						{
							__instance.m_alternating_missile_fire = !__instance.m_alternating_missile_fire;
							__instance.m_refire_missile_time += ((__instance.c_player.m_missile_level[(int)__instance.c_player.m_missile_type] != WeaponUnlock.LEVEL_2A) ? 0.3f : 0.22f);
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_FALCON, 1f, 1f);
							break;
						}
					case MissileType.MISSILE_POD:
						{
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_MISSILE_POD, 1f, 1f);
							__instance.m_refire_missile_time += 0.11f;
							break;
						}
					case MissileType.HUNTER:
						{
							__instance.m_refire_missile_time += 0.35f;
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_HUNTER, 1f, 1f);
							break;
						}
					case MissileType.CREEPER:
						{
							__instance.m_alternating_missile_fire = !__instance.m_alternating_missile_fire;
							__instance.m_refire_missile_time += 0.12f;
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_CREEPER, 1f, 1f);
							break;
						}
					case MissileType.NOVA:
						{
							__instance.m_refire_missile_time += 0.4f;
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_NOVA, 1f, 1f);
							break;
						}
					case MissileType.DEVASTATOR:
						{
							__instance.m_refire_missile_time += ((__instance.c_player.m_missile_level[(int)__instance.c_player.m_missile_type] < WeaponUnlock.LEVEL_1) ? 1f : 20f);
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_DEVASTATOR, 1f, 1f);
							break;
						}
					case MissileType.TIMEBOMB:
						{
							__instance.m_refire_missile_time += 1f;
							if (!GameplayManager.IsMultiplayer)
							{
								__instance.c_rigidbody.AddForce(direction * (UnityEngine.Random.Range(-200f, -250f) * __instance.c_rigidbody.mass * RUtility.FIXED_FT_INVERTED));
								__instance.c_rigidbody.AddTorque(__instance.c_transform_rotation * new Vector3(1f, 0f, 0f) * (UnityEngine.Random.Range(-1500f, -1000f) * RUtility.FIXED_FT_INVERTED));
							}
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_TIMEBOMB, 1f, 1f);
							break;
						}
					case MissileType.VORTEX:
						{
							__instance.m_refire_missile_time += 0.5f;
							__instance.c_player.PlayCameraShake(CameraShakeType.FIRE_VORTEX, 1f, 1f);
							break;
						}
				}
				if (__instance.m_refire_missile_time < 1f || __instance.c_player.m_missile_type == MissileType.TIMEBOMB)
				{
					__instance.c_player.MaybeSwitchToNextMissile();
				}
				if (__instance.m_refire_missile_time < 0.01f)
				{
					__instance.m_refire_missile_time = 0.01f;
				}
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerShip), "FireFlare")]
	class MPSniperPacketsFireFlare
	{
		static bool Prefix(PlayerShip __instance, bool sticky = false, bool boosting = false)
		{
			if (!GameplayManager.IsMultiplayerActive || !Overload.NetworkManager.IsHeadless() || !MPSniperPackets.snipers.Contains(__instance.c_player.netId))
			{
				return true;
			}

			GameplayManager.m_flare_or_headlight = true;
			if (__instance.m_refire_flare_time <= 0f)
			{
				__instance.FiringVolumeModifier = 1f;
				if (sticky)
				{
					SFXCueManager.PlayRawSoundEffectPos(SoundEffect.flare2, __instance.m_muzzle_center.position, 0.6f, UnityEngine.Random.Range(0.4f, 0.5f), 0f);
					SFXCueManager.PlayRawSoundEffectPos(SoundEffect.wep_driller_fire_low1_r2, __instance.m_muzzle_center.position, 0.5f, UnityEngine.Random.Range(0f, 0.1f), 0f);
					__instance.m_refire_flare_time = 1f;
				}
				else
				{
					SFXCueManager.PlayRawSoundEffectPos(SoundEffect.flare2, __instance.m_muzzle_center.position, 1f, UnityEngine.Random.Range(0f, 0.1f), 0f);
					__instance.m_refire_flare_time = 0.5f;
				}
				__instance.m_flare_hold_timer = 0f;
			}

			return false;
		}
	}
}
