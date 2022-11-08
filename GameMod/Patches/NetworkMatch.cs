using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using GameMod.Objects.Models;
using GameMod.VersionHandling;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches {
    /// <summary>
    /// Refresh the weapon spawn budget with the specified type.
    /// </summary>
    [Mod(Mods.PrimarySpawns)]
    [HarmonyPatch(typeof(NetworkMatch), "AddWeaponSpawn")]
    public static class NetworkMatch_AddWeaponSpawn {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(WeaponType wt) {
            // Get the primary from the budget list, bail if there isn't one.
            var primary = PrimarySpawns.Budget.Find(b => b.Type == wt);
            if (primary == null) {
                return false;
            }

            // Bail if there are none active.
            if (primary.Active == 0) {
                return false;
            }

            // Restore some remaining budget, decrement the active count.
            primary.Remaining += (primary.Budget - primary.Remaining) / primary.Active;
            primary.Active--;

            // Set timer if it's less than 0.
            if (PrimarySpawns.SpawnWeaponTimer <= 0) {
                if (MPClassic.matchEnabled) {
                    PrimarySpawns.SpawnWeaponTimer = UnityEngine.Random.Range(15f, 30f);
                } else {
                    PrimarySpawns.SpawnWeaponTimer = UnityEngine.Random.Range(60f, 120f);
                }
            }

            // Short circuit the original code.
            return false;
        }
    }

    /// <summary>
    /// Attempts to download the level if missing, and set players to start match to 1 if not already.
    /// </summary>
    [Mod(new Mods[] { Mods.DownloadLevels, Mods.OnePlayerMultiplayerGames })]
    [HarmonyPatch(typeof(NetworkMatch), "ApplyPrivateMatchSettings")]
    public static class NetworkMatch_ApplyPrivateMatchSettings {
        [Mod(new Mods[] { Mods.DownloadLevels })]
        private static void TryDownloadLevelIfMissing(ref bool __result, PrivateMatchDataMessage pmd) {
            // unknown level?
            if (!__result && !Switches.NoDownload && !string.IsNullOrEmpty(pmd.m_addon_level_name_hash)) {
                MPDownloadLevel.StartGetLevel(pmd.m_addon_level_name_hash);
                __result = true;
            }
        }

        [Mod(new Mods[] { Mods.DownloadLevels, Mods.OnePlayerMultiplayerGames })]
        public static void Postfix(ref bool __result, PrivateMatchDataMessage pmd, ref int ___m_num_players_to_start_match) {
            if (___m_num_players_to_start_match != 1) // Always allow game to start with 1 player.
                ___m_num_players_to_start_match = 1;
            TryDownloadLevelIfMissing(ref __result, pmd);
        }
    }

    /// <summary>
    /// Doubles the time allotted to wait for a client to start the match.
    /// </summary>
    [Mod(Mods.LaunchCountdown)]
    [HarmonyPatch(typeof(NetworkMatch), "CanLaunchCountdown")]
    public static class MPTweaks_NetworkMatch_CanLaunchCountdown {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldc_R4) {
                    code.operand = 60f;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Reports the end of the game to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
    public static class NetworkMatch_ExitMatch {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix() {
            if (!NetworkManager.IsHeadless())
                return;
            Tracker.EndGame();
        }
    }

    /// <summary>
    /// Adjust highest score so kill goals work as expected in no-assist games.
    /// </summary>
    [Mod(Mods.AssistScoring)]
    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScoreAnarchy")]
    public static class NetworkMatch_GetHighestScoreAnarchy {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static int Postfix(int result) {
            return MPModPrivateData.AssistScoring ? result : (result / 3);
        }
    }

    /// <summary>
    /// Gets the highest team score in a monsterball match.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScorePowercore")]
    public static class NetworkMatch_GetHighestScorePowercore {
        public static bool Prefix(ref int __result) {
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || Teams.NetworkMatchTeamCount == 2)
                return true;
            __result = Teams.HighestScore();
            return false;
        }
    }

    /// <summary>
    /// Gets the highest team score in a team anarchy match.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScoreTeamAnarchy")]
    public static class NetworkMatch_GetHighestScoreTeamAnarchy {
        public static bool Prefix(ref int __result) {
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || Teams.NetworkMatchTeamCount == 2)
                return true;
            __result = Teams.HighestScore();
            return false;
        }
    }

    /// <summary>
    /// Gets the team name.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "GetTeamName")]
    public static class NetworkMatch_GetTeamName {
        public static bool Prefix(MpTeam team, ref string __result) {
            __result = Teams.TeamName(team);
            return false;
        }
    }

    /// <summary>
    /// Allows game to start even if teams are severely unbalanced.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch]
    public static class NetworkMatch_HostActiveMatchInfo_CanStartNow {
        public static MethodBase TargetMethod() {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetMethod("CanStartNow", BindingFlags.Public | BindingFlags.Instance);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) {
            foreach (var c in cs) {
                if (c.opcode == OpCodes.Ldsfld && ((FieldInfo)c.operand).Name == "m_match_mode") {
                    var c2 = new CodeInstruction(OpCodes.Ldc_I4_1) { labels = c.labels };
                    yield return c2;
                    yield return new CodeInstruction(OpCodes.Ret);
                    c.labels = null;
                }
                yield return c;
            }
        }
    }

    /// <summary>
    /// Change the 60 second "Player Joining" timeout to 5 seconds.
    /// </summary>
    [Mod(Mods.ReducePlayerJoinTimeout)]
    [HarmonyPatch]
    public static class NetworkMatch_HostPlayerMatchmakerInfo_GetStatus {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static MethodBase TargetMethod() {
            return typeof(NetworkMatch).GetNestedType("HostPlayerMatchmakerInfo", AccessTools.all).GetMethod("GetStatus");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (state == 0) {
                    if (code.opcode == OpCodes.Ldc_R8 && (double)code.operand == 60) {
                        state = 1;
                        yield return new CodeInstruction(OpCodes.Ldc_R8, 5.0);
                    } else {
                        yield return code;
                    }
                } else {
                    yield return code;
                }
            }
            if (state != 1) {
                Debug.LogFormat("MPReduceJoinTimeout: transpiler failed at state {0}", state);
            }
        }
    }

    /// <summary>
    /// Resets the team scores at the start of the game.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "Init")]
    public static class NetworkMatch_Init {
        public static void Prefix() {
            NetworkMatch.m_team_scores = new int[(int)Teams.MPTEAM_NUM];
        }
    }

    /// <summary>
    /// Sets overtime off, initialize the tweaks before each match, and resets the team scores.
    /// </summary>
    [Mod(new Mods[] { Mods.Respawn, Mods.SuddenDeath, Mods.Teams, Mods.Tweaks })]
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    public static class NetworkMatch_InitBeforeEachMatch {
        public static void Postfix() {
            Respawn.spawnPointsFromItems.Clear();
            Respawn.spawnPointDistances.Clear();

            SuddenDeath.InOvertime = false;

            for (int i = 0, l = NetworkMatch.m_team_scores.Length; i < l; i++)
                NetworkMatch.m_team_scores[i] = 0;

            Tweaks.InitMatch();
        }
    }

    /// <summary>
    /// When sudden death overtime is enabled, this code ensures overtime begins when time expires, and ends when the tie is broken.
    /// </summary>
    [Mod(Mods.SuddenDeath)]
    [HarmonyPatch(typeof(NetworkMatch), "MaybeEndTimer")]
    public static class NetworkMatch_MaybeEndTimer {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix() {
            if (
                NetworkMatch.m_match_elapsed_seconds > NetworkMatch.m_match_time_limit_seconds
                && (NetworkMatch.GetMode() == MatchMode.MONSTERBALL || NetworkMatch.GetMode() == CTF.MatchModeCTF)
                && SuddenDeath.Enabled
                && NetworkMatch.m_team_scores[(int)MpTeam.TEAM0] == NetworkMatch.m_team_scores[(int)MpTeam.TEAM1]
            ) {
                if (!SuddenDeath.InOvertime) {
                    SuddenDeath.InOvertime = true;
                    MPHUDMessage.SendToAll("Sudden Death!");

                }
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Try to spawn a primary if the timer is ready for it, and change the original algorithm for basic powerup spawns if the map implemented it.
    /// </summary>
    [Mod(new Mods[] { Mods.BasicPowerupSpawns, Mods.PrimarySpawns })]
    [HarmonyPatch(typeof(NetworkMatch), "MaybeSpawnPowerup")]
    public static class NetworkMatch_MaybeSpawnPowerup {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        [Mod(Mods.BasicPowerupSpawns)]
        private static ItemPrefab PowerupTypeToPrefab(BasicPowerupSpawns.PowerupType pt) {
            switch (pt) {
                case BasicPowerupSpawns.PowerupType.HEALTH:
                    return ItemPrefab.entity_item_shields;
                case BasicPowerupSpawns.PowerupType.ENERGY:
                    return ItemPrefab.entity_item_energy;
                case BasicPowerupSpawns.PowerupType.AMMO:
                    return ItemPrefab.entity_item_ammo;
                case BasicPowerupSpawns.PowerupType.ALIENORB:
                    return ItemPrefab.entity_item_alien_orb;
                default:
                    return ItemPrefab.none;
            }
        }

        [Mod(Mods.BasicPowerupSpawns)]
        public static ItemPrefab RandomBasicSpawn() {
            if (BasicPowerupSpawns.m_multiplayer_spawnable_powerups.Count > 0) {
                // Somewhat ugly but consistent/based on NetworkMatch.RandomAllowedMissileSpawn()
                // New algorithm
                float[] array = new float[4];
                for (int i = 0; i < BasicPowerupSpawns.m_multiplayer_spawnable_powerups.Count; i++) {
                    BasicPowerupSpawns.PowerupType type = (BasicPowerupSpawns.PowerupType)BasicPowerupSpawns.m_multiplayer_spawnable_powerups[i].type;
                    array[(int)type] = BasicPowerupSpawns.m_multiplayer_spawnable_powerups[i].percent;
                }
                float num = 0f;
                for (int j = 0; j < 4; j++) {
                    num += array[j];
                }

                if (num > 0f) {
                    for (int k = 0; k < 4; k++) {
                        array[k] /= num;
                    }
                    float num2 = UnityEngine.Random.Range(0f, 1f);
                    float num3 = 0f;
                    for (int l = 0; l < 4; l++) {
                        if (num2 < num3 + array[l]) {
                            return PowerupTypeToPrefab((BasicPowerupSpawns.PowerupType)l);
                        }
                        num3 += array[l];
                    }
                    Debug.Log("We had valid powerups, but couldn't choose one when spawning it");
                    return ItemPrefab.num;
                }
            } else {
                // Original algorithm
                int num = UnityEngine.Random.Range(0, 4);
                if (NetworkMatch.AnyPlayersHaveAmmoWeapons()) {
                    num = UnityEngine.Random.Range(0, 5);
                }
                switch (num) {
                    case 1:
                    case 2:
                        return ItemPrefab.entity_item_energy;
                    case 3:
                    case 4:
                        return ItemPrefab.entity_item_ammo;
                    default:
                        return ItemPrefab.entity_item_shields;
                }
            }

            return ItemPrefab.num;
        }

        [Mod(Mods.PrimarySpawns)]
        public static void Prefix() {
            PrimarySpawns.SpawnWeaponTimer -= RUtility.FRAMETIME_GAME;
            if (PrimarySpawns.SpawnWeaponTimer <= 0f) {
                PrimarySpawns.SpawnPrimary();
            }
        }

        [Mod(Mods.BasicPowerupSpawns)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = instructions.ToList();
            int m_spawn_basic_timer_index = -1;
            int removeStart = -1;
            int removeEnd = -1;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].operand == AccessTools.Field(typeof(NetworkMatch), "m_spawn_basic_timer"))
                    m_spawn_basic_timer_index++;

                if (codes[i].opcode == OpCodes.Ldc_I4_0 && m_spawn_basic_timer_index == 3) {
                    removeStart = i;
                    m_spawn_basic_timer_index = -3;
                }

                if (codes[i].operand == AccessTools.Method(typeof(NetworkMatch), "SetSpawnBasicTimer")) {
                    removeEnd = i;
                }
            }

            if (removeStart >= 0 && removeEnd >= 0) {
                codes.RemoveRange(removeStart, removeEnd - removeStart - 1);
                codes.InsertRange(removeStart, new List<CodeInstruction>() {
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NetworkMatch_MaybeSpawnPowerup), "RandomBasicSpawn")),
                    new CodeInstruction(OpCodes.Stloc_2),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldc_I4_0)
                });
            }

            return codes;
        }
    }

    /// <summary>
    /// Balances teams when new players join.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "NetSystemGetTeamForPlayer")]
    public static class NetworkMatch_NetSystemGetTeamForPlayer {
        public static bool Prefix(ref MpTeam __result, int connection_id) {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode())) {
                __result = MpTeam.ANARCHY;
                return false;
            }
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || (Teams.NetworkMatchTeamCount == 2 &&
                !MPJoinInProgress.NetworkMatchEnabled)) // use this simple balancing method for JIP to hopefully solve JIP team imbalances
                return true;
            if (NetworkMatch.m_players.TryGetValue(connection_id, out var connPlayer)) // keep team if player already exists (when called from OnUpdateGameSession)
            {
                __result = connPlayer.m_team;
                return false;
            }
            int[] team_counts = new int[(int)Teams.MPTEAM_NUM];
            foreach (var player in NetworkMatch.m_players.Values)
                team_counts[(int)player.m_team]++;
            MpTeam min_team = MpTeam.TEAM0;
            foreach (var team in Teams.GetTeams)
                if (team_counts[(int)team] < team_counts[(int)min_team] ||
                    (team_counts[(int)team] == team_counts[(int)min_team] &&
                        NetworkMatch.m_team_scores[(int)team] < NetworkMatch.m_team_scores[(int)min_team]))
                    min_team = team;
            __result = min_team;
            Debug.LogFormat("GetTeamForPlayer: result {0}, conn {1}, counts {2}, scores {3}", (int)min_team, connection_id,
                team_counts.Join(), NetworkMatch.m_team_scores.Join());
            return false;
        }
    }

    /// <summary>
    /// Update lobby status display.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public static class NetworkMatch_OnAcceptedToLobby_PresetData {
        public static void Postfix() {
            PresetData.UpdateLobbyStatus();
        }
    }

    /// <summary>
    /// Send client capabilities for compatibility.
    /// </summary>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public static class NetworkMatch_OnAcceptedToLobby_Tweaks {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Prefix(AcceptedToLobbyMessage accept_msg) {
            if (Client.GetClient() == null) {
                return;
            }

            var server = accept_msg.m_server_location;
            if (!server.StartsWith("OLMOD ")) {
                // other server / server too old
                Debug.LogFormat("MPTweaks: unsupported server {0}", server);
                return;
            }

            var caps = new Dictionary<string, string> {
                { "ModVersion", OlmodVersion.RunningVersion.ToString(OlmodVersion.RunningVersion.Revision == 0 ? 3 : 4) },
                { "ModFullVersion", OlmodVersion.FullVersionString },
                { "Modded", OlmodVersion.Modded ? "1" : "0" },
                { "ModsLoaded", Core.GameMod.ModsLoaded },
                { "SupportsTweaks", "" }
            };
            Client.GetClient().Send(MessageTypes.MsgClientCapabilities, new TweaksMessage { m_settings = caps });
        }
    }

    /// <summary>
    /// Override the original PowerupLevelStart to use a primary budget.
    /// </summary>
    [Mod(Mods.PrimarySpawns)]
    [HarmonyPatch(typeof(NetworkMatch), "PowerupLevelStart")]
    public static class NetworkMatch_PowerupLevelStart {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix(ref int[] ___m_spawn_weapon_count) {
            // Setup the primary budget.
            PrimarySpawns.Budget.Clear();
            foreach (var weapon in RobotManager.m_multiplayer_spawnable_weapons) {
                var weaponType = (WeaponType)weapon.type;
                if (NetworkMatch.IsWeaponAllowed(weaponType)) {
                    PrimarySpawns.Budget.Add(new PrimaryBudget {
                        Type = weaponType,
                        Budget = weapon.percent,
                        Remaining = weapon.percent,
                        Active = 0
                    });
                }
            }

            // Spawn in initial primaries.
            var primaries = PrimarySpawns.GetMaxPrimaries();

            for (int i = 0; i < primaries; i++) {
                PrimarySpawns.SpawnPrimary();
            }

            // This is the rest of the PowerupLevelStart code, currently unmodified.
            var num = RobotManager.m_multi_missile_count;
            for (int n = 0; n < num; n++) {
                MissileType missileType = NetworkMatch.RandomAllowedMissileSpawn();
                if (missileType != MissileType.NUM) {
                    ItemPrefab item2 = ChallengeManager.MissileTypeToPrefab(missileType);
                    // NetworkMatch.SpawnItem(item2, false);
                    AccessTools.Method(typeof(NetworkMatch), "SpawnItem").Invoke(null, new object[] { item2, false });
                }
            }
            NetworkMatch.SetSpawnMissileTimer();
            NetworkMatch.SetSpawnSuperTimer();
            NetworkMatch.SetSpawnBasicTimer();

            // Get rid of weapon counts so they aren't triggered by the original function.
            for (var i = 0; i < 8; i++) {
                ___m_spawn_weapon_count[i] = 0;
            }

            // Short circuit the original code.
            return false;
        }
    }

    [Mod(Mods.Respawn)]
    [HarmonyPatch(typeof(NetworkMatch), "ProcessPregame")]
    public static class NetworkMatch_ProcessPregame {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Prefix() {
            // Check mode, bail if not Anarchy or Team Anarchy.
            var mode = NetworkMatch.GetMode();
            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return;
            }

            // Bail if we have spawn points.
            if (Respawn.spawnPointDistances.Count > 0) {
                return;
            }

            // If enabled, get all of the item spawn points and check to see if they can be reused as player spawn points.
            if (Respawn.EnableItemSpawnsAsPlayerSpawns) {
                for (int i = 0; i < GameManager.m_level_data.m_item_spawn_points.Length; i++) {
                    // TODO: Figure out how to bail when supers are involved.
                    // if (spawn point is super) {
                    //     continue;
                    // }

                    var itemSpawnPoint = GameManager.m_level_data.m_item_spawn_points[i];

                    float largestDistance = 0f;
                    Quaternion largestQuaternion = Quaternion.identity;

                    for (float angle = 0f; angle < 360f; angle += 15f) {
                        var quaternion = Quaternion.Euler(0, angle, 0);

                        if (Physics.Raycast(itemSpawnPoint.position, quaternion * Vector3.forward, out RaycastHit hitInfo, 9999f)) {
                            var distance = hitInfo.distance;
                            if (distance > largestDistance) {
                                largestDistance = distance;
                                largestQuaternion = quaternion;
                            }
                        }
                    }

                    if (largestDistance > 20f) {
                        Respawn.spawnPointsFromItems.Add(new Respawn.ItemSpawnPoint(i, largestQuaternion));
                    }
                }
            }

            // Generate a lookup of the distance between spawn points.
            var spawnPoints = new List<LevelData.SpawnPoint>();
            spawnPoints.AddRange(Respawn.spawnPointsFromItems.Select(s => s.spawnPoint));
            spawnPoints.AddRange(GameManager.m_level_data.m_player_spawn_points);

            foreach (var spawn1 in spawnPoints) {
                var position = spawn1.position;
                var segnum = GameManager.m_level_data.FindSegmentContainingWorldPosition(position);
                Respawn.spawnPointDistances.Add(spawn1, new Dictionary<LevelData.SpawnPoint, float>());
                foreach (var spawn2 in spawnPoints) {
                    if (spawn1 == spawn2) {
                        continue;
                    }

                    var position2 = spawn2.position;
                    var segnum2 = GameManager.m_level_data.FindSegmentContainingWorldPosition(position2);

                    var distance = RUtility.FindVec3Distance(position - position2);

                    Respawn.spawnPointDistances[spawn1].Add(spawn2, distance);
                }
            }
        }
    }

    /// <summary>
    /// Resets level downloading.
    /// </summary>
    [Mod(new Mods[] { Mods.DownloadLevels })]
    [HarmonyPatch(typeof(NetworkMatch), "SetDefaultMatchSettings")]
    public static class NetworkMatch_SetDefaultMatchSettings {
        public static void Postfix() {
            MPDownloadLevel.Reset();
        }
    }

    /// <summary>
    /// Sets the timer for basic spawns.
    /// </summary>
    [Mod(Mods.BasicPowerupSpawns)]
    [HarmonyPatch(typeof(NetworkMatch), "SetSpawnBasicTimer")]
    public static class MPTags_NetworkMatch_SetSpawnBasicTimer {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static bool Prefix() {
            if (BasicPowerupSpawns.m_multi_powerup_frequency <= 0f)
                return true;

            float num = Mathf.Max(1f, BasicPowerupSpawns.m_multi_powerup_frequency);
            int count = NetworkMatch.m_players.Count;
            num *= NetworkMatch.GetNumPlayerSpawnModifier(count);
            int count2 = RobotManager.m_master_item_list.Count;
            if (count2 > 10) {
                num += (count2 - 10) * 0.5f;
            }
            num *= NetworkMatch.GetSpawnMultiplier();
            AccessTools.Field(typeof(NetworkMatch), "m_spawn_basic_timer").SetValue(null, num);
            return false;
        }
    }

    /// <summary>
    /// Only start super spawn timer if super spawn actually exists in the level.
    /// </summary>
    [Mod(Mods.DisableSuperSpawn)]
    [HarmonyPatch(typeof(NetworkMatch), "SetSpawnSuperTimer")]
    public static class NetworkMatch_SetSpawnSuperTimer {
        public static bool Prefix(ref float ___m_spawn_super_timer) {
            if (GameManager.m_level_data.m_item_spawn_points.Any(x => x.multiplayer_team_association_mask == 1) && // 1 -> is super
                RobotManager.m_multiplayer_spawnable_supers.Count != 0)
                return true;
            ___m_spawn_super_timer = -1f;
            return false;
        }
    }

    /// <summary>
    /// Starts pinging the tracker every 5 minutes.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(NetworkMatch), "StartLobby")]
    public static class NetworkMatch_StartLobby {
        private static bool started = false;

        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Postfix() {
            if (!started && NetworkManager.IsHeadless()) {
                started = true;
                GameManager.m_gm.StartCoroutine(PingRoutine());
            }
        }

        public static IEnumerator PingRoutine() {
            while (true) {
                Tracker.Ping();
                yield return new WaitForSecondsRealtime(5 * 60);
            }
        }
    }

    /// <summary>
    /// Reports the start of the game to the tracker, and resets the team scores.
    /// </summary>
    [Mod(new Mods[] { Mods.Teams, Mods.Tracker })]
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    public static class NetworkMatch_StartPlaying {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix() {
            if (NetworkManager.IsHeadless()) {
                Tracker.StartGame();
            }

            for (int i = 0, l = NetworkMatch.m_team_scores.Length; i < l; i++)
                NetworkMatch.m_team_scores[i] = 0;
        }
    }

    /// <summary>
    /// Disables updating of Gamelift pings.
    /// </summary>
    [Mod(Mods.DisableGamelift)]
    [HarmonyPatch(typeof(NetworkMatch), "UpdateGameliftPings")]
    public static class NetworkMatch_UpdateGameliftPings {
        public static bool Prefix() {
            return false;
        }
    }
}
