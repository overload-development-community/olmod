using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{

    public class MPDeathReview
    {
        public static DeathReviewMessage lastDeathReview;
        public static int GetTextureIndex(ProjPrefab prefab)
        {
            switch (prefab)
            {
                case ProjPrefab.proj_vortex:
                    return 27;
                case ProjPrefab.proj_thunderbolt:
                    return 32;
                case ProjPrefab.proj_shotgun:
                    return 29;
                case ProjPrefab.proj_reflex:
                    return 28;
                case ProjPrefab.proj_impulse:
                    return 26;
                case ProjPrefab.proj_flak_cannon:
                    return 31;
                case ProjPrefab.proj_driller_mini:
                case ProjPrefab.proj_driller:
                    return 30;
                case ProjPrefab.proj_beam:
                    return 33;
                case ProjPrefab.missile_creeper:
                    return 107;
                case ProjPrefab.missile_devastator:
                case ProjPrefab.missile_devastator_mini:
                    return 109;
                case ProjPrefab.missile_falcon:
                    return 104;
                case ProjPrefab.missile_hunter:
                    return 106;
                case ProjPrefab.missile_pod:
                    return 105;
                case ProjPrefab.missile_smart:
                case ProjPrefab.missile_smart_mini:
                    return 108;
                case ProjPrefab.missile_timebomb:
                    return 110;
                case ProjPrefab.missile_vortex:
                    return 111;
                default:
                    return 34;
            }
        }
    }

    static class ServerDamageLog
    {
        public static Dictionary<Player, List<DamageEvent>> damageEvents;

        public static void AddDamage(Player player, DamageEvent de)
        {
            if (!damageEvents.ContainsKey(player))
                damageEvents.Add(player, new List<DamageEvent>());

            damageEvents[player].Add(de);
        }

        public static void Clear(Player player)
        {
            damageEvents[player] = new List<DamageEvent>();
        }

        public static Dictionary<NetworkInstanceId, List<DamageSummary>> GetSummaryForDeadPlayer(Player player)
        {
            Dictionary<NetworkInstanceId, List<DamageSummary>> players = new Dictionary<NetworkInstanceId, List<DamageSummary>>();
            foreach (var dmgPlayer in damageEvents)
            {
                players.Add(dmgPlayer.Key.netId, new List<DamageSummary>());
                foreach (var dmgStats in dmgPlayer.Value.GroupBy(x => x.weapon))
                {
                    players[dmgPlayer.Key.netId].Add(new DamageSummary { weapon = dmgStats.Key, damage = dmgStats.Sum(x => x.damage) });
                }
            }

            return players;
        }
    }

    class DamageEvent
    {
        public float time;
        public Player attacker;
        public ProjPrefab weapon;
        public float damage;
    }

    public class DamageSummary
    {
        public ProjPrefab weapon;
        public float damage;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPDeathReview_Client_RegisterHandlers
    {
        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgDeathReview, OnDeathReview);
        }

        private static void OnDeathReview(NetworkMessage rawMsg)
        {
            DeathReviewMessage rs = rawMsg.ReadMessage<DeathReviewMessage>();
            MPDeathReview.lastDeathReview = rs;
        }
    }

    public class DeathReviewMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write(m_killer_player_id); // Killer
            writer.Write(m_assister_player_id); // Assister
            writer.WritePackedUInt32((uint)players.Count);
            foreach (var player in players)
            {
                writer.Write(player.Key);
                writer.WritePackedUInt32((uint)player.Value.Count);
                foreach (var damageType in player.Value)
                {
                    writer.WritePackedUInt32((uint)damageType.weapon);
                    writer.Write(damageType.damage);
                }
            }
        }

        public override void Deserialize(NetworkReader reader)
        {
            players = new Dictionary<NetworkInstanceId, List<DamageSummary>>();
            var version = reader.ReadByte();
            m_killer_player_id = reader.ReadString();
            m_assister_player_id = reader.ReadString();
            var numPlayers = reader.ReadPackedUInt32();
            for (int i = 0; i < numPlayers; i++)
            {
                NetworkInstanceId m_player_id = reader.ReadNetworkId();
                players[m_player_id] = new List<DamageSummary>();
                uint damageCount = reader.ReadPackedUInt32();
                for (int j = 0; j < damageCount; j++)
                {
                    ProjPrefab weapon = (ProjPrefab)reader.ReadPackedUInt32();
                    float damage = reader.ReadSingle();
                    players[m_player_id].Add(new DamageSummary { weapon = weapon, damage = damage });
                }
            }
        }

        public Dictionary<NetworkInstanceId, List<DamageSummary>> players;
        public string m_killer_player_id;
        public string m_assister_player_id;
        public NetworkInstanceId m_killer_id
        {
            get
            {
                NetworkInstanceId id = new NetworkInstanceId();
                if (!String.IsNullOrEmpty(m_killer_player_id))
                {
                    id = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.m_mp_player_id == m_killer_player_id).netId;
                }
                return id;
            }
        }

        public NetworkInstanceId m_assister_id
        {
            get
            {
                NetworkInstanceId id = new NetworkInstanceId();
                if (!String.IsNullOrEmpty(m_killer_player_id))
                {
                    id = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.m_mp_player_id == m_assister_player_id).netId;
                }
                return id;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpDeathOverlay")]
    class MPDeathReview_UIElement_DrawMpDeathOverlay
    {
        static void Postfix(UIElement __instance)
        {
            Vector2 pos = new Vector2();
            pos.x = UIManager.UI_RIGHT - 100f;
            pos.y = UIManager.UI_TOP + 400f;
            float col1 = -380f;
            float col2 = -40f;

            Player killer = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.m_mp_player_id == MPDeathReview.lastDeathReview.m_killer_player_id);
            Player assister = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.m_mp_player_id == MPDeathReview.lastDeathReview.m_assister_player_id);

            float w = 50f;
            Color c = NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) ? MPTeams.TeamColor(GameManager.m_local_player.m_mp_team, 0) : UIManager.m_col_damage;
            float m_alpha = 0.8f;

            foreach (var g2 in MPDeathReview.lastDeathReview.players.SelectMany(x => x.Value).GroupBy(x => x.weapon).OrderByDescending(x => x.Sum(y => y.damage)))
            {
                __instance.DrawDigitsVariable(pos + Vector2.right * w, (int)g2.Sum(y => y.damage), 0.7f, StringOffset.RIGHT, c, m_alpha);
                UIManager.DrawSpriteUI(pos, 0.3f, 0.3f, c, m_alpha, MPDeathReview.GetTextureIndex(g2.Key));
                pos.y += 32f;
            }
            pos.y += 100f;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    class MPDeathReview_PlayerShip_ApplyDamage
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(GameplayManager), "AddStatsPlayerDamage"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldarg_0)
                {
                    state = 2;
                    yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = code.labels };
                    yield return new CodeInstruction(OpCodes.Ldarg_1); //DamageInfo
                    yield return new CodeInstruction(OpCodes.Ldloc_3); // num2 float
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPDeathReview_PlayerShip_ApplyDamage), "AddDamageEvent"));
                    code.labels = null;
                }

                yield return code;
            }
        }

        static void AddDamageEvent(PlayerShip playerShip, DamageInfo di, float damage_scaled)
        {
            if (!Overload.NetworkManager.IsHeadless() || di.damage == 0f ||
                playerShip.m_death_stats_recorded || playerShip.m_cannot_die || playerShip.c_player.m_invulnerable)
                return;

            float hitpoints = playerShip.c_player.m_hitpoints;
            DamageEvent de = new DamageEvent
            {
                time = NetworkMatch.m_match_elapsed_seconds,
                attacker = di.owner?.GetComponent<Player>(),
                weapon = di.weapon,
                damage = (hitpoints - damage_scaled <= 0f) ? hitpoints : damage_scaled
            };
            ServerDamageLog.AddDamage(playerShip.c_player, de);
        }
    }

    // Server call
    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
    class MPDeathReview_Player_OnKilledByPlayer
    {
        static void ReportPlayerDeath(Player player, string m_killer_id, string m_assister_id)
        {
            // Send stats to client and clear out
            NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgDeathReview, new DeathReviewMessage { m_killer_player_id = m_killer_id, m_assister_player_id = m_assister_id, players = ServerDamageLog.GetSummaryForDeadPlayer(player) });
            ServerDamageLog.Clear(player);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Telemetry), "ReportPlayerDeath"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 9);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 8);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPDeathReview_Player_OnKilledByPlayer), "ReportPlayerDeath"));
                    continue;
                }
                yield return code;
            }
        }
    }

    // Server call
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPDeathReview_NetworkMatch_InitBeforeEachMatch
    {
        private static void Postfix()
        {
            ServerDamageLog.damageEvents = new Dictionary<Player, List<DamageEvent>>();
        }
    }
}
