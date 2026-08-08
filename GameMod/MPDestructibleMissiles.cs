using HarmonyLib;
using Newtonsoft.Json.Linq;
using Overload;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    // server side hp tracker attached to each live missile
    class DestructibleMissile : MonoBehaviour
    {
        private float m_hp;
        private bool m_destroyed;
        private Projectile m_proj;

        // prevents multi collisions by tracking attacker id and collider
        private readonly HashSet<int> m_hit_by = new HashSet<int>();
        private readonly List<Collider> m_ignored = new List<Collider>();

        public void Initialize(Projectile proj, float hp)
        {
            RestoreIgnoredCollisions();
            m_hp = hp;
            m_proj = proj;
            m_destroyed = false;
            m_hit_by.Clear();
        }

        // returns true when the missile survived a plain projectile hit and the attacker should be consumed
        public bool AbsorbHit(Projectile attacker)
        {
            if (ShouldIgnore(attacker))
                return false;

            var other = MPDestructibleMissiles.FindInParents<DestructibleMissile>(
                attacker.c_collider != null ? attacker.c_collider.gameObject : attacker.c_go);
            if (other != null)
            {
                ResolveMissileDuel(other, attacker);
                return false;
            }
            return TakeHit(attacker);
        }

        private bool ShouldIgnore(Projectile attacker)
        {
            if (m_destroyed || m_proj == null || !m_proj.m_alive)
                return true;
            if (attacker == null || attacker == m_proj || !attacker.m_alive)
                return true;
            if (attacker.m_owner == m_proj.m_owner)
                return true;
            if (m_proj.m_mp_team != MpTeam.ANARCHY && attacker.m_mp_team == m_proj.m_mp_team)
                return true;
            return !m_hit_by.Add(AttackerKey(attacker));
        }

        private bool TakeHit(Projectile attacker)
        {
            m_hp -= MPDestructibleMissiles.Damage(attacker);
            Settle(this, attacker);
            return !m_destroyed;
        }

        // both missiles take the damage of the weaker one
        private void ResolveMissileDuel(DestructibleMissile other, Projectile attacker)
        {
            other.m_hit_by.Add(AttackerKey(m_proj));   // the mirrored collision event becomes a no op
            float dmg = Mathf.Min(MPDestructibleMissiles.Damage(attacker), MPDestructibleMissiles.Damage(m_proj));
            m_hp -= dmg;
            other.m_hp -= dmg;
            Settle(this, attacker);
            Settle(other, m_proj);
            if (!m_destroyed && !other.m_destroyed)
                IgnoreFurtherContacts(other);
        }

        // explode when hp runs out
        private static void Settle(DestructibleMissile m, Projectile attacker)
        {
            if (m.m_hp > 0f)
                return;
            m.m_destroyed = true;
            MPDestructibleMissiles.Destroy(m.m_proj, attacker);
        }

        private static int AttackerKey(Projectile p)
        {
            return p.m_projectile_id >= 0 ? p.m_projectile_id : p.GetInstanceID();
        }

        // survivors of a mutual hit stop bouncing off each other
        private void IgnoreFurtherContacts(DestructibleMissile other)
        {
            Collider a = m_proj != null ? m_proj.c_collider : null;
            Collider b = other.m_proj != null ? other.m_proj.c_collider : null;
            if (a == null || b == null)
                return;
            Physics.IgnoreCollision(a, b, true);
            m_ignored.Add(b);
            other.m_ignored.Add(a);
        }

        private void RestoreIgnoredCollisions()
        {
            Collider mine = m_proj != null ? m_proj.c_collider : null;
            foreach (var other in m_ignored)
                if (mine != null && other != null &&
                    mine.enabled && mine.gameObject.activeInHierarchy &&
                    other.enabled && other.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(mine, other, false);
            m_ignored.Clear();
        }
    }

    static class MPDestructibleMissiles
    {
        public const int MISSILE_LAYER = 27;   // rp ignore layer
        public static bool Enabled = false;

        //                                         falcon  pod hunt creeper nova   dev  time  vortex
        public static readonly float[] FullDamage = { 22f, 8f, 11f, 16.75f, 120f, 120f, 120f, 56f };

        // default hp is half of what the missile itself deals
        public static readonly float[] DefaultHealth = { 11f, 4f, 5.5f, 8.3525f, 60f, 60f, 60f, 28f };

        public static float[] MissileTypeHealth = (float[])DefaultHealth.Clone();
        private static float[] serverOverrides;

        private static readonly System.Reflection.FieldInfo ProjDamageField =
            AccessTools.Field(typeof(Projectile), "m_damage");

        public static float Damage(Projectile p)
        {
            int idx = MissileIndex(p.m_type);
            return idx >= 0 ? FullDamage[idx] : (float)ProjDamageField.GetValue(p);
        }

        public static void ApplyServerOverrides()
        {
            if (!GameplayManager.IsDedicatedServer() && !Overload.NetworkManager.IsServer())
                return;
            if (serverOverrides == null)
            {
                serverOverrides = new float[8];
                var j = Config.Settings != null ? Config.Settings["missileTypeHealth"] as JObject : null;
                if (j != null)
                    for (int i = 0; i < 8; i++)
                        if (j.TryGetValue(((MissileType)i).ToString(), System.StringComparison.OrdinalIgnoreCase, out JToken tok))
                            serverOverrides[i] = (float)tok;
            }
            for (int i = 0; i < 8; i++)
                if (serverOverrides[i] > 0f)
                    MissileTypeHealth[i] = serverOverrides[i];
        }

        public static T FindInParents<T>(GameObject go) where T : Component
        {
            for (Transform t = go != null ? go.transform : null; t != null; t = t.parent)
            {
                T c = t.GetComponent<T>();
                if (c != null)
                    return c;
            }
            return null;
        }

        public static int MissileIndex(ProjPrefab t)
        {
            switch (t)
            {
                case ProjPrefab.missile_falcon:     return (int)MissileType.FALCON;
                case ProjPrefab.missile_pod:        return (int)MissileType.MISSILE_POD;
                case ProjPrefab.missile_hunter:     return (int)MissileType.HUNTER;
                case ProjPrefab.missile_creeper:    return (int)MissileType.CREEPER;
                case ProjPrefab.missile_smart:      return (int)MissileType.NOVA;
                case ProjPrefab.missile_devastator: return (int)MissileType.DEVASTATOR;
                case ProjPrefab.missile_timebomb:   return (int)MissileType.TIMEBOMB;
                case ProjPrefab.missile_vortex:     return (int)MissileType.VORTEX;
                default:                            return -1;
            }
        }

        // server relayers the missile and tracks its hp
        public static void SetupServer(Projectile proj, int idx)
        {
            GameObject holder = proj.c_collider != null ? proj.c_collider.gameObject : proj.c_go;
            holder.layer = MISSILE_LAYER;
            proj.c_go.layer = MISSILE_LAYER;
            var comp = holder.GetComponent<DestructibleMissile>() ?? holder.AddComponent<DestructibleMissile>();
            comp.Initialize(proj, MissileTypeHealth[idx]);
        }

        public static void SetupClient(Projectile proj)
        {
            GameObject holder = proj.c_collider != null ? proj.c_collider.gameObject : proj.c_go;
            if (holder.layer == MISSILE_LAYER)
                holder.layer = proj.c_go.layer;
        }

        // server side kill, attacker is only set when a missile got shot down
        public static void Destroy(Projectile proj, Projectile attacker = null)
        {
            if (proj == null || !proj.m_alive)
                return;
            if (Server.ProjectileTypeHasLaunchDataSynced(proj.m_type))
            {
                var attackerId = attacker != null && attacker.m_owner_player != null
                    ? attacker.m_owner_player.netId : default(NetworkInstanceId);
                BroadcastDestroy(proj.m_type, proj.m_projectile_id, proj.c_transform.position, attackerId);
            }
            proj.Explode(true);
        }

        public static void BroadcastDestroy(ProjPrefab type, int projId, Vector3 pos, NetworkInstanceId attackerId)
        {
            SendToCapable(MessageTypes.MsgDestroyMissile, new DestroyMissileMessage
            {
                m_proj_type = type,
                m_proj_id = projId,
                m_pos = pos,
                m_attacker_net_id = attackerId
            });
        }

        private static void SendToCapable(short msgType, MessageBase msg)
        {
            foreach (var conn in NetworkServer.connections)
                if (conn != null && MPTweaks.ClientHasTweak(conn.connectionId, "destructiblemissiles"))
                    conn.SendByChannel(msgType, msg, 0);
        }

        // reliable vs unreliable channel potential time difference. keep information a bit longer to potentially resend
        private const float PENDING_TTL = 1f;

        private class PendingDestroy
        {
            public ProjPrefab type;
            public int id;
            public Vector3 pos;
            public float deadline;
        }

        private static readonly List<PendingDestroy> s_pending = new List<PendingDestroy>();

        public static void ApplyOrQueueDestroy(ProjPrefab type, int id, Vector3 pos)
        {
            var u = new PendingDestroy { type = type, id = id, pos = pos };
            if (!TryApply(u))
            {
                u.deadline = Time.unscaledTime + PENDING_TTL;
                s_pending.Add(u);
            }
        }

        private static bool TryApply(PendingDestroy u)
        {
            var proj = ProjectileManager.FindProjectileById(u.type, u.id);
            if (proj == null || !proj.m_alive)
                return false;
            if (proj.c_transform != null)
                proj.c_transform.position = u.pos;
            proj.Explode(true);
            return true;
        }

        public static void ProcessPending()
        {
            if (s_pending.Count == 0)
                return;
            float now = Time.unscaledTime;
            for (int i = s_pending.Count - 1; i >= 0; i--)
                if (TryApply(s_pending[i]) || s_pending[i].deadline < now)
                    s_pending.RemoveAt(i);
        }

        public static void PlayHitConfirm(NetworkInstanceId attackerId)
        {
            var local = GameManager.m_local_player;
            if (local == null || attackerId != local.netId)
                return;
            SFXCueManager.PlayCue2D(SFXCue.impact_energy, 0.8f, 0.25f);
        }
    }

    class DestroyMissileMessage : MessageBase
    {
        public ProjPrefab m_proj_type;
        public int m_proj_id;
        public Vector3 m_pos;
        public NetworkInstanceId m_attacker_net_id;

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((int)m_proj_type);
            writer.Write(m_proj_id);
            writer.Write(m_pos);
            writer.Write(m_attacker_net_id);
        }

        public override void Deserialize(NetworkReader reader)
        {
            m_proj_type = (ProjPrefab)reader.ReadInt32();
            m_proj_id = reader.ReadInt32();
            m_pos = reader.ReadVector3();
            m_attacker_net_id = reader.ReadNetworkId();
        }
    }

    [HarmonyPatch(typeof(GameManager), "Awake")]
    class MPDestructibleMissiles_GameManager_Awake
    {
        // 11 robots 
        // 13 projectiles 
        // 14 level 
        // 16 ships 
        // 17 doors 
        // 18 destroyables 
        // 23 solid 
        // 24 area effects 
        // 26 lava 
        // 31 monsterball
        static readonly int[] RelevantLayers = { 11, 13, 14, 16, 17, 18, 23, 24, 26, 31 };

        static void Postfix()
        {
            Physics.IgnoreLayerCollision(MPDestructibleMissiles.MISSILE_LAYER, MPDestructibleMissiles.MISSILE_LAYER, false);
            foreach (int layer in RelevantLayers)
                Physics.IgnoreLayerCollision(MPDestructibleMissiles.MISSILE_LAYER, layer, false);

            MPDestructibleMissiles.ApplyServerOverrides();
        }
    }

    [HarmonyPatch(typeof(GameManager), "Update")]
    class MPDestructibleMissiles_GameManager_Update
    {
        static void Postfix()
        {
            MPDestructibleMissiles.ProcessPending();
        }
    }

    [HarmonyPatch(typeof(Projectile), "ProcessCollision")]
    class MPDestructibleMissiles_Projectile_ProcessCollision
    {
        static bool Prefix(Projectile __instance, GameObject collider)
        {
            if (!MPDestructibleMissiles.Enabled)
                return true;

            if (__instance.c_go.layer == MPDestructibleMissiles.MISSILE_LAYER && collider.layer == 13)
                return false;

            if (collider.layer == MPDestructibleMissiles.MISSILE_LAYER && Overload.NetworkManager.IsServer())
            {
                var missile = MPDestructibleMissiles.FindInParents<DestructibleMissile>(collider);
                if (missile != null)
                {
                    if (missile.AbsorbHit(__instance))
                        MPDestructibleMissiles.Destroy(__instance);
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Projectile), "OnTriggerEnter")]
    class MPDestructibleMissiles_Projectile_OnTriggerEnter
    {
        static bool Prefix(Projectile __instance, Collider other)
        {
            if (!MPDestructibleMissiles.Enabled || other == null || !Overload.NetworkManager.IsServer())
                return true;

            var missile = MPDestructibleMissiles.FindInParents<DestructibleMissile>(other.gameObject);
            if (missile == null)
                return true;
            if (missile.AbsorbHit(__instance))
                MPDestructibleMissiles.Destroy(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Projectile), "Fire")]
    class MPDestructibleMissiles_Projectile_Fire
    {
        static void Postfix(Projectile __instance)
        {
            int idx = MPDestructibleMissiles.MissileIndex(__instance.m_type);
            if (idx < 0)
                return;
            if (MPDestructibleMissiles.Enabled && GameplayManager.IsMultiplayerActive && Overload.NetworkManager.IsServer())
                MPDestructibleMissiles.SetupServer(__instance, idx);
            else
                MPDestructibleMissiles.SetupClient(__instance);
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPDestructibleMissiles_Client_RegisterHandlers
    {
        static void Postfix(UnityEngine.Networking.NetworkClient ___m_network_client)
        {
            ___m_network_client.RegisterHandler(MessageTypes.MsgDestroyMissile, OnDestroyMissile);
        }

        static void OnDestroyMissile(NetworkMessage netMsg)
        {
            var msg = netMsg.ReadMessage<DestroyMissileMessage>();
            MPDestructibleMissiles.PlayHitConfirm(msg.m_attacker_net_id);
            MPDestructibleMissiles.ApplyOrQueueDestroy(msg.m_proj_type, msg.m_proj_id, msg.m_pos);
        }
    }
}
