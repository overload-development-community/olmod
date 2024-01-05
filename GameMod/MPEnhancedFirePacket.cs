using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using HarmonyLib;
using System.Reflection.Emit;
using Overload;

namespace GameMod
{
	[HarmonyPatch]
    public static class MPEnhancedFirePacket
    {
        public static void SendEnhancedProjectileFiredToClients(Player player, ProjPrefab type, Vector3 pos, Quaternion rot, WeaponUnlock upgrade_level, bool no_sound, int id, float strength)
        {
            foreach (Player player2 in Overload.NetworkManager.m_Players)
            {
                if ((bool)player2 && !player2.isLocalPlayer && !player2.m_spectator)
                {
                    if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "efirepacket"))
                    {
                        player2.connectionToClient.SendByChannel(MessageTypes.MsgEnhancedFirePacket, new EnhancedFireProjectileToClientMessage(player.netId, type, pos, rot, upgrade_level, no_sound, id, strength), 2);
						//Debug.Log("CCF sent E-fire packet, strength " + strength);
					}
                    else // older client, don't send the upgraded packet
                    {
                        player2.connectionToClient.SendByChannel(70, new FireProjectileToClientMessage(player.netId, type, pos, rot, upgrade_level, no_sound, id), 2);
                    }
                }
            }
        }

		public static void OnEnhancedFireProjectileToClient(NetworkMessage msg)
		{
			if (GameplayManager.IsMultiplayerActive && !NetworkMatch.InGameplay())
			{
				return;
			}
			EnhancedFireProjectileToClientMessage fireProjectileToClientMessage = msg.ReadMessage<EnhancedFireProjectileToClientMessage>();
			Player playerFromNetId = GetPlayerFromNetId(fireProjectileToClientMessage.m_net_id);
			if (playerFromNetId == null)
			{
				return;
			}
			if (!playerFromNetId.isLocalPlayer || fireProjectileToClientMessage.m_proj_prefab == ProjPrefab.missile_devastator_mini || fireProjectileToClientMessage.m_proj_prefab == ProjPrefab.missile_smart_mini)
			{
				ProjectileManager.PlayerFire(playerFromNetId, fireProjectileToClientMessage.m_proj_prefab, fireProjectileToClientMessage.m_pos, fireProjectileToClientMessage.m_rot, fireProjectileToClientMessage.m_strength, fireProjectileToClientMessage.m_upgrade_level, fireProjectileToClientMessage.m_no_sound, -1, fireProjectileToClientMessage.m_id);
			}
			if (playerFromNetId.isLocalPlayer && fireProjectileToClientMessage.m_proj_prefab != ProjPrefab.missile_devastator_mini && fireProjectileToClientMessage.m_proj_prefab != ProjPrefab.missile_smart_mini)
			{
				Projectile projectile = ProjectileManager.FindOldestUnlinkedProjectileForPlayer(playerFromNetId, fireProjectileToClientMessage.m_proj_prefab);
				if ((bool) projectile)
				{
					projectile.m_projectile_id = fireProjectileToClientMessage.m_id;
				}
			}
			//Debug.Log("CCF Received E-fire packet, strength " + fireProjectileToClientMessage.m_strength);
        }

		[HarmonyReversePatch]
		[HarmonyPatch(typeof(Client), "GetPlayerFromNetId")]
		public static Player GetPlayerFromNetId(NetworkInstanceId net_id)
		{
			// its a stub so it has no initial content
			throw new NotImplementedException("It's a stub");
		}

		internal class EnhancedFireProjectileToClientMessage : MessageBase
		{
			public NetworkInstanceId m_net_id;
			public ProjPrefab m_proj_prefab;
			public Vector3 m_pos;
			public Quaternion m_rot;
			public int m_id;
			public WeaponUnlock m_upgrade_level;
			public bool m_no_sound;
			public float m_strength;

			public EnhancedFireProjectileToClientMessage()
			{
			}

			public EnhancedFireProjectileToClientMessage(NetworkInstanceId net_id, ProjPrefab proj_prefab, Vector3 pos, Quaternion rot, WeaponUnlock upgrade_level, bool no_sound, int id, float strength)
			{
				m_net_id = net_id;
				m_proj_prefab = proj_prefab;
				m_pos = pos;
				m_rot = rot;
				m_upgrade_level = upgrade_level;
				m_id = id;
				m_no_sound = no_sound;
				m_strength = strength;
			}

			public override void Serialize(NetworkWriter writer)
			{
				writer.Write(m_net_id);
				writer.Write((byte)m_proj_prefab);
				//writer.Write(HalfHelper.Compress(m_pos.x));
				//writer.Write(HalfHelper.Compress(m_pos.y));
				//writer.Write(HalfHelper.Compress(m_pos.z));
				writer.Write(m_pos.x);
				writer.Write(m_pos.y);
				writer.Write(m_pos.z);
				writer.Write(NetworkCompress.CompressQuaternion(m_rot));
				//writer.Write(m_rot);
				writer.Write(m_id);
				byte b = (byte)m_upgrade_level;
				if (m_no_sound)
				{
					b = (byte)(b | 0x80u);
				}
				writer.Write(b);
				writer.Write(m_strength);
			}

			public override void Deserialize(NetworkReader reader)
			{
				m_net_id = reader.ReadNetworkId();
				m_proj_prefab = (ProjPrefab)reader.ReadByte();
				//m_pos.x = HalfHelper.Decompress(reader.ReadUInt16());
				//m_pos.y = HalfHelper.Decompress(reader.ReadUInt16());
				//m_pos.z = HalfHelper.Decompress(reader.ReadUInt16());
				m_pos.x = reader.ReadSingle();
				m_pos.y = reader.ReadSingle();
				m_pos.z = reader.ReadSingle();
				m_rot = NetworkCompress.DecompressQuaternion(reader.ReadUInt32());
				//m_rot = reader.ReadQuaternion();
				m_id = reader.ReadInt32();
				byte b = reader.ReadByte();
				m_upgrade_level = (WeaponUnlock)(b & 0x7F);
				m_no_sound = (b & 0x80) > 0;
				m_strength = reader.ReadSingle();
			}
		}
	}

	// Injections

    [HarmonyPatch(typeof(ProjectileManager), "FireProjectile")]
    public static class MPFirePacketEnhancement_ProjectileManager_FireProjectile
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Server), "SendProjectileFiredToClients"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)4); // "strength" argument
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPEnhancedFirePacket), "SendEnhancedProjectileFiredToClients"));
                }
                else
                {
                    yield return code;
                }
            }
        }
    }

	[HarmonyPatch(typeof(Client), "RegisterHandlers")]
	public static class MPFirePacketEnhancement_Client_RegisterHandlers
	{
		public static void Postfix()
		{
			if (Client.GetClient() == null)
				return;
			Client.GetClient().RegisterHandler(MessageTypes.MsgEnhancedFirePacket, MPEnhancedFirePacket.OnEnhancedFireProjectileToClient);
		}
	}
}
