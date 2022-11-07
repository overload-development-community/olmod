using System;
using System.Linq;
using System.Reflection;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches {
    [Mod(Mods.Respawn)]
    [HarmonyPatch(typeof(NetworkSpawnPoints), "ChooseSpawnPoint")]
    public static class NetworkSpawnPoints_ChooseSpawnPoint {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        private static readonly MethodInfo _NetworkSpawnPoints_GetRandomRespawnPointWithoutFiltering_Method = AccessTools.Method(typeof(NetworkSpawnPoints), "GetRandomRespawnPointWithoutFiltering");
        public static bool Prefix(MpTeam team, ref LevelData.SpawnPoint __result) {
            // Check mode, bail if not Anarchy or Team Anarchy.
            var mode = NetworkMatch.GetMode();
            if (mode != MatchMode.ANARCHY && mode != MatchMode.TEAM_ANARCHY) {
                return true;
            }

            var respawnPointCandidates = Respawn.GetRespawnPointCandidates(team);

            if (respawnPointCandidates.Count == 0) {
                __result = (LevelData.SpawnPoint)_NetworkSpawnPoints_GetRandomRespawnPointWithoutFiltering_Method.Invoke(null, new object[] { });
            } else if (NetworkManager.m_Players.Count == 0) {
                __result = respawnPointCandidates[UnityEngine.Random.Range(0, respawnPointCandidates.Count)];
            } else {
                var scores = Respawn.GetRespawnPointScores(team, respawnPointCandidates, true);
                __result = scores.OrderByDescending(s => s.Value).First().Key;
            }

            Respawn.lastRespawn[__result] = DateTime.Now;
            return false;
        }
    }
}
