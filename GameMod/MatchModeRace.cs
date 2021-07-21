using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    public static class Race
    {
        public static List<RacePlayer> Players = new List<RacePlayer>();
        public static List<ArenaSpawn> spawns = new List<ArenaSpawn>();
        public static List<RobotManager.MultiplayerSpawnableWeapon> initialWeapons = new List<RobotManager.MultiplayerSpawnableWeapon>();
        public static List<RobotManager.MultiplayerSpawnableMissile> initialMissiles = new List<RobotManager.MultiplayerSpawnableMissile>();
        public static List<MPTags.MultiplayerSpawnablePowerup> initialPowerups = new List<MPTags.MultiplayerSpawnablePowerup>();
        public static ItemPrefab GetPrefabFromType(ItemType itemType)
        {
            switch (itemType)
            {
                case ItemType.KEY_SECURITY:
                    return ItemPrefab.entity_item_security_key;
                case ItemType.LOG_ENTRY:
                    return ItemPrefab.entity_item_log_entry;
                case ItemType.UPGRADE_L1:
                    return ItemPrefab.entity_item_upgrade_L1;
                case ItemType.UPGRADE_L2:
                    return ItemPrefab.entity_item_upgrade_L2;
                case ItemType.POWERUP_SHIELD:
                    return ItemPrefab.entity_item_shields;
                case ItemType.POWERUP_AMMO:
                    return ItemPrefab.entity_item_ammo;
                case ItemType.POWERUP_ALIEN_ORB:
                    return ItemPrefab.entity_item_alien_orb;
                case ItemType.TEMP_CLOAK:
                    return ItemPrefab.entity_item_cloak;
                case ItemType.TEMP_RAPID:
                    return ItemPrefab.entity_item_rapid;
                case ItemType.POWERUP_ENERGY:
                    return ItemPrefab.entity_item_energy;
                case ItemType.WEAPON_DRILLER:
                    return ItemPrefab.entity_item_driller;
                case ItemType.WEAPON_CYCLONE:
                    return ItemPrefab.entity_item_cyclone;
                case ItemType.WEAPON_FLAK:
                    return ItemPrefab.entity_item_flak;
                case ItemType.WEAPON_LANCER:
                    return ItemPrefab.entity_item_lancer;
                case ItemType.WEAPON_REFLEX:
                    return ItemPrefab.entity_item_reflex;
                case ItemType.WEAPON_SHOTGUN:
                    return ItemPrefab.entity_item_crusher;
                case ItemType.WEAPON_IMPULSE:
                    return ItemPrefab.entity_item_impulse;
                case ItemType.WEAPON_THUNDERBOLT:
                    return ItemPrefab.entity_item_thunderbolt;
                case ItemType.MISSILE_CREEPER:
                    return ItemPrefab.entity_item_creeper;
                case ItemType.MISSILE_DEVASTATOR:
                    return ItemPrefab.entity_item_devastator;
                case ItemType.MISSILE_FALCON:
                    return ItemPrefab.entity_item_falcon4pack;
                case ItemType.MISSILE_HUNTER:
                    return ItemPrefab.entity_item_hunter4pack;
                case ItemType.MISSILE_POD:
                    return ItemPrefab.entity_item_missile_pod;
                case ItemType.MISSILE_SMART:
                    return ItemPrefab.entity_item_nova;
                case ItemType.MISSILE_TIMEBOMB:
                    return ItemPrefab.entity_item_timebomb;
                case ItemType.MISSILE_VORTEX:
                    return ItemPrefab.entity_item_vortex;
                default:
                    return ItemPrefab.entity_item_shields;
            }
        }
        public static void DrawMpMiniScoreboard(ref Vector2 pos, UIElement uie)
        {
            int match_time_remaining = NetworkMatch.m_match_time_remaining;
            int num = (int)NetworkMatch.m_match_elapsed_seconds;
            pos.y -= 15f;
            uie.DrawDigitsTime(pos + Vector2.right * 95f, (float)match_time_remaining, 0.45f, (num <= 10 || match_time_remaining >= 10) ? UIManager.m_col_ui2 : UIManager.m_col_em5, uie.m_alpha, false);
            pos.y += 25f;

            pos.y -= 12f;
            pos.x += 6f;
            UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ui0, uie.m_alpha, 21);
            pos.y += 10f;
            Vector2 temp_pos;
            temp_pos.x = pos.x;
            temp_pos.x = temp_pos.x + 90f;
            for (int i = 0; i < Race.Players.Count; i++)
            {
                temp_pos.y = pos.y;
                Player player = Race.Players[i].player;
                if (player && !player.m_spectator)
                {
                    Color color = (!player.isLocalPlayer) ? UIManager.m_col_ui2 : UIManager.m_col_hi4;
                    float num2 = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    uie.DrawDigitsVariable(temp_pos, Race.Players[i].Laps.Count(), 0.4f, StringOffset.RIGHT, color, uie.m_alpha * num2);
                    temp_pos.x = temp_pos.x - 40f;
                    uie.DrawStringSmall(player.m_mp_name, temp_pos, 0.35f, StringOffset.RIGHT, color, num2, -1f);
                    temp_pos.x = temp_pos.x + 10f;
                    temp_pos.x = temp_pos.x + 30f;
                    pos.y += 16f;
                }
            }
            pos.y -= 6f;
            UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ui0, uie.m_alpha, 21);
            pos.x -= 6f;
            pos.y -= 6f;
        }
        public static bool ItemPickupUpgradePoint(bool super, Player player)
        {
            bool flag = false;
            if (super)
            {
                var st = NetworkMatch.RandomAllowedSuperSpawn();
                switch (st)
                {
                    case SuperType.CLOAK:
                        flag = player.StartCloak();
                        break;
                    case SuperType.CREEPER:
                        player.UnlockMissile(MissileType.CREEPER);
                        flag = player.AddMissileAmmo(Race.GetAmmoAmount(MissileType.CREEPER, super), MissileType.CREEPER, false, super);
                        break;
                    case SuperType.DEVASTATOR:
                        player.UnlockMissile(MissileType.DEVASTATOR);
                        flag = player.AddMissileAmmo(Race.GetAmmoAmount(MissileType.DEVASTATOR, super), MissileType.DEVASTATOR, false, super);
                        break;
                    case SuperType.FALCON:
                        player.UnlockMissile(MissileType.FALCON);
                        flag = player.AddMissileAmmo(Race.GetAmmoAmount(MissileType.FALCON, super), MissileType.FALCON, false, super);
                        break;
                    case SuperType.HUNTER:
                        player.UnlockMissile(MissileType.HUNTER);
                        flag = player.AddMissileAmmo(Race.GetAmmoAmount(MissileType.HUNTER, super), MissileType.HUNTER, false, super);
                        break;
                    case SuperType.INVULNERABILITY:
                        flag = player.StartInvul(30, false);
                        break;
                    case SuperType.MISSILE_POD:
                        player.UnlockMissile(MissileType.MISSILE_POD);
                        flag = player.AddMissileAmmo(10, MissileType.MISSILE_POD, false, super);
                        break;
                    case SuperType.NOVA:
                        player.UnlockMissile(MissileType.NOVA);
                        flag = player.AddMissileAmmo(3, MissileType.NOVA, false, super);
                        break;
                    case SuperType.OVERDRIVE:
                        flag = player.StartRapid();
                        break;
                    case SuperType.TIMEBOMB:
                        player.UnlockMissile(MissileType.TIMEBOMB);
                        flag = player.AddMissileAmmo(3, MissileType.TIMEBOMB, false, super);
                        break;
                    case SuperType.VORTEX:
                        player.UnlockMissile(MissileType.VORTEX);
                        flag = player.AddMissileAmmo(3, MissileType.VORTEX, false, super);
                        break;
                    default:
                        flag = false;
                        break;
                }
            }
            else
            {
                Dictionary<int, float> odds = new Dictionary<int, float>();
                foreach (var p in Race.initialWeapons)
                {
                    if (p.percent > 0f)
                    {
                        odds.Add(p.type + 1, p.percent * 100);
                    }
                }
                foreach (var m in Race.initialMissiles)
                {
                    if (NetworkMatch.IsMissileAllowed((MissileType)m.type) && m.percent > 0f)
                    {
                        odds.Add(m.type * -1 - 1, m.percent * 100);
                    }
                }
                foreach (var p in Race.initialPowerups)
                {
                    if (p.percent > 0f)
                    {
                        odds.Add(p.type + 100, p.percent * 100);
                    }
                }
                float total = odds.Sum(x => x.Value);
                foreach (var o in odds.ToArray())
                {
                    odds[o.Key] = (odds[o.Key] / total);
                }
                float rn = UnityEngine.Random.Range(0f, 1f);
                int selected = 0;
                float sum = 0f;
                foreach (var o in odds.OrderBy(x => x.Value))
                {
                    sum += o.Value;
                    if (rn <= sum)
                    {
                        selected = o.Key;
                        break;
                    }
                }
                if (selected > 0 && selected <= 100)
                {
                    // Primary
                    var wt = (WeaponType)selected - 1;
                    flag = player.UnlockWeapon(wt, false, true);
                }
                else if (selected > 0 && selected > 100)
                {
                    // Powerup
                    var pt = (MPTags.PowerupType)selected - 100;
                    switch (pt)
                    {
                        case MPTags.PowerupType.ALIENORB:
                            flag |= player.AddArmor(GameplayManager.dl_powerup_shields[GameplayManager.DifficultyLevel] * 0.5f, true, false);
                            flag |= player.AddEnergy(GameplayManager.dl_powerup_energy[GameplayManager.DifficultyLevel] * 0.25f, true, false);
                            flag |= player.AddAmmo((int)(GameplayManager.dl_powerup_ammo[GameplayManager.DifficultyLevel] * 0.2f), true, false, false);
                            if (flag)
                            {
                                player.CallTargetAddHUDMessage(player.connectionToClient, Loc.LSN("ARMOR, ENERGY, AND AMMO INCREASED"), -1, false);
                            }
                            break;
                        case MPTags.PowerupType.AMMO:
                            flag = player.AddAmmo((!super) ? 50 : 1000, false, super, false);
                            break;
                        case MPTags.PowerupType.ENERGY:
                            flag = player.AddEnergy(GameplayManager.dl_powerup_energy[GameplayManager.DifficultyLevel], false, false);
                            break;
                        case MPTags.PowerupType.HEALTH:
                            flag = player.AddArmor(GameplayManager.dl_powerup_shields[GameplayManager.DifficultyLevel], false, false);
                            break;
                        default:
                            Debug.Log("Undefined MPTags.PowerupType pickup");
                            flag = false;
                            break;
                    }
                }
                else if (selected < 0)
                {
                    // Secondary
                    var mt = (MissileType)(Math.Abs(selected) - 1);
                    player.UnlockMissile(mt);
                    flag = player.AddMissileAmmo(Race.GetAmmoAmount(mt, super), mt, false, super);
                }
                else
                {
                    flag = false;
                }

            }

            return flag;
        }
        public static int GetAmmoAmount(MissileType mt, bool super)
        {
            switch (mt)
            {
                case MissileType.CREEPER:
                    return super ? 20 : 5;
                case MissileType.DEVASTATOR:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 1;
                case MissileType.FALCON:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 4;
                case MissileType.HUNTER:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 3;
                case MissileType.MISSILE_POD:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 15;
                case MissileType.NOVA:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 1;
                case MissileType.TIMEBOMB:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 1;
                case MissileType.VORTEX:
                    return super ? Player.SUPER_MISSILE_AMMO_MP[(int)mt] : 2;
                default:
                    return 0;
            }
        }
        public static void SendJoinUpdate(Player player)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgSetFullMatchState, new FullRaceStateMessage());
        }
        public static void Sort()
        {
            Race.Players = Race.Players.OrderByDescending(x => x.Laps.Count()).ThenBy(x => x.Laps.Sum(y => y.Time)).ToList();
        }

        public class ArenaSpawn
        {
            public ItemPrefab prefab;
            public float respawn_length;
            public float respawn_timer;
            public bool active;
            public GameObject gameObject;
            public Vector3 position;
            public bool super;
        }
    }

    public class RacePlayer : NetworkBehaviour
    {

        public Player player;
        public List<Lap> Laps;
        public Vector3? LastDeathVec;
        public bool lastTriggerForward;

        public RacePlayer(Player _player)
        {
            player = _player;
            Laps = new List<Lap>();
            lastTriggerForward = true;
        }

        public class Lap
        {
            public uint Num { get; set; }
            public float Time { get; set; }
        }

    }

    public class PlayerLapMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)1); // version
            writer.Write(m_player_id);
            writer.Write(lapTime);
            writer.Write(lapNum);
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            lapTime = reader.ReadSingle();
            lapNum = reader.ReadPackedUInt32();
        }
        public NetworkInstanceId m_player_id;
        public uint lapNum;
        public float lapTime;
    }

    public class FullRaceStateMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write(m_match_elapsed_seconds);
            writer.WritePackedUInt32((uint)Race.Players.Count);
            foreach (var rp in Race.Players)
            {
                writer.Write(rp.player.c_player_ship.netId.Value);
                writer.WritePackedUInt32((uint)rp.Laps.Count);
                foreach (var lap in rp.Laps)
                {
                    writer.WritePackedUInt32((uint)lap.Num);
                    writer.Write(lap.Time);
                }
            }
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_match_elapsed_seconds = reader.ReadSingle();
            var numPlayers = reader.ReadPackedUInt32();
            for (int i = 0; i < numPlayers; i++)
            {
                uint netId = reader.ReadUInt32();
                RacePlayer rp = Race.Players.FirstOrDefault(x => x.player.c_player_ship.netId.Value == netId);
                uint lapCount = reader.ReadPackedUInt32();
                for (int j = 0; j < lapCount; j++)
                {
                    uint lapNum = reader.ReadPackedUInt32();
                    float lapTime = reader.ReadSingle();
                    rp.Laps.Add(new RacePlayer.Lap { Num = lapNum, Time = lapTime });
                }
            }
        }

        public float m_match_elapsed_seconds;
    }

    [HarmonyPatch(typeof(NetworkMatch), "PowerupLevelStart")]
    class MatchModeRace_NetworkMatch_PowerupLevelStart
    {
        static void Prefix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            Race.initialWeapons.Clear();
            Race.initialMissiles.Clear();
            Race.initialPowerups.Clear();
            RobotManager.m_multiplayer_spawnable_weapons.ForEach(x => Race.initialWeapons.Add(new RobotManager.MultiplayerSpawnableWeapon { type = x.type, count = x.count, percent = x.percent }));
            RobotManager.m_multiplayer_spawnable_missiles.ForEach(x => Race.initialMissiles.Add(new RobotManager.MultiplayerSpawnableMissile { type = x.type, percent = x.percent }));
            MPTags.m_multiplayer_spawnable_powerups.ForEach(x => Race.initialPowerups.Add(new MPTags.MultiplayerSpawnablePowerup { type = x.type, percent = x.percent }));
        }
    }

    [HarmonyPatch(typeof(NetworkSpawnPlayer), "SetMultiplayerLoadout")]
    class MatchModeRace_Player_SetMultiplayerLoadout
    {
        static void Prefix()
        {
            if (GameplayManager.IsMultiplayerActive && MPModPrivateData.MatchMode == ExtMatchMode.RACE)
            {
                Player.MP_DEFAULT_MISSILE_AMMO[(int)MissileType.CREEPER] = 5;
                Player.SUPER_MISSILE_AMMO_MP[(int)MissileType.CREEPER] = 20;
            }
            else
            {
                Player.MP_DEFAULT_MISSILE_AMMO[(int)MissileType.CREEPER] = 6;
                Player.SUPER_MISSILE_AMMO_MP[(int)MissileType.CREEPER] = 40;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "StartDying")]
    class MatchModeRace_PlayerShip_StartDying
    {
        static void Postfix(Vector3 dir, PlayerShip __instance)
        {
            if (GameplayManager.IsMultiplayerActive && MPModPrivateData.MatchMode == ExtMatchMode.RACE)
            {
                var rp = Race.Players.FirstOrDefault(x => x.player == __instance.c_player);
                rp.LastDeathVec = __instance.c_transform.position;
            }
        }
    }

    [HarmonyPatch(typeof(NetworkSpawnPlayer), "Respawn")]
    class NetworkSpawnPlayer_Respawn
    {
        static bool Prefix(PlayerShip player_ship)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            foreach (var rp1 in Race.Players)
            {
                // Check if player has an associated death and respawn at location, otherwise normal algo
                if (rp1.player.netId.Value == player_ship.c_player.netId.Value && rp1.LastDeathVec.HasValue)
                {
                    NetworkSpawnPlayer.StartSpawnInvul(player_ship.c_player);
                    player_ship.c_player.m_input_deficit = 0;
                    Vector3 pos = rp1.LastDeathVec.Value;
                    Quaternion rot = Quaternion.identity;
                    TriggerWindTunnel[] wts = UnityEngine.Object.FindObjectsOfType<TriggerWindTunnel>();
                    foreach (var wt in wts)
                    {
                        var c = wt.gameObject.GetComponent<BoxCollider>();
                        bool flag = (c.ClosestPoint(pos) - pos).sqrMagnitude < Mathf.Epsilon * Mathf.Epsilon;
                        if (flag)
                        {
                            rot = Quaternion.LookRotation(wt.transform.forward);
                            break;
                        }
                    }
                    Server.RespawnPlayer(player_ship.c_player, pos, rot);
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "MaybeEndScore")]
    class NetworkMatch_MaybeEndScore
    {
        static bool Prefix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            // Defer to score limit on no lap limit
            if (MPModPrivateData.LapLimit == 0)
                return true;

            if (Race.Players.Any(x => x.Laps.Count() >= MPModPrivateData.LapLimit))
                NetworkMatch.End();

            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "MaybeEndTimer")]
    class NetworkMatch_MaybeEndTimer
    {
        static bool Prefix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            // Defer to time limit on no lap limit
            if (MPModPrivateData.LapLimit == 0)
                return true;

            if (Race.Players.Any(x => x.Laps.Count() >= MPModPrivateData.LapLimit))
                NetworkMatch.End();

            return false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MatchModeRace_NetworkMatch_InitBeforeEachMatch
    {
        private static void Postfix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            Race.Players.Clear();
            Race.spawns.Clear();
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MatchModeRace_Client_RegisterHandlers
    {
        private static void OnLapCompleted(NetworkMessage rawMsg)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            Race.Sort();
            PlayerLapMessage plm = rawMsg.ReadMessage<PlayerLapMessage>();
            var rp = Race.Players.FirstOrDefault(x => x.player.netId.Value == plm.m_player_id.Value);
            rp.Laps.Add(new RacePlayer.Lap { Num = plm.lapNum, Time = plm.lapTime });
            if (GameManager.m_local_player == rp.player)
            {
                var lastLap = TimeSpan.FromSeconds(rp.Laps.LastOrDefault().Time);
                var personalBest = TimeSpan.FromSeconds(rp.Laps.Min(x => x.Time));
                var matchBest = TimeSpan.FromSeconds(Race.Players.SelectMany(x => x.Laps).Min(y => y.Time));
                GameplayManager.AddHUDMessage($"Last Lap ({lastLap.Minutes:0}:{lastLap.Seconds:00}.{lastLap.Milliseconds:000}), Personal Best ({personalBest.Minutes:0}:{personalBest.Seconds:00}.{personalBest.Milliseconds:000}), Match Best ({matchBest.Minutes:0}:{matchBest.Seconds:00}.{matchBest.Milliseconds:000})", -1, true);
            }
            Race.Sort();
        }

        private static void OnSetFullMatchState(NetworkMessage rawMsg)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            FullRaceStateMessage rs = rawMsg.ReadMessage<FullRaceStateMessage>();
            Race.Sort();
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgLapCompleted, OnLapCompleted);
            Client.GetClient().RegisterHandler(MessageTypes.MsgSetFullMatchState, OnSetFullMatchState);
        }
    }

    [HarmonyPatch(typeof(TriggerBase), "OnTrigger")]
    public class MatchModeRace_TriggerBase_OnTrigger
    {
        static bool Prefix(TriggerBase __instance, Collider other, ref bool ___m_has_triggered, ref Single ___m_repeat_timer)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            if (__instance.m_one_time && ___m_has_triggered)
            {
                return false;
            }
            if (other.gameObject.layer == 12 && !__instance.m_player_weapons)
            {
                return false;
            }
            foreach (GameObject gameObject in __instance.c_go_link)
            {
                gameObject.SendMessage("ActivateScriptLink", null, SendMessageOptions.DontRequireReceiver);
            }
            ___m_has_triggered = true;
            ___m_repeat_timer = __instance.m_repeat_delay;

            if (Overload.NetworkManager.IsServer())
            {
                var playerShip = other.GetComponent<PlayerShip>();
                if (playerShip && !playerShip.m_dying && NetworkMatch.m_match_state == MatchState.PLAYING)
                {
                    var rp = Race.Players.FirstOrDefault(x => x.player.netId.Value == playerShip.c_player.netId.Value);
                    Vector3 direction = other.transform.position - __instance.transform.position;
                    if (Vector3.Dot(__instance.transform.forward, direction) > 0)
                    {
                        rp.lastTriggerForward = false;
                        playerShip.c_player.CallTargetAddHUDMessage(playerShip.c_player.connectionToClient, "WRONG WAY!", -1, false);
                    }
                    if (Vector3.Dot(__instance.transform.forward, direction) < 0)
                    {
                        var lapTime = NetworkMatch.m_match_elapsed_seconds - (rp.Laps.Count() == 0 ? 0 : rp.Laps.Sum(x => x.Time));

                        if (rp.lastTriggerForward && lapTime > 4f)
                        {
                            NetworkServer.SendToAll(MessageTypes.MsgLapCompleted, new PlayerLapMessage { m_player_id = playerShip.c_player.netId, lapNum = (uint)rp.Laps.Count() + 1, lapTime = lapTime });
                        }
                        rp.lastTriggerForward = true;
                    }
                    if (Vector3.Dot(__instance.transform.forward, direction) == 0)
                    {
                        rp.lastTriggerForward = false;
                        playerShip.c_player.CallTargetAddHUDMessage(playerShip.c_player.connectionToClient, "SIDE", -1, false);

                    }
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Overload.NetworkManager), "AddPlayer")]
    class NetworkManager_AddPlayer
    {
        static void Postfix(Player player)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            if (!Race.Players.Any(x => x.player == player))
                Race.Players.Add(new RacePlayer(player));
        }
    }

    [HarmonyPatch(typeof(Overload.NetworkManager), "RemovePlayer")]
    class NetworkManager_RemovePlayer
    {
        static void Postfix(Player player)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            foreach (var rp in Race.Players.ToArray())
            {
                if (rp.player.GetInstanceID() == player.GetInstanceID())
                    Race.Players.Remove(rp);
            }
        }
    }

    [HarmonyPatch(typeof(UIManager), "DrawMultiplayerNames")]
    class MatchModeRace_UIManager_DrawMultiplayerNames
    {
        static bool Prefix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            Vector2 zero = Vector2.zero;
            PlayerShip player_ship = GameManager.m_player_ship;
            UIManager.mp_local_orient = player_ship.c_transform.localRotation;
            Vector3 forward = player_ship.c_camera_transform.forward;
            Vector3 c_transform_position = player_ship.c_transform_position;
            bool flag = NetworkMatch.IsTeamMode(NetworkMatch.GetMode());
            int count = Overload.NetworkManager.m_Players.Count;
            for (int i = 0; i < count; i++)
            {
                Player player = Overload.NetworkManager.m_Players[i];
                if (!player.m_spectator)
                {
                    player.m_mp_data.visible = false;
                    if (!player.isLocalPlayer)
                    {
                        Vector3 localPosition = player.c_player_ship.c_transform.localPosition;
                        player.m_mp_data.pos = localPosition;
                        Vector3 vector;
                        vector.x = localPosition.x - c_transform_position.x;
                        vector.y = localPosition.y - c_transform_position.y;
                        vector.z = localPosition.z - c_transform_position.z;
                        player.m_mp_data.dist = Mathf.Max(0.1f, vector.magnitude);
                        if (player.m_hitpoints > 0f && !player.m_cloaked)
                        {
                            player.m_mp_data.visible = true;
                        }
                    }
                    if (player.m_mp_data.visible)
                    {
                        player.m_mp_data.color = UIManager.ChooseMpColor(player.m_mp_team);
                        player.m_mp_data.vis_fade = ((player.m_mp_data.vis_fade >= 1f) ? 1f : (player.m_mp_data.vis_fade + RUtility.FRAMETIME_UI * 2.5f));
                    }
                    else
                    {
                        player.m_mp_data.vis_fade = ((player.m_mp_data.vis_fade <= 0f) ? 0f : (player.m_mp_data.vis_fade - RUtility.FRAMETIME_UI * 5f));

                    }

                    int quad_index2 = UIManager.m_quad_index;
                    zero.y = -80f / player.m_mp_data.dist;
                    UIManager.DrawMpPlayerName(player, zero);
                    UIManager.DrawMpPlayerArrow(player, zero);
                    UIManager.PreviousQuadsTransformPlayer(player, quad_index2);
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class MatchModeRace_UIElement_DrawHUD
    {
        static void Postfix(UIElement __instance)
        {
            if (!GameplayManager.IsMultiplayerActive || MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            if (!GameplayManager.ShowHud)
                return;

            var rp = Race.Players.FirstOrDefault(x => x.player == GameManager.m_player_ship.c_player);
            var current = NetworkMatch.m_match_elapsed_seconds - (rp.Laps.Any() ? rp.Laps.Sum(l => l.Time) : 0f);
            var last = TimeSpan.FromSeconds(rp.Laps.Any() ? rp.Laps.LastOrDefault().Time : 0f);
            var best = TimeSpan.FromSeconds(rp.Laps.Any() ? rp.Laps.Min(x => x.Time) : 0f);
            var leader = TimeSpan.FromSeconds(Race.Players.SelectMany(x => x.Laps).Any() ? Race.Players.SelectMany(x => x.Laps).Min(y => y.Time) : 0f);
            Vector2 vector = default(Vector2);
            vector.x = 0f;
            vector.y = UIManager.UI_TOP - 210f;
            var vel = GameManager.m_player_ship.c_rigidbody.velocity;
            if (!GameManager.m_player_ship.c_player.m_spectator)
                __instance.DrawStringSmall($"Vel: {vel.magnitude:n2}", vector, 0.5f, StringOffset.CENTER, UIManager.m_col_ui4, 1f);

            vector.x = UIManager.UI_LEFT + 10f;
            vector.y = UIManager.UI_TOP + 90f;
            __instance.DrawStringSmall($"Current: {last.Minutes:0}:{last.Seconds:00}.{last.Milliseconds:000}", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
            vector.y += 24F;
            __instance.DrawStringSmall($"Last: {last.Minutes:0}:{last.Seconds:00}.{last.Milliseconds:000}", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
            vector.y += 24f;
            __instance.DrawStringSmall($"Best: {best.Minutes:0}:{best.Seconds:00}.{best.Milliseconds:000}", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
            vector.y += 24f;
            __instance.DrawStringSmall($"Leader: {leader.Minutes:0}:{leader.Seconds:00}.{leader.Milliseconds:000}", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
            vector.y += 24f;
            __instance.DrawStringSmall($"Total: {TimeSpan.FromSeconds(NetworkMatch.m_match_elapsed_seconds).Minutes:0}:{TimeSpan.FromSeconds(NetworkMatch.m_match_elapsed_seconds).Seconds:00}.{TimeSpan.FromSeconds(NetworkMatch.m_match_elapsed_seconds).Milliseconds:000}", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
            vector.y += 24F;
            //var rps = Race.Players.Where(x => x.player.m_mp_name.Length > 2);
            //if (rps.Count() > 1)
            //{
            //    var rp1 = rps.ElementAt(0);
            //    var rp2 = rps.ElementAt(1);
            //    int pathlength;
            //    Pathfinding.FindConnectedDistancePointPoint(rp1.player.c_player_ship.SegmentIndex, rp2.player.c_player_ship.SegmentIndex, rp1.player.c_player_ship.c_transform.position, rp2.player.c_player_ship.c_transform.position, out pathlength);
            //    __instance.DrawStringSmall("Distance: " + pathlength.ToString(), vector, 0.4f, StringOffset.CENTER, UIManager.m_col_damage, 1f);
            //    vector.y += 24f;
            //    __instance.DrawStringSmall("Pathfinding: " + Pathfinding.m_precomputed_paths_valid.ToString(), vector, 0.4f, StringOffset.CENTER, UIManager.m_col_damage, 1f);
            //}
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUDScoreInfo")]
    class MatchModeRace_UIElement_DrawHUDScoreInfo
    {
        static bool Prefix(Vector2 pos, UIElement __instance, Vector2 ___temp_pos)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            pos.x -= 4f;
            pos.y -= 5f;
            ___temp_pos.y = pos.y;
            ___temp_pos.x = pos.x - 100f;
            __instance.DrawStringSmall(NetworkMatch.GetModeString(MatchMode.NUM), ___temp_pos, 0.4f, StringOffset.LEFT, UIManager.m_col_ub0, 1f, 130f);
            ___temp_pos.x = pos.x + 95f;
            int match_time_remaining = NetworkMatch.m_match_time_remaining;
            int num3 = (int)NetworkMatch.m_match_elapsed_seconds;
            __instance.DrawDigitsTime(___temp_pos, (float)match_time_remaining, 0.45f, (num3 <= 10 || match_time_remaining >= 10) ? UIManager.m_col_ui2 : UIManager.m_col_em5, __instance.m_alpha, false);
            ___temp_pos.x = pos.x - 100f;
            ___temp_pos.y = ___temp_pos.y - 20f;
            __instance.DrawPing(___temp_pos);
            pos.y += 24f;

            pos.y -= 12f;
            pos.x += 6f;
            UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ub1, __instance.m_alpha, 21);
            pos.y += 10f;
            ___temp_pos.x = pos.x;
            ___temp_pos.x = ___temp_pos.x + 90f;
            for (int i = 0; i < Race.Players.Count; i++)
            {
                ___temp_pos.y = pos.y;
                Player player = Race.Players[i].player;
                if (player && !player.m_spectator)
                {
                    var rp = Race.Players[i];
                    Color color3 = (!player.isLocalPlayer) ? UIManager.m_col_ui1 : UIManager.m_col_hi3;
                    float num4 = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    __instance.DrawDigitsVariable(___temp_pos, rp.Laps.Count(), 0.4f, StringOffset.RIGHT, color3, __instance.m_alpha * num4);
                    ___temp_pos.x = ___temp_pos.x - 35f;
                    __instance.DrawStringSmall(player.m_mp_name, ___temp_pos, 0.35f, StringOffset.RIGHT, color3, num4, -1f);
                    ___temp_pos.x = ___temp_pos.x + 8f;
                    if (UIManager.ShouldDrawPlatformId(player.m_mp_platform))
                    {
                        UIManager.DrawSpriteUI(___temp_pos, 0.1f, 0.1f, color3, num4 * 0.6f, (int)(226 + player.m_mp_platform));
                    }
                    ___temp_pos.x = ___temp_pos.x + 27f;
                    pos.y += 16f;
                }
            }
            pos.y -= 6f;
            UIManager.DrawQuadUI(pos, 100f, 1.2f, UIManager.m_col_ub1, __instance.m_alpha, 21);
            pos.x -= 6f;
            pos.y -= 6f;

            pos.y += 22f;
            pos.x += 100f;
            __instance.DrawRecentKillsMP(pos);
            if (GameManager.m_player_ship.m_wheel_select_state == WheelSelectState.QUICK_CHAT)
            {
                pos.y = UIManager.UI_TOP + 128f;
                pos.x = -448f;
                __instance.DrawQuickChatWheel(pos);
            }
            else
            {
                pos.y = UIManager.UI_TOP + 60f;
                pos.x = UIManager.UI_LEFT + 5f;
                __instance.DrawQuickChatMP(pos);
            }


            return false;
        }
    }


    [HarmonyPatch(typeof(UIElement), "DrawMpScoreboardRaw")]
    class MatchModeRace_UIElement_DrawMpScoreboardRaw
    {
        static bool Prefix(Vector2 pos, UIElement __instance)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            DrawMpScoreboardRaw(ref pos, __instance);
            return false;
        }

        public static void DrawMpScoreboardRaw(ref Vector2 pos, UIElement uie)
        {
            float col = -380f;
            float col2 = -40f;
            float col3 = 100f;
            float col4 = 240f;
            float col5 = 330f;
            float col6 = 400f;
            float col7 = 470f;
            DrawScoreHeader(uie, pos, col, col2, col3, col4, col5, col6, col7, true);
            pos.y += 15f;
            uie.DrawVariableSeparator(pos, 450f);
            pos.y += 20f;
            DrawScoresWithoutTeams(uie, pos, col, col2, col3, col4, col5, col6, col7);
        }

        static void DrawScoreHeader(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, float col6, float col7, bool score = false)
        {
            uie.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * col1, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            uie.DrawStringSmall(Loc.LS("TOTAL"), pos + Vector2.right * col2, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("BEST"), pos + Vector2.right * col3, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("LAPS"), pos + Vector2.right * col4, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * col5, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            uie.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * col6, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, 85f);
            UIManager.DrawSpriteUI(pos + Vector2.right * col7, 0.13f, 0.13f, UIManager.m_col_ui0, uie.m_alpha, 204);
        }

        static void DrawScoresWithoutTeams(UIElement uie, Vector2 pos, float col1, float col2, float col3, float col4, float col5, float col6, float col7)
        {
            for (int j = 0; j < Race.Players.Count; j++)
            {
                Player player = Race.Players[j].player;
                if (player && !player.m_spectator)
                {
                    float num = (!player.gameObject.activeInHierarchy) ? 0.3f : 1f;
                    if (j % 2 == 0)
                    {
                        UIManager.DrawQuadUI(pos, 400f, 13f, UIManager.m_col_ub0, uie.m_alpha * num * 0.1f, 13);
                    }
                    Color c;
                    Color c2;
                    if (player.isLocalPlayer)
                    {
                        UIManager.DrawQuadUI(pos, 510f, 12f, UIManager.m_col_ui0, uie.m_alpha * num * UnityEngine.Random.Range(0.2f, 0.22f), 20);
                        c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f));
                        c2 = UIManager.m_col_hi5;
                        UIManager.DrawQuadUI(pos - Vector2.up * 12f, 400f, 1.2f, c, uie.m_alpha * num * 0.5f, 4);
                        UIManager.DrawQuadUI(pos + Vector2.up * 12f, 400f, 1.2f, c, uie.m_alpha * num * 0.5f, 4);
                    }
                    else
                    {
                        c = UIManager.m_col_ui1;
                        c2 = UIManager.m_col_hi1;
                    }
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 35f), 0.11f, 0.11f, c, uie.m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod1, true));
                    UIManager.DrawSpriteUI(pos + Vector2.right * (col1 - 15f), 0.11f, 0.11f, c, uie.m_alpha * num, Player.GetMpModifierIcon(player.m_mp_mod2, false));
                    float max_width = col2 - col1 - (float)((!NetworkMatch.m_head_to_head) ? 130 : 10);
                    uie.DrawPlayerNameBasic(pos + Vector2.right * col1, player.m_mp_name, c, player.m_mp_rank_true, 0.6f, num, player.m_mp_platform, max_width);

                    var total = TimeSpan.Zero;
                    if (j == 0)
                    {
                        total = TimeSpan.FromSeconds(Race.Players[j].Laps.Sum(x => x.Time));
                    }
                    uie.DrawStringSmall(j == 0 ? $"{total.Minutes:0}:{total.Seconds:00}.{total.Milliseconds:000}" : "", pos + Vector2.right * col2, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);

                    var best = TimeSpan.Zero;
                    if (Race.Players[j].Laps.Count > 0)
                    {
                        best = TimeSpan.FromSeconds(Race.Players[j].Laps.Min(x => x.Time));
                    }
                    uie.DrawStringSmall(Race.Players[j].Laps.Count > 0 ? $"{best.Minutes:0}:{best.Seconds:00}.{best.Milliseconds:000}" : "", pos + Vector2.right * col3, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);

                    uie.DrawDigitsVariable(pos + Vector2.right * col4, Race.Players[j].Laps.Count(), 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col5, player.m_kills, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                    uie.DrawDigitsVariable(pos + Vector2.right * col6, player.m_deaths, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                    c = uie.GetPingColor(player.m_avg_ping_ms);
                    uie.DrawDigitsVariable(pos + Vector2.right * col7, player.m_avg_ping_ms, 0.65f, StringOffset.CENTER, c, uie.m_alpha * num);
                    pos.y += 25f;
                }
            }
        }
    }

    [HarmonyPatch(typeof(TriggerWindTunnel), "Start")]
    class MatchModeRace_TriggerWindTunnel_Start
    {
        static void Postfix(TriggerWindTunnel __instance)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            foreach (ParticleSystem componentsInChild in __instance.gameObject.GetComponentsInChildren<ParticleSystem>())
            {
                if (__instance.m_one_time)
                {
                    componentsInChild.SetEmissionRate(0f);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TriggerWindTunnel), "OnTriggerStay")]
    class MatchModeRace_TriggerWindTunnel_OnTriggerStay
    {

        private static bool MaybePatchCreepers(TriggerWindTunnel twt, Projectile proj)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return false;

            if (proj.m_type == ProjPrefab.missile_creeper)
                return true;

            return false;
        }

        // Skip out of applying velocity to creepers in race mode
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var matchModeRace_TriggerWindTunnel_OnTriggerStay_MaybePatchCreepers_Method = AccessTools.Method(typeof(MatchModeRace_TriggerWindTunnel_OnTriggerStay), "MaybePatchCreepers");

            int state = 0;
            object brjump = null;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldloc_3)
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Brfalse)
                {
                    state = 2;
                    brjump = code.operand;
                }

                if (state == 2 && code.opcode == OpCodes.Ldc_R4)
                {
                    state = 3;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_3);
                    yield return new CodeInstruction(OpCodes.Call, matchModeRace_TriggerWindTunnel_OnTriggerStay_MaybePatchCreepers_Method);
                    yield return new CodeInstruction(OpCodes.Brtrue, brjump);

                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "UpdateDynamic")]
    class MatchModeRace_Projectile_UpdateDynamic
    {
        static void Prefix(Projectile __instance, Collider ___m_collider_to_ignore, float ___m_lifetime)
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            if (__instance.m_type == ProjPrefab.missile_creeper && ___m_lifetime < 14f && ___m_collider_to_ignore != null)
                Physics.IgnoreCollision(__instance.c_collider, ___m_collider_to_ignore, false);
        }
    }

    // Static creeper drops
    [HarmonyPatch(typeof(Projectile), "Fire")]
    class MatchModeRace_Projectile_Fire
    {
        static void Prefix(Projectile __instance, ref Quaternion rot)
        {
            if (!GameplayManager.IsMultiplayerActive || MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            if (__instance.m_type == ProjPrefab.missile_creeper)
                rot = Quaternion.LookRotation(rot * Vector3.back);
        }

        static void Postfix(ref Projectile __instance, ref float ___m_lifetime, ref float ___m_init_speed, ref int ___m_bounce_max_count, Collider ___m_collider_to_ignore)
        {
            if (!GameplayManager.IsMultiplayerActive || MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            if (__instance.m_type == ProjPrefab.missile_creeper)
            {
                __instance.m_homing_max_dist = 0f;
                __instance.m_homing_strength = 0f;
                ___m_lifetime = 20f;
                ___m_bounce_max_count = 9999;
                __instance.m_acceleration = 0f;
                ___m_init_speed = 0f;
                __instance.c_rigidbody.velocity = Vector3.forward * 2f;
            }
        }
    }

    [HarmonyPatch(typeof(Item), "OnTriggerEnter")]
    public class MatchModeRace_Item_OnTriggerEnter
    {
        private static FieldInfo _Item_m_fake_picked_up_Field = AccessTools.Field(typeof(Item), "m_fake_picked_up");
        private static MethodInfo _Item_HideItem_Method = AccessTools.Method(typeof(Item), "HideItem");
        private static MethodInfo _Item_PlayItemPickupFX = AccessTools.Method(typeof(Item), "PlayItemPickupFX");
        public static void TryFakePickup(ref Item item, Player player)
        {
            //bool m_fake_picked_up = (bool)AccessTools.Field(typeof(Item), "m_fake_picked_up").GetValue(item);
            bool m_fake_picked_up = FakePickUp(ref item, player);
            _Item_m_fake_picked_up_Field.SetValue(item, m_fake_picked_up);
            if (m_fake_picked_up)
            {
                _Item_HideItem_Method.Invoke(item, new object[] { true });
                _Item_PlayItemPickupFX.Invoke(item, new object[] { player });
            }
        }

        public static bool FakePickUp(ref Item item, Player player)
        {
            switch (item.m_type)
            {
                case ItemType.POWERUP_SHIELD:
                    return player.CanAddArmor();
                case ItemType.POWERUP_ENERGY:
                    return player.CanAddEnergy();
                case ItemType.POWERUP_AMMO:
                    return player.CanAddAmmo();
                case ItemType.WEAPON_DRILLER:
                case ItemType.WEAPON_FLAK:
                case ItemType.WEAPON_CYCLONE:
                case ItemType.WEAPON_THUNDERBOLT:
                case ItemType.WEAPON_REFLEX:
                case ItemType.WEAPON_IMPULSE:
                case ItemType.WEAPON_SHOTGUN:
                case ItemType.WEAPON_LANCER:
                    {
                        WeaponType wt = Item.ItemToWeaponType(item.m_type);
                        if (player.WeaponUsesAmmo(wt))
                        {
                            return player.CanAddAmmo();
                        }
                        return player.CanAddEnergy();
                    }
                case ItemType.MISSILE_FALCON:
                    return player.CanAddMissileAmmo(MissileType.FALCON, item.m_super);
                case ItemType.MISSILE_POD:
                    return player.CanAddMissileAmmo(MissileType.MISSILE_POD, item.m_super);
                case ItemType.MISSILE_HUNTER:
                    return player.CanAddMissileAmmo(MissileType.HUNTER, item.m_super);
                case ItemType.MISSILE_CREEPER:
                    return player.CanAddMissileAmmo(MissileType.CREEPER, item.m_super);
                case ItemType.MISSILE_SMART:
                    return player.CanAddMissileAmmo(MissileType.NOVA, item.m_super);
                case ItemType.MISSILE_DEVASTATOR:
                    return player.CanAddMissileAmmo(MissileType.DEVASTATOR, item.m_super);
                case ItemType.MISSILE_TIMEBOMB:
                    return player.CanAddMissileAmmo(MissileType.TIMEBOMB, item.m_super);
                case ItemType.MISSILE_VORTEX:
                    return player.CanAddMissileAmmo(MissileType.VORTEX, item.m_super);
                case ItemType.TEMP_INVULN:
                    return player.CanStartInvul();
                case ItemType.TEMP_CLOAK:
                    return player.CanStartCloak();
                case ItemType.TEMP_RAPID:
                    return player.CanStartRapid();
                case ItemType.UPGRADE_L1:
                case ItemType.UPGRADE_L2:
                case ItemType.LOG_ENTRY:
                    return true;
                case ItemType.COLLECTIBLE:
                    return false;
                case ItemType.KEY_SECURITY:
                    return player.CanAddKey();
                case ItemType.POWERUP_ALIEN_ORB:
                    return player.CanAddArmor() || player.CanAddEnergy() || player.CanAddAmmo();
                default:
                    return false;
            }
        }

        private static MethodInfo _Item_ItemIsReachable_Method = typeof(Item).GetMethod("ItemIsReachable", AccessTools.all);
        static bool Prefix(ref Collider other, ref Item __instance, ref bool ___m_fake_picked_up)
        {
            if (!GameplayManager.IsMultiplayerActive || MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return true;

            if (other.attachedRigidbody == null)
            {
                return false;
            }
            PlayerShip component = other.attachedRigidbody.GetComponent<PlayerShip>();
            if (component == null)
            {
                return false;
            }
            Player c_player = component.c_player;
            bool itemIsReachable = (bool)_Item_ItemIsReachable_Method.Invoke(__instance, new object[] { other });
            if (!itemIsReachable || component.m_dying)
            {
                return false;
            }
            bool flag = false;
            if (GameplayManager.IsMultiplayerActive && !Overload.NetworkManager.IsServer())
            {
                if (c_player.isLocalPlayer && !___m_fake_picked_up)
                {
                    TryFakePickup(ref __instance, c_player);
                }
                return false;
            }
            if (Robot.m_guidebot_powerup_goal == ItemType.NUM || __instance.m_type == Robot.m_guidebot_powerup_goal || (Robot.m_guidebot_powerup_goal == ItemType.POWERUP_ENERGY && __instance.m_type == ItemType.POWERUP_AMMO))
            {
                Robot.m_player_picked_up_guidebot_powerup = true;
            }

            switch (__instance.m_type)
            {
                case ItemType.POWERUP_SHIELD:
                    flag = c_player.AddArmor(GameplayManager.dl_powerup_shields[GameplayManager.DifficultyLevel], false, false);
                    break;
                case ItemType.POWERUP_ENERGY:
                    flag = c_player.AddEnergy(GameplayManager.dl_powerup_energy[GameplayManager.DifficultyLevel], false, false);
                    break;
                case ItemType.POWERUP_AMMO:
                    flag = c_player.AddAmmo((!__instance.m_super) ? 50 : 1000, false, __instance.m_super, false);
                    break;
                case ItemType.WEAPON_DRILLER:
                case ItemType.WEAPON_FLAK:
                case ItemType.WEAPON_CYCLONE:
                case ItemType.WEAPON_THUNDERBOLT:
                case ItemType.WEAPON_REFLEX:
                case ItemType.WEAPON_IMPULSE:
                case ItemType.WEAPON_SHOTGUN:
                case ItemType.WEAPON_LANCER:
                    {
                        c_player.AddXP(1);
                        WeaponType weaponType = Item.ItemToWeaponType(__instance.m_type);
                        bool flag2 = c_player.m_weapon_level[(int)weaponType] != WeaponUnlock.LOCKED;
                        flag = c_player.UnlockWeapon(weaponType, false, true);
                        if (Overload.NetworkManager.IsServer() && flag && flag2)
                        {
                            //NetworkMatch.AddWeaponSpawn(weaponType);
                        }
                        break;
                    }
                case ItemType.MISSILE_FALCON:
                    c_player.UnlockMissile(MissileType.FALCON);
                    flag = c_player.AddMissileAmmo(__instance.m_amount, MissileType.FALCON, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_POD:
                    c_player.UnlockMissile(MissileType.MISSILE_POD);
                    flag = c_player.AddMissileAmmo(15, MissileType.MISSILE_POD, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_HUNTER:
                    c_player.UnlockMissile(MissileType.HUNTER);
                    flag = c_player.AddMissileAmmo(3, MissileType.HUNTER, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_CREEPER:
                    c_player.UnlockMissile(MissileType.CREEPER);
                    flag = c_player.AddMissileAmmo(__instance.m_super ? 20 : 5, MissileType.CREEPER, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_SMART:
                    c_player.UnlockMissile(MissileType.NOVA);
                    flag = c_player.AddMissileAmmo(__instance.m_amount, MissileType.NOVA, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_DEVASTATOR:
                    c_player.UnlockMissile(MissileType.DEVASTATOR);
                    flag = c_player.AddMissileAmmo(__instance.m_amount, MissileType.DEVASTATOR, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_TIMEBOMB:
                    c_player.UnlockMissile(MissileType.TIMEBOMB);
                    flag = c_player.AddMissileAmmo(__instance.m_amount, MissileType.TIMEBOMB, false, __instance.m_super);
                    break;
                case ItemType.MISSILE_VORTEX:
                    c_player.UnlockMissile(MissileType.VORTEX);
                    flag = c_player.AddMissileAmmo(2, MissileType.VORTEX, false, __instance.m_super);
                    break;
                case ItemType.TEMP_INVULN:
                    c_player.AddXP(1);
                    c_player.m_spawn_invul_active = false;
                    flag = c_player.StartInvul((!c_player.m_unlock_item_duration) ? Player.TEMP_POWER_TIMER : 30f, false);
                    break;
                case ItemType.TEMP_CLOAK:
                    c_player.AddXP(1);
                    flag = c_player.StartCloak();
                    break;
                case ItemType.TEMP_RAPID:
                    c_player.AddXP(1);
                    flag = c_player.StartRapid();
                    break;
                case ItemType.UPGRADE_L1:
                    c_player.AddXP(2);
                    //c_player.AddUpgradePoint(false, false);
                    flag = Race.ItemPickupUpgradePoint(false, c_player);
                    //flag = true;
                    break;
                case ItemType.UPGRADE_L2:
                    c_player.AddXP(2);
                    //c_player.AddUpgradePoint(true, false);
                    flag = Race.ItemPickupUpgradePoint(true, c_player);
                    break;
                case ItemType.KEY_SECURITY:
                    c_player.AddXP(5);
                    flag = c_player.AddKey();
                    Robot.m_player_picked_up_security_key = true;
                    break;
                case ItemType.LOG_ENTRY:
                    c_player.AddXP(1);
                    GameplayManager.PickupLogEntry();
                    flag = true;
                    break;
                case ItemType.POWERUP_ALIEN_ORB:
                    flag |= c_player.AddArmor(GameplayManager.dl_powerup_shields[GameplayManager.DifficultyLevel] * 0.5f, true, false);
                    flag |= c_player.AddEnergy(GameplayManager.dl_powerup_energy[GameplayManager.DifficultyLevel] * 0.25f, true, false);
                    //flag |= c_player.AddAmmo((int)(GameplayManager.dl_powerup_ammo[GameplayManager.DifficultyLevel] * 0.2f), true, false, false);
                    if (flag)
                    {
                        c_player.CallTargetAddHUDMessage(c_player.connectionToClient, Loc.LSN("ARMOR AND ENERGY INCREASED"), -1, false);
                    }
                    break;
            }
            GameplayManager.AddStatsPowerup(__instance.m_type, __instance.m_secret);

            if (__instance.m_secret)
            {
                c_player.AddXP(2);
            }

            if (flag)
            {
                c_player.CallRpcPlayItemPickupFX(__instance.m_type, __instance.m_super);
                foreach (ScriptBase scriptBase in __instance.triggered_on_pickup)
                {
                    scriptBase.SendMessage("ActivateScriptLink", null, SendMessageOptions.DontRequireReceiver);
                }
                if (__instance.m_spawn_point > -1 && __instance.m_spawn_point < Item.HasLiveItem.Length)
                {
                    Item.HasLiveItem[__instance.m_spawn_point] = false;
                    if (NetworkSpawnItem.m_respawn_nodes != null)
                    {
                        NetworkSpawnItem.m_respawn_nodes[__instance.m_spawn_point].m_respawn_timer = __instance.m_index;
                    }
                }

                if (Overload.NetworkManager.IsServer())
                {
                    for (int i = 0; i < Race.spawns.Count; i++)
                    {
                        if (i == __instance.m_index)
                        {
                            Race.spawns[i].gameObject = null;
                            Race.spawns[i].active = false;
                            Race.spawns[i].respawn_timer = 0f;
                            break;
                        }
                    }
                }

                RobotManager.RemoveItemFromList(__instance);
                UnityEngine.Object.Destroy(__instance.c_go);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Item), "MaybeDespawnPowerup")]
    class MatchModeRace_Item_MaybeDespawnPowerup
    {
        static void Postfix(float __result)
        {
            if (GameplayManager.IsMultiplayerActive && MPModPrivateData.MatchMode == ExtMatchMode.RACE)
                __result = 0f;
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
    class MatchModeRace_GameplayManager_StartLevel
    {
        static void Prefix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            Race.spawns = new List<Race.ArenaSpawn>();
            List<Item> reinitList = new List<Item>();
            foreach (var item in GameObject.FindObjectsOfType<Item>())
            {
                if (item.m_respawning || item.m_index > 0)
                {
                    reinitList.Add(item);
                }
            }

            foreach (var item in reinitList)
            {
                if (Overload.NetworkManager.IsServer())
                {
                    Race.spawns.Add(new Race.ArenaSpawn
                    {
                        gameObject = null,
                        prefab = Race.GetPrefabFromType(item.m_type),
                        respawn_length = item.m_index,
                        respawn_timer = 0f,
                        active = false,
                        position = item.gameObject.transform.position,
                        super = item.is_super
                    });
                }

                UnityEngine.Object.Destroy(item.c_go);
            }
        }

    }

    [HarmonyPatch(typeof(GameplayManager), "Update")]
    class MatchModeRace_GameplayManager_Update
    {
        static void Postfix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.RACE)
                return;

            if (!Overload.NetworkManager.IsServer())
            {
                return;
            }

            for (int i = 0; i < Race.spawns.Count; i++)
            {
                Race.ArenaSpawn s = Race.spawns[i];
                Race.spawns[i].respawn_timer += RUtility.FRAMETIME_GAME;

                if (s.respawn_timer > s.respawn_length && !s.active && s.gameObject == null)
                {
                    Race.spawns[i].gameObject = Spew(PrefabManager.item_prefabs[(int)s.prefab], s.position, Vector3.zero);
                    Item item = Race.spawns[i].gameObject.GetComponent<Item>();
                    item.m_index = i;
                    Race.spawns[i].respawn_timer = 0f;
                    Race.spawns[i].active = true;
                    Race.spawns[i].super = item.m_super;
                }
            }
        }

        private static FieldInfo _Item_m_spewed_Field = AccessTools.Field(typeof(Item), "m_spewed");
        public static GameObject Spew(GameObject prefab, Vector3 pos, Vector3 vel, int spawn_idx = -1, bool make_super = false)
        {
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(prefab, pos, Quaternion.identity);
            if (gameObject == null)
            {
                Debug.LogWarningFormat("Failed to instantiate prefab: {0} in Item::Spew", new object[]
                {
                prefab.name
                });
                return null;
            }
            if (Overload.NetworkManager.IsServer())
            {
                NetworkSpawnItem.Spawn(gameObject);
            }
            Item component = gameObject.GetComponent<Item>();
            if (component == null)
            {
                Debug.LogErrorFormat("Failed to find Item component on {0} in Item::Spew", new object[]
                {
                gameObject.name
                });
                return null;
            }
            component.m_super = make_super;
            if (Overload.NetworkManager.IsServer() && make_super)
            {
                component.CallRpcMakeSuper();
            }
            component.c_rigidbody.velocity = vel * component.m_push_multiplier;
            _Item_m_spewed_Field.SetValue(component, true);
            //component.m_spewed = true;
            component.m_spawn_point = spawn_idx;
            if (spawn_idx > -1 && spawn_idx < Item.HasLiveItem.Length)
            {
                Item.HasLiveItem[spawn_idx] = true;
            }

            return gameObject;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MatchModeRace_MenuManager_MpMatchSetup
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var menuManager_mms_respawn_time_Field = AccessTools.Field(typeof(MenuManager), "mms_respawn_time");

            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldsfld && code.operand == menuManager_mms_respawn_time_Field)
                    state = 1;

                else if (state == 1 && code.opcode == OpCodes.Ldc_I4_2)
                {
                    state = 2;
                    code.opcode = OpCodes.Ldc_I4_0; // Patch in 0 as minimum clampf arg for respawn timer
                }

                yield return code;
            }
        }

        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 2 &&
                UIManager.m_menu_selection == 0)
            {
                // If mode changed, force race to 0s
                MenuManager.mms_respawn_time = MenuManager.mms_mode == ExtMatchMode.RACE ? 0 : 2;
            }
        }
    }

    // the level remains active, disable windtunnels to prevent continuous bumping in menus
    [HarmonyPatch(typeof(NetworkMatch), "NetSystemShutdown")]
    class DisableWindTunnels
    {
        static void Prefix() {
            foreach (var wt in UnityEngine.Object.FindObjectsOfType<TriggerWindTunnel>())
                wt.gameObject.SetActive(false);
        }
    }
}
