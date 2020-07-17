using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod
{
	public class MPCreeperSync
	{
		public const int NET_VERSION_CREEPER_SYNC = 1;
	}

	public struct ProjInfo
	{
		public int m_id;
		public Vector3 m_pos;
		public Vector3 m_vel;
	}

	public class ProjUpdateMsg : MessageBase
	{
		public override void Serialize(NetworkWriter writer)
		{
			writer.WritePackedUInt32((uint)m_num_proj_info);
			for (int i = 0; i < m_num_proj_info; i++)
			{
				writer.Write(m_proj_info[i].m_id);
				writer.Write(m_proj_info[i].m_pos);
				writer.Write(m_proj_info[i].m_vel);
			}
		}

		public override void Deserialize(NetworkReader reader)
		{
			m_num_proj_info = (int)reader.ReadPackedUInt32();
			m_proj_info = new ProjInfo[m_num_proj_info];
			for (int i = 0; i < m_num_proj_info; i++)
			{
				m_proj_info[i].m_id = reader.ReadInt32();
				m_proj_info[i].m_pos = reader.ReadVector3();
				m_proj_info[i].m_vel = reader.ReadVector3();
			}
		}

		public int m_num_proj_info;
		public ProjInfo[] m_proj_info;
	}

	public class ExplodeMsg : MessageBase
	{
		public override void Serialize(NetworkWriter writer)
		{
			writer.Write(m_id);
			writer.Write(m_pos);
			writer.Write(m_damaged_something);
		}

		public override void Deserialize(NetworkReader reader)
		{
			m_id = reader.ReadInt32();
			m_pos = reader.ReadVector3();
			m_damaged_something = reader.ReadBoolean();
		}

		public int m_id;
		public Vector3 m_pos;
		public bool m_damaged_something;
	}
		
	[HarmonyPatch(typeof(Server), "SendSnapshotsToPlayers")]
	class CreeperSyncSend
	{
		static ProjUpdateMsg msg = new ProjUpdateMsg() { m_proj_info = new ProjInfo[10] };
		public const int UPDATE_INTERVAL = 8; // must be power of 2
		static int frame_num;

		static void Postfix()
		{
			var creeper_list = ProjectileManager.proj_list[(int)ProjPrefab.missile_creeper];
			if (creeper_list.Count > msg.m_proj_info.Length)
				msg.m_proj_info = new ProjInfo[creeper_list.Count];
			var proj_info = msg.m_proj_info;
			int count = 0;
			if (++frame_num == UPDATE_INTERVAL)
				frame_num = 0;
			foreach (var proj in creeper_list)
			{
				if (proj.m_alive)
				{
					var c_proj = proj.c_proj;
					int id = c_proj.m_projectile_id;
					if ((id & (UPDATE_INTERVAL - 1)) != frame_num)
						continue;
					proj_info[count].m_id = id;
					proj_info[count].m_pos = c_proj.transform.position;
					proj_info[count].m_vel = c_proj.c_rigidbody.velocity;
					count++;
				}
			}

			if (count == 0)
				return;

			msg.m_num_proj_info = count;
			foreach (var conn in NetworkServer.connections)
				if (conn != null && MPTweaks.ClientHasNetVersion(conn.connectionId, MPCreeperSync.NET_VERSION_CREEPER_SYNC))
					conn.SendByChannel(MessageTypes.MsgCreeperSync, msg, 3); // channel 3 has QosType.StateUpdate
		}
	}

	[HarmonyPatch(typeof(Client), "RegisterHandlers")]
	class CreeperSyncRegister
	{
		static void OnCreeperSyncMsg(NetworkMessage msg)
		{
			CreeperSyncExplode.m_allow_explosions = false;
			var proj_msg = msg.ReadMessage<ProjUpdateMsg>();
			var count = proj_msg.m_num_proj_info;
			var proj_info = proj_msg.m_proj_info;
			for (int i = 0; i < count; i++)
			{
				var proj = ProjectileManager.FindProjectileById(ProjPrefab.missile_creeper, proj_info[i].m_id);
				if (proj == null)
					continue;
				var diff = proj_info[i].m_pos - proj.transform.position;
				float sqrDiff = diff.sqrMagnitude;
				/*
				Debug.LogFormat("moving creeper {0} from {1},{2},{3} to {4},{5},{6} ({7})",
					proj_info[i].m_id,
					proj.transform.position.x, proj.transform.position.y, proj.transform.position.z,
					proj_info[i].m_pos.x, proj_info[i].m_pos.y, proj_info[i].m_pos.z,
					sqrDiff);
				*/

				var rigidbody = proj.c_rigidbody;

				// slow down just fired creeper to move to ping-delayed server pos
				if (proj.m_owner_player.isLocalPlayer)
				{
					float age = Time.time - proj.m_create_time;
					if (age < 1f)
					{
						if (age < .6f && Vector3.Dot(proj.transform.forward, diff) < 0 && sqrDiff > 0.2f)
							rigidbody.velocity *= .8f;
						else
							rigidbody.velocity = proj_info[i].m_vel + diff / (1.1f - age);
						return;
					}
				}
				if (sqrDiff > 0.01f)
					rigidbody.MovePosition(proj_info[i].m_pos);
				rigidbody.velocity = proj_info[i].m_vel;
			}
		}

		static void OnExplodeMsg(NetworkMessage msg)
		{
			var explode_msg = msg.ReadMessage<ExplodeMsg>();
			if (Server.IsActive())
				return;
			var proj = ProjectileManager.FindProjectileById(ProjPrefab.missile_creeper, explode_msg.m_id);
			if (proj == null)
				proj = ProjectileManager.FindProjectileById(ProjPrefab.missile_devastator, explode_msg.m_id);
			if (proj == null)
				return;
			proj.c_rigidbody.MovePosition(explode_msg.m_pos);
			CreeperSyncExplode.m_allow_explosions = true;
			proj.Explode(explode_msg.m_damaged_something);
			CreeperSyncExplode.m_allow_explosions = false;
		}

		static void OnSetAlternatingMissileFire(NetworkMessage msg)
		{
			var m_alternating_missile_fire = msg.ReadMessage<IntegerMessage>().value != 0;
			GameManager.m_local_player.c_player_ship.m_alternating_missile_fire = m_alternating_missile_fire;
		}

		static void Postfix(NetworkClient ___m_network_client)
		{
			if (___m_network_client == null)
				return;
			___m_network_client.RegisterHandler(MessageTypes.MsgCreeperSync, OnCreeperSyncMsg);
			___m_network_client.RegisterHandler(MessageTypes.MsgExplode, OnExplodeMsg);
			___m_network_client.RegisterHandler(MessageTypes.MsgSetAlternatingMissleFire, OnSetAlternatingMissileFire);
		}
	}

	[HarmonyPatch(typeof(Projectile), "Explode")]
	class CreeperSyncExplode
	{
		public static bool m_allow_explosions;

		static bool Prefix(ProjPrefab ___m_type, Projectile __instance, bool damaged_something)
		{
			if (!GameplayManager.IsMultiplayerActive ||
				(___m_type != ProjPrefab.missile_creeper && ___m_type != ProjPrefab.missile_devastator) || // Extend to devastators
				__instance.m_projectile_id == -1 || __instance.RemainingLifetime() < -4f) // unlinked/timeout: probably stuck, explode anyway
				return true;
			if (!Server.IsActive()) // ignore explosions on client if creeper-sync active
				return m_allow_explosions;
			var msg = new ExplodeMsg();
			msg.m_id = __instance.m_projectile_id;
			msg.m_pos = __instance.c_transform.position;
			msg.m_damaged_something = damaged_something;
			foreach (var conn in NetworkServer.connections)
				if (conn != null && MPTweaks.ClientHasNetVersion(conn.connectionId, MPCreeperSync.NET_VERSION_CREEPER_SYNC))
					NetworkServer.SendToClient(conn.connectionId, MessageTypes.MsgExplode, msg);
			return true;
		}
	}

	[HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
	class CreeperSyncMatchInit
	{
		static void Prefix()
		{
			CreeperSyncExplode.m_allow_explosions = true; // assume server without creeper-sync until sync msg received
		}
	}

	// Should no longer be needed with MPSniperPackets.
	//// the server might fire more missles than the client, correct state when the missiles run out on the server
	//[HarmonyPatch(typeof(PlayerShip), "MaybeFireMissile")]
	//class CreeperSyncRunOutSync
	//{
	//	static void Postfix(float ___m_refire_missile_time, Player ___c_player)
	//	{
	//		if (!GameplayManager.IsMultiplayerActive ||
	//			!Server.IsActive() ||
	//			!(___m_refire_missile_time == 1f &&
	//			___c_player.m_old_missile_type != MissileType.NUM &&
	//			___c_player.m_missile_ammo[(int)___c_player.m_old_missile_type] == 0)) // just switched?
	//			return;

	//		// make sure ammo is also zero on the client
	//		___c_player.CallRpcSetMissileAmmo((int)___c_player.m_old_missile_type, 0);

	//		// workaround for not updating missle name in hud
	//		___c_player.CallRpcSetMissileType(___c_player.m_missile_type);
	//		___c_player.CallTargetUpdateCurrentMissileName(___c_player.connectionToClient);

	//		// make sure alternating missle fire is equal
	//		int connectionId = ___c_player.connectionToClient.connectionId;
	//		if (MPTweaks.ClientHasNetVersion(connectionId, MPCreeperSync.NET_VERSION_CREEPER_SYNC))
	//		{
	//			var msg = new IntegerMessage(___c_player.c_player_ship.m_alternating_missile_fire ? 1 : 0);
	//			NetworkServer.SendToClient(connectionId, CreeperSyncMsg.MsgSetAlternatingMissleFire, msg);
	//		}
	//	}
	//}
}
