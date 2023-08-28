using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

// by luponix, roncli

namespace GameMod {
    public static class ServerStatLog
    {
        private struct PlayerPlayerWeaponDamage
        {
            public Player Attacker, Defender;
            public ProjPrefab Weapon;
        }

        private struct Kill
        {
            public float Time;
            public string Attacker, Defender, Assisted;
            public MpTeam AttackerTeam, DefenderTeam, AssistedTeam;
            public ProjPrefab Weapon;
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

        private struct TeamChange
        {
            public float Time;
            public string PlayerName;
            public MpTeam? PreviousTeam;
            public MpTeam? CurrentTeam;
        }

        private static Dictionary<PlayerPlayerWeaponDamage, float> DamageTable = new Dictionary<PlayerPlayerWeaponDamage, float>();
        private static List<Kill> Kills = new List<Kill>();
        private static List<Goal> Goals = new List<Goal>();
        private static List<FlagStat> FlagStats = new List<FlagStat>();
        private static List<TeamChange> TeamChanges = new List<TeamChange>();
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
            TeamChanges = new List<TeamChange>();
            GameStarted = false;
        }

        public static JArray GetDamageTable()
        {
            return new JArray(
                from d in DamageTable
                select JObject.FromObject(new
                {
                    attacker = d.Key.Attacker?.m_mp_name,
                    defender = d.Key.Defender.m_mp_name,
                    weapon = d.Key.Weapon.ToString(),
                    damage = d.Value
                }, new JsonSerializer()
                {
                    NullValueHandling = NullValueHandling.Ignore
                })
            );
        }

        public static JArray GetTeamChanges()
        {
            return new JArray(
                from tc in TeamChanges
                select JObject.FromObject(new
                {
                    time = tc.Time,
                    playerName = tc.PlayerName,
                    previousTeam = TeamName(tc.PreviousTeam),
                    currentTeam = TeamName(tc.CurrentTeam)
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
                    weapon = k.Weapon.ToString()
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
                flagStats = GetFlagStats(),
                teamChanges = GetTeamChanges()
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

        public static void TrackerPost(string path, JObject body)
        {
            string url;
            Debug.LogFormat("{0}: TrackerPost {1} {2}", DateTime.Now.ToString(), path, body.ToString(Formatting.None));
            if (!Config.Settings.Value<bool>("isServer") ||
                string.IsNullOrEmpty(url = Config.Settings.Value<string>("trackerBaseUrl")))
                return;
            Post(url + path, body);
        }

        public static void TrackerPostStats(JObject body)
        {
            body.Add("port", Server.GetListenPort());
            TrackerPost("/api/stats", body);
        }

        public static void EndGame()
        {
            if (!GameStarted)
            {
                TrackerPostStats(JObject.FromObject(new
                {
                    name = "Stats",
                    type = "LobbyExit",
                }));
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

            TrackerPostStats(obj);

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
            if (NetworkMatch.m_postgame || NetworkMatch.GetMatchState() == MatchState.NONE)
                return;

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Disconnect",
                time = NetworkMatch.m_match_elapsed_seconds,
                player = name
            });

            TrackerPostStats(obj);
        }

        // called from MPJoinInProgress
        public static void Connected(string name)
        {
            if (NetworkMatch.m_postgame)
                return;

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "Connect",
                time = NetworkMatch.m_match_elapsed_seconds,
                player = name
            });

            TrackerPostStats(obj);
        }

        private static string TeamName(MpTeam? team)
        {
            return team == null || team == MpTeam.ANARCHY || team == (MpTeam)(-1) ? null : MPTeams.TeamNameNotLocalized((MpTeam)team);
        }

        public static void AddGoal()
        {
            if (NetworkMatch.m_postgame)
                return;

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

            TrackerPostStats(obj);
        }

        public static void AddBlunder()
        {
            if (NetworkMatch.m_postgame)
                return;

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

            TrackerPostStats(obj);
        }

        public static void AddTeamChange(Player player, MpTeam newTeam)
        {
            if (NetworkMatch.m_postgame)
                return;

            TeamChanges.Add(new TeamChange
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                PlayerName = player.m_mp_name,
                PreviousTeam = player.m_mp_team,
                CurrentTeam = newTeam
            });

            var obj = JObject.FromObject(new
            {
                name = "Stats",
                type = "TeamChange",
                time = NetworkMatch.m_match_elapsed_seconds,
                playerName = player.m_mp_name,
                previousTeam = TeamName(player.m_mp_team),
                currentTeam = TeamName(newTeam)
            }, new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            TrackerPostStats(obj);
        }

        // gets the **stock** projprefab for that weapon slot
        private static ProjPrefab GetProjPrefab(int damageType)
        {
            switch (damageType) {
                case 0:
                    return ProjPrefab.proj_impulse;
                case 1:
                    return ProjPrefab.proj_vortex;
                case 2:
                    return ProjPrefab.proj_reflex;
                case 3:
                    return ProjPrefab.proj_shotgun;
                case 4:
                    return ProjPrefab.proj_driller;
                case 5:
                    return ProjPrefab.proj_flak_cannon;
                case 6:
                    return ProjPrefab.proj_thunderbolt;
                case 7:
                    return ProjPrefab.proj_beam;
                case 8:
                    return ProjPrefab.missile_falcon;
                case 9:
                    return ProjPrefab.missile_pod;
                case 10:
                    return ProjPrefab.missile_hunter;
                case 11:
                    return ProjPrefab.missile_creeper;
                case 12:
                    return ProjPrefab.missile_smart;
                case 13:
                    return ProjPrefab.missile_devastator;
                case 14:
                    return ProjPrefab.missile_timebomb;
                case 15:
                    return ProjPrefab.missile_vortex;
                case 16:
                    return ProjPrefab.proj_melee;
                default:
                    return ProjPrefab.none;
            }
        }

        // any custom weapon IDs that have made it through to this point will get converted back to their stock weapon slot's damage
        private static ProjPrefab SanitizeWeaponID(ProjPrefab weapon)
        {
            int damageType = -1;

            for (int i = 0; i < 8; i++)
            {
                if ((int)weapon == (int)MPWeapons.primaries[i].projprefab)
                {
                    damageType = i;
                    break;
                }
                else if ((int)weapon == (int)MPWeapons.secondaries[i].projprefab || (int)weapon == (int)MPWeapons.secondaries[i].subproj)
                {
                    damageType = i + 8;
                    break;
                }
            }

            if (weapon == ProjPrefab.proj_melee)
            {
                damageType = 16;
            }
            /*
            switch (weapon)
            {
                case ProjPrefab.proj_impulse:
                    damageType = 0; break;
                case ProjPrefab.proj_vortex:
                    damageType = 1; break;
                case ProjPrefab.proj_reflex:
                    damageType = 2; break;
                case ProjPrefab.proj_shotgun:
                    damageType = 3; break;
                case ProjPrefab.proj_driller:
                    damageType = 4; break;
                case ProjPrefab.proj_flak_cannon:
                    damageType = 5; break;
                case ProjPrefab.proj_thunderbolt:
                    damageType = 6; break;
                case ProjPrefab.proj_beam:
                    damageType = 7; break;
                case ProjPrefab.missile_falcon:
                    damageType = 8; break;
                case ProjPrefab.missile_pod:
                    damageType = 9; break;
                case ProjPrefab.missile_hunter:
                    damageType = 10; break;
                case ProjPrefab.missile_creeper:
                    damageType = 11; break;
                case ProjPrefab.missile_smart:
                case ProjPrefab.missile_smart_mini:
                    damageType = 12; break;
                case ProjPrefab.missile_devastator:
                case ProjPrefab.missile_devastator_mini:
                    damageType = 13; break;
                case ProjPrefab.missile_timebomb:
                    damageType = 14; break;
                case ProjPrefab.missile_vortex:
                    damageType = 15; break;
                case ProjPrefab.proj_melee:
                    damageType = 16; break;
                default:
                    damageType = -1; break;
            }
            */
            return GetProjPrefab(damageType);
        }

        public static void AddKill(DamageInfo di, PlayerDamageRecord pdr, bool flag)
        {
            if (NetworkMatch.m_postgame)
                return;

            //Debug.Log("CCF ServerStatLog kill recorded, damage type is " + GetProjPrefab(pdr.dmg_type) + "and weapon idx is " + SanitizeWeaponID(di.weapon));

            Kills.Add(new Kill
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                Attacker = Attacker,
                Defender = Defender,
                Assisted = Assisted,
                AttackerTeam = AttackerTeam,
                DefenderTeam = DefenderTeam,
                AssistedTeam = AssistedTeam,
                Weapon = flag && pdr.client_id > -1 ? GetProjPrefab(pdr.dmg_type) : SanitizeWeaponID(di.weapon)
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
                weapon = flag && pdr.client_id > -1 ? GetProjPrefab(pdr.dmg_type).ToString() : SanitizeWeaponID(di.weapon).ToString()
            }, new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            TrackerPostStats(obj);

            Attacker = null;
            Defender = null;
            Assisted = null;
            AttackerTeam = MpTeam.ANARCHY;
            DefenderTeam = MpTeam.ANARCHY;
            AssistedTeam = MpTeam.ANARCHY;
        }

        public static void AddDamage(Player defender, Player attacker, ProjPrefab weapon, float damage)
        {
            if (NetworkMatch.m_postgame)
                return;

            //Debug.Log("CCF ServerStatLog AddDamage, damage type is " + SanitizeWeaponID(weapon).ToString());

            var key = new PlayerPlayerWeaponDamage { Attacker = attacker, Defender = defender, Weapon = SanitizeWeaponID(weapon) };
            if (DamageTable.TryGetValue(key, out float totalDamage))
                DamageTable[key] = totalDamage + damage;
            else
                DamageTable[key] = damage;
        }

        public static void AddFlagEvent(Player player, string @event, MpTeam flagTeam)
        {
            if (NetworkMatch.m_postgame)
                return;

            var capture = new FlagStat()
            {
                Time = NetworkMatch.m_match_elapsed_seconds,
                Event = @event,
                Scorer = player?.m_mp_name,
                ScorerTeam = player != null ? player.m_mp_team : flagTeam
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

            TrackerPostStats(obj);
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

        public static string GetMatchModeString(MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.ANARCHY: return "ANARCHY";
                case MatchMode.TEAM_ANARCHY: return "TEAM ANARCHY";
                case MatchMode.MONSTERBALL: return "MONSTERBALL";
                case CTF.MatchModeCTF: return "CTF";
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

        private static FieldInfo _Networkmatch_m_match_force_playlist_addon_idstringhash_Field = typeof(NetworkMatch).GetField("m_match_force_playlist_addon_idstringhash", AccessTools.all);
        private static FieldInfo _Networkmatch_m_match_force_playlist_level_idx_Field = typeof(NetworkMatch).GetField("m_match_force_playlist_level_idx", AccessTools.all);
        public static string GetCurrentLevelName()
        {
            string match_force_playlist_addon_idstringhash = (string)_Networkmatch_m_match_force_playlist_addon_idstringhash_Field.GetValue(null);
            int idx;
            if (string.IsNullOrEmpty(match_force_playlist_addon_idstringhash)) {
                idx = (int)_Networkmatch_m_match_force_playlist_level_idx_Field.GetValue(null);
            } else {
                idx = GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(match_force_playlist_addon_idstringhash);
                if (idx < 0) // unknown level
                    return Path.GetFileNameWithoutExtension(match_force_playlist_addon_idstringhash.Split(':')[0]).Replace('_', ' ').ToUpper();
            }
            if (idx < 0 || idx >= GameManager.MultiplayerMission.NumLevels)
                return null;
            return GameManager.MultiplayerMission.GetLevelDisplayName(idx);
        }

        public static JObject GetGameData()
        {
            return JObject.FromObject(new
            {
                creator = NetworkMatch.m_name.Split('\0')[0],
                forceModifier1 = NetworkMatch.m_force_modifier1 == 4 ? "OFF" : Player.GetMpModifierName(NetworkMatch.m_force_modifier1, true),
                forceModifier2 = NetworkMatch.m_force_modifier2 == 4 ? "OFF" : Player.GetMpModifierName(NetworkMatch.m_force_modifier2, false),
                forceMissile1 = NetworkMatch.m_force_m1.ToString()?.Replace('_', ' '),
                forceMissile2 = NetworkMatch.m_force_m2 == MissileType.NUM ? "NONE" : NetworkMatch.m_force_m2.ToString()?.Replace('_', ' '),
                forceWeapon1 = NetworkMatch.m_force_w1.ToString(),
                forceWeapon2 = NetworkMatch.m_force_w2 == WeaponType.NUM ? "NONE" : NetworkMatch.m_force_w2.ToString(),
                forceLoadout = MenuManager.GetToggleSetting(NetworkMatch.m_force_loadout),
                powerupFilterBitmask = NetworkMatch.m_powerup_filter_bitmask,
                powerupBigSpawn = GetPowerupBigSpawnString(NetworkMatch.m_powerup_big_spawn),
                powerupInitial = GetPowerupInitialString(NetworkMatch.m_powerup_initial),
                turnSpeedLimit = GetTurnSpeedLimitString(NetworkMatch.m_turn_speed_limit),
                powerupSpawn = GetPowerupSpawnString(NetworkMatch.m_powerup_spawn),
                friendlyFire = NetworkMatch.m_team_damage,
                matchMode = GetMatchModeString(NetworkMatch.GetMode()),
                maxPlayers = NetworkMatch.GetMaxPlayersForMatch(),
                showEnemyNames = NetworkMatch.m_show_enemy_names.ToString()?.Replace('_', ' '),
                timeLimit = NetworkMatch.m_match_time_limit_seconds,
                scoreLimit = NetworkMatch.m_match_score_limit,
                respawnTimeSeconds = NetworkMatch.m_respawn_time_seconds,
                respawnShieldTimeSeconds = NetworkMatch.m_respawn_shield_seconds,
                level = GetCurrentLevelName(),
                joinInProgress = MPJoinInProgress.NetworkMatchEnabled,
                rearViewAllowed = RearView.MPNetworkMatchEnabled,
                teamCount = MPTeams.NetworkMatchTeamCount,
                players = NetworkMatch.m_players.Values.Where(x => !x.m_name.StartsWith("OBSERVER")).Select(x => x.m_name),
                hasPassword = MPModPrivateData.HasPassword,
                matchNotes = MPModPrivateData.MatchNotes,
                classicSpawnsEnabled = MPClassic.matchEnabled,
                ctfCarrierBoostEnabled = CTF.CarrierBoostEnabled,
                suddenDeath = MPSuddenDeath.SuddenDeathMenuEnabled
            });
        }

        public static void StartGame()
        {
            StartTime = DateTime.UtcNow;
            CleanUp();
            GameStarted = true;

            var obj = GetGameData();
            obj["name"] = "Stats";
            obj["type"] = "StartGame";
            obj["start"] = DateTime.UtcNow.ToString("o");
            obj["time"] = 0;

            TrackerPostStats(obj);
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
            ServerStatLog.EndGame();
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    internal class LogOnDamage
    {
        public static void Prefix(DamageInfo di, PlayerShip __instance)
        {
            if (!Overload.NetworkManager.IsHeadless() || di.damage == 0f ||
                __instance.m_death_stats_recorded || __instance.m_cannot_die || __instance.c_player.m_invulnerable)
                return;
            var otherPlayer = di.owner?.GetComponent<Player>();

            float hitpoints = __instance.c_player.m_hitpoints;

            // Increase hitpoints by ratio of damage reduction so that we report the correct value.
            float reduction = Player.ARMOR_DAMAGE[__instance.c_player.m_upgrade_level[0]];
            if (di.type == DamageType.EXPLOSIVE && __instance.c_player.m_unlock_blast_damage) {
                reduction *= 0.8f;
            }
            hitpoints /= reduction;

            ProjPrefab weapon = di.weapon;

            float damage = di.damage;
            if (hitpoints - di.damage <= 0f)
                damage = hitpoints;
            ServerStatLog.AddDamage(__instance.c_player, otherPlayer, weapon, damage);
        }
    }

    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
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
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // pdr
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 5); // flag
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

    [HarmonyPatch(typeof(Server), "InvokeDisconnectFlashOnClients")]
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
