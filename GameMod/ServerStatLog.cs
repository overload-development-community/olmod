﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Harmony;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

// by luponix, roncli

namespace GameMod
{
    public static class ServerStatLog
    {
        private struct PlayerPlayerWeaponDamage
        {
            public Player Attacker, Defender;
            public string Weapon;
        }

        private struct Kill
        {
            public float Time;
            public string Attacker, Defender, Assisted;
            public MpTeam AttackerTeam, DefenderTeam, AssistedTeam;
            public string Weapon;
        }

        private struct Goal
        {
            public float Time;
            public string Scorer, Assisted;
            public MpTeam? ScorerTeam;
            public bool Blunder;
        }

        private struct FlagStat
        {
            public float Time;
            public string Event;
            public string Scorer;
            public MpTeam? ScorerTeam;
        }

        private static Dictionary<PlayerPlayerWeaponDamage, float> DamageTable = new Dictionary<PlayerPlayerWeaponDamage, float>();
        private static List<Kill> Kills = new List<Kill>();
        private static List<Goal> Goals = new List<Goal>();
        private static List<FlagStat> FlagStats = new List<FlagStat>();
        public static bool GameStarted = false;
        private static string Attacker, Defender, Assisted;
        private static MpTeam AttackerTeam = (MpTeam)(-1), DefenderTeam = (MpTeam)(-1), AssistedTeam = (MpTeam)(-1);
        public static DateTime StartTime, EndTime;

        public static void CleanUp()
        {
            DamageTable = new Dictionary<PlayerPlayerWeaponDamage, float>();
            Kills = new List<Kill>();
            Goals = new List<Goal>();
            FlagStats = new List<FlagStat>();
            GameStarted = false;
        }

        public static JArray GetDamageTable()
        {
            return new JArray(
                from d in DamageTable
                select JObject.FromObject(new
                {
                    attacker = d.Key.Attacker.m_mp_name,
                    defender = d.Key.Defender.m_mp_name,
                    weapon = d.Key.Weapon.ToString(),
                    damage = d.Value
                }, new JsonSerializer()
                {
                    NullValueHandling = NullValueHandling.Ignore
                })
            );
        }

        public static JArray GetKills()
        {
            return new JArray(
                from k in Kills
                select JObject.FromObject(new
                {
                    time = k.Time,
                    attacker = k.Attacker,
                    attackerTeam = k.Attacker == null ? null : TeamName(k.AttackerTeam),
                    defender = k.Defender,
                    defenderTeam = k.Defender == null ? null : TeamName(k.DefenderTeam),
                    assisted = k.Assisted,
                    assistedTeam = k.Assisted == null ? null : TeamName(k.AssistedTeam),
                    weapon = k.Weapon
                }, new JsonSerializer()
                {
                    NullValueHandling = NullValueHandling.Ignore
                })
            );
        }

        public static JArray GetGoals()
        {
            if (Goals.Count == 0)
            {
                return null;
            }

            return new JArray(
                from g in Goals
                select JObject.FromObject(new
                {
                    time = g.Time,
                    scorer = g.Scorer,
                    scorerTeam = TeamName(g.ScorerTeam),
                    assisted = g.Assisted,
                    blunder = g.Blunder
                }, new JsonSerializer()
                {
                    NullValueHandling = NullValueHandling.Ignore
                })
            );
        }

        public static JArray GetFlagStats()
        {
            if (FlagStats.Count == 0)
            {
                return null;
            }

            return new JArray(
                from s in FlagStats
                select JObject.FromObject(new
                {
                    time = s.Time,
                    @event = s.Event,
                    scorer = s.Scorer,
                    scorerTeam = TeamName(s.ScorerTeam)
                }, new JsonSerializer()
                {
                    NullValueHandling = NullValueHandling.Ignore
                })
            );
        }

        public static JObject GetGame()
        {
            return JObject.FromObject(new
            {
                name = "Stats",
                type = "EndGame",
                start = StartTime.ToString("o"),
                end = EndTime.ToString("o"),
                damage = GetDamageTable(),
                kills = GetKills(),
                goals = GetGoals(),
                flagStats = GetFlagStats()
            }, new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        private static IEnumerator RequestCoroutine(UnityWebRequestAsyncOperation op)
        {
            yield return op;
        }

        public static void Post(string url, JObject body)
        {
            var request = new UnityWebRequest(url)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(),
                method = UnityWebRequest.kHttpVerbPOST
            };
            request.SetRequestHeader("Content-Type", "application/json");
            GameManager.m_gm.StartCoroutine(RequestCoroutine(request.SendWebRequest()));
        }

        public static void TrackerPost(JObject body)
        {
            string url;
            Debug.Log("TrackerPost " + body.ToString(Formatting.None));
            if (!Config.Settings.Value<bool>("isServer") ||
                string.IsNullOrEmpty(url = Config.Settings.Value<string>("trackerBaseUrl")))
                return;
            Post(url + "/api/stats", body);
        }

        public static void PrintResultsAsJson()
        {
            if (!GameStarted)
            {
                return;
            }

            string FullTimeJsonPath = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            EndTime = DateTime.UtcNow;
            string JsonPath = Path.Combine(Application.persistentDataPath, "SSL_" + FullTimeJsonPath + ".json");

            var obj = GetGame();

            using (StreamWriter streamWriter = new StreamWriter(JsonPath))
            {
                streamWriter.Write(obj.ToString(Formatting.Indented));
            }

            TrackerPost(obj);

            CleanUp();
        }

        public static void SetAttacker(string name, MpTeam team)
        {
            Attacker = name;
            AttackerTeam = team;
        }

        public static void SetDefender(string name, MpTeam team)
        {
            Defender = name;
            DefenderTeam = team;
        }

        public static void SetAssisted(string name, MpTeam team)
        {
            Assisted = name;
            AssistedTeam = team;
        }

        public static void Disconnected(string name)
        {
            if (NetworkMatch.m_postgame) return;

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Disconnect",
                time = NetworkMatch.m_match_elapsed_seconds,
                player = name
            });

            TrackerPost(obj);
        }

        // called from MPJoinInProgress
        public static void Connected(string name)
        {
            if (NetworkMatch.m_postgame) return;

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Connect",
                time = NetworkMatch.m_match_elapsed_seconds,
                player = name
            });

            TrackerPost(obj);
        }

        private static string TeamName(MpTeam? team)
        {
            return team == null || team == MpTeam.ANARCHY || team == (MpTeam)(-1) ? null : MPTeams.TeamName((MpTeam)team);
        }

        public static void AddGoal()
        {
            if (NetworkMatch.m_postgame) return;

            var goal = new Goal()
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                Scorer = MonsterballAddon.CurrentPlayer?.m_mp_name,
                Assisted = MonsterballAddon.LastPlayer?.m_mp_name,
                ScorerTeam = MonsterballAddon.CurrentPlayer?.m_mp_team,
                Blunder = false,
            };

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Goal",
                time = goal.Time,
                scorer = MonsterballAddon.CurrentPlayer?.m_mp_name,
                scorerTeam = TeamName(MonsterballAddon.CurrentPlayer?.m_mp_team),
                assisted = MonsterballAddon.LastPlayer?.m_mp_name,
                assistedTeam = TeamName(MonsterballAddon.LastPlayer?.m_mp_team)
            }, new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            Goals.Add(goal);

            TrackerPost(obj);
        }

        public static void AddBlunder()
        {
            if (NetworkMatch.m_postgame) return;

            var goal = new Goal()
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                Scorer = MonsterballAddon.CurrentPlayer?.m_mp_name,
                ScorerTeam = MonsterballAddon.CurrentPlayer?.m_mp_team,
                Blunder = true,
            };

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Blunder",
                time = goal.Time,
                scorer = MonsterballAddon.CurrentPlayer?.m_mp_name,
                scorerTeam = TeamName(MonsterballAddon.CurrentPlayer?.m_mp_team)
            });

            Goals.Add(goal);

            TrackerPost(obj);
        }

        public static void AddKill(DamageInfo di)
        {
            if (NetworkMatch.m_postgame) return;

            Kills.Add(new Kill
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                Attacker = Attacker,
                Defender = Defender,
                Assisted = Assisted,
                AttackerTeam = AttackerTeam,
                DefenderTeam = DefenderTeam,
                AssistedTeam = AssistedTeam,
                Weapon = di.weapon.ToString()
            });

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Kill",
                time = NetworkMatch.m_match_elapsed_seconds,
                attacker = Attacker,
                attackerTeam = Attacker == null ? null : TeamName(AttackerTeam),
                defender = Defender,
                defenderTeam = Defender == null ? null : TeamName(DefenderTeam),
                assisted = Assisted,
                assistedTeam = Assisted == null ? null : TeamName(AssistedTeam),
                weapon = di.weapon.ToString()
            }, new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            TrackerPost(obj);

            Attacker = null;
            Defender = null;
            Assisted = null;
            AttackerTeam = MpTeam.ANARCHY;
            DefenderTeam = MpTeam.ANARCHY;
            AssistedTeam = MpTeam.ANARCHY;
        }

        public static void AddDamage(Player defender, Player attacker, string weapon, float damage)
        {
            if (NetworkMatch.m_postgame) return;

            var key = new PlayerPlayerWeaponDamage { Attacker = attacker, Defender = defender, Weapon = weapon };
            if (DamageTable.TryGetValue(key, out float totalDamage))
                DamageTable[key] = totalDamage + damage;
            else
                DamageTable[key] = damage;
        }

        public static void AddFlagEvent(Player player, string @event)
        {
            if (NetworkMatch.m_postgame) return;

            var capture = new FlagStat()
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                Event = @event,
                Scorer = player.m_mp_name,
                ScorerTeam = player.m_mp_team
            };

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "CTF",
                time = capture.Time,
                @event,
                scorer = capture.Scorer,
                scorerTeam = TeamName(capture.ScorerTeam),
            }, new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            FlagStats.Add(capture);

            TrackerPost(obj);
        }

        public static string GetPowerupBigSpawnString(int spawn)
        {
            switch (spawn)
            {
                case 0: return "OFF";
                case 1: return "LOW";
                case 2: return "NORMAL";
                case 3: return "HIGH";
                default: return "UNKNOWN";
            }
        }

        public static string GetPowerupInitialString(int initial)
        {
            switch (initial)
            {
                case 0: return "NONE";
                case 1: return "LOW";
                case 2: return "NORMAL";
                case 3: return "HIGH";
                default: return "UNKNOWN";
            }
        }

        public static string GetTurnSpeedLimitString(int limit)
        {
            switch (limit)
            {
                case 0: return "VERY STRONG";
                case 1: return "STRONG";
                case 2: return "MEDIUM";
                case 3: return "WEAK";
                case 4: return "OFF";
                default: return "UNKNOWN";
            }
        }

        public static string GetPowerupSpawnString(int spawn)
        {
            switch (spawn)
            {
                case 0: return "LOW";
                case 1: return "NORMAL";
                case 2: return "HIGH";
                case 3: return "MAX";
                default: return "UNKNOWN";
            }
        }

        public static string GetLevel(int levelNum, string level)
        {
            if (string.IsNullOrEmpty(level))
            {
                return GameManager.MultiplayerMission.GetLevelDisplayName(levelNum);
            }
            else
            {
                return level.Split(':')[0];
            }
        }

        public static void StartGame()
        {
            StartTime = DateTime.UtcNow;
            CleanUp();
            GameStarted = true;

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "StartGame",
                start = DateTime.UtcNow.ToString("o"),
                time = 0,
                creator = NetworkMatch.m_name.Split('\0')[0],
                forceModifier1 = NetworkMatch.m_force_modifier1 == 4 ? "OFF" : Player.GetMpModifierName(NetworkMatch.m_force_modifier1, true),
                forceModifier2 = NetworkMatch.m_force_modifier2 == 4 ? "OFF" : Player.GetMpModifierName(NetworkMatch.m_force_modifier2, false),
                forceMissile1 = NetworkMatch.m_force_m1.ToString()?.Replace("_", " "),
                forceMissile2 = NetworkMatch.m_force_m2 == MissileType.NUM ? "NONE" : NetworkMatch.m_force_m2.ToString()?.Replace("_", " "),
                forceWeapon1 = NetworkMatch.m_force_w1.ToString(),
                forceWeapon2 = NetworkMatch.m_force_w2 == WeaponType.NUM ? "NONE" : NetworkMatch.m_force_w2.ToString(),
                forceLoadout = MenuManager.GetToggleSetting(NetworkMatch.m_force_loadout),
                powerupFilterBitmask = NetworkMatch.m_powerup_filter_bitmask,
                powerupBigSpawn = GetPowerupBigSpawnString(NetworkMatch.m_powerup_big_spawn),
                powerupInitial = GetPowerupInitialString(NetworkMatch.m_powerup_initial),
                turnSpeedLimit = GetTurnSpeedLimitString(NetworkMatch.m_turn_speed_limit),
                powerupSpawn = GetPowerupSpawnString(NetworkMatch.m_powerup_spawn),
                friendlyFire = NetworkMatch.m_team_damage,
                matchMode = NetworkMatch.GetMode().ToString()?.Replace("_", " "),
                maxPlayers = NetworkMatch.GetMaxPlayersForMatch(),
                showEnemyNames = NetworkMatch.m_show_enemy_names.ToString()?.Replace("_", " "),
                timeLimit = NetworkMatch.m_match_time_limit_seconds,
                scoreLimit = NetworkMatch.m_match_score_limit,
                respawnTimeSeconds = NetworkMatch.m_respawn_time_seconds,
                respawnShieldTimeSeconds = NetworkMatch.m_respawn_shield_seconds,
                level = GameplayManager.Level.DisplayName,
                joinInProgress = MPJoinInProgress.NetworkMatchEnabled,
                rearViewAllowed = RearView.MPNetworkMatchEnabled,
                teamCount = MPTeams.NetworkMatchTeamCount,
                players = Overload.NetworkManager.m_Players.Where(x => !x.m_spectator).Select(x => x.m_mp_name)
            });

            TrackerPost(obj);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    internal class LogOnConnect
    {

        private static void Prefix()
        {
            if (!Overload.NetworkManager.IsHeadless())
                return;
            ServerStatLog.StartGame();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
    internal class LogOnExit
    {
        private static void Prefix()
        {
            if (!Overload.NetworkManager.IsHeadless())
                return;
            ServerStatLog.PrintResultsAsJson();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    internal class LogOnDamage
    {
        public static void Prefix(DamageInfo di, PlayerShip __instance)
        {
            if (!Overload.NetworkManager.IsHeadless() || di.damage == 0f || di.owner == null ||
                __instance.m_death_stats_recorded || __instance.m_cannot_die || __instance.c_player.m_invulnerable)
                return;
            var otherPlayer = di.owner.GetComponent<Player>();
            if (otherPlayer == null)
                return;

            float hitpoints = __instance.c_player.m_hitpoints;
            string weapon = di.weapon.ToString();

            //bool killed = false;
            float damage = di.damage;
            if (hitpoints - di.damage <= 0f)
            {
                damage = hitpoints;
                //killed = true;
            }
            ServerStatLog.AddDamage(__instance.c_player, otherPlayer, weapon, damage);
            //ServerStatLog.damageTable.
            //ServerStatLog.damageTable[new PlayerPlayerWeaponDamage { From = __instance.c_player, To = otherPlayer, Weapon = weapon }] += damage;
            //ServerStatLog.AddLine("Event: " + ServerStatLog.ShortTime() + ":" + mp_name2 + ":" +mp_name + ":" + di.weapon + ":" + damage + ":" + killed);
        }
    }

    [HarmonyPatch(typeof(Overload.Player), "OnKilledByPlayer")]
    internal class LogOnKill
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool lastTryGetValue = false;
            object lastLocVar = null;
            int setCount = 0;
            string[] setMethods = new[] { "SetDefender", "SetAttacker", "SetAssisted" };

            foreach (var code in instructions)
            {
                if (code.opcode == OpCodes.Ret && setCount > 0)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // damageInfo
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ServerStatLog), "AddKill"));
                }
                yield return code;
                if (code.opcode == OpCodes.Brfalse && lastTryGetValue && setCount < setMethods.Length)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_name"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, lastLocVar);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerLobbyData), "m_team"));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ServerStatLog),
                        setMethods[setCount]));
                    setCount++;
                }
                if (code.opcode == OpCodes.Ldloca || code.opcode == OpCodes.Ldloca_S)
                {
                    lastLocVar = code.operand;
                }
                lastTryGetValue = code.opcode == OpCodes.Callvirt && ((MemberInfo)code.operand).Name == "TryGetValue";
            }
        }
    }

    [HarmonyPatch(typeof(Overload.Server), "InvokeDisconnectFlashOnClients")]
    internal class LogOnDisconnect
    {
        static void Prefix(Player disconnected_player)
        {
            if (!disconnected_player.m_spectator)
            {
                ServerStatLog.Disconnected(disconnected_player.m_mp_name);
            }
        }
    }
}
