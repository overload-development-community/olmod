using HarmonyLib;
using Newtonsoft.Json;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.CMTracker
{
    [HarmonyPatch(typeof(Platform), "Init")]
    internal class CMTracker_Platform_Init
    {
        static void Postfix()
        {
            AccessTools.Field(typeof(Platform), "CloudProvider").SetValue(null, 4);
        }
    }

    [HarmonyPatch(typeof(Platform), "UserName", MethodType.Getter)]
    internal class CMTracker_Platform_UserName
    {
        static void Postfix(ref string __result)
        {
            __result = PilotManager.PilotName;
        }
    }

    [HarmonyPatch(typeof(Platform), "PlatformName", MethodType.Getter)]
    internal class CMTracker_Platform_PlatformName
    {
        public static void Postfix(ref string __result)
        {
            __result = "OLMOD";
        }
    }

    [HarmonyPatch(typeof(Platform), "StatsAvailable", MethodType.Getter)]
    internal class CMTracker_Platform_PlatformStatsAvailable
    {
        public static void Postfix(ref bool __result)
        {
            __result = true;
        }
    }

    [HarmonyPatch(typeof(Platform), "OnlineErrorMessage", MethodType.Getter)]
    internal class CMTracker_Platform_OnlineErrorMessage
    {
        public static void Postfix(ref string __result)
        {
            __result = null;
        }
    }

    [HarmonyPatch(typeof(Platform), "GetLeaderboardData")]
    internal class CMTracker_Platform_GetLeaderboardData
    {
        public static void Postfix(ref LeaderboardEntry[] __result, out int leaderboard_length, out int user_index, out Platform.LeaderboardDataState result)
        {
            user_index = -1;
            leaderboard_length = 0;

            if (m_download_state == DownloadState.NoLeaderboard)
            {
                result = Platform.LeaderboardDataState.NoLeaderboard;
                __result = null;
                return;
            }
            if (m_download_state == DownloadState.RetryFromStart)
            {
                m_request_start = 1;
                try
                {
                    Platform.RequestChallengeLeaderboardData(MenuManager.ChallengeMission.DisplayName, MenuManager.m_leaderboard_challenge_countdown, MenuManager.m_leaderboard_difficulty, 1, m_request_num_entries - 1, false);
                    m_download_state = DownloadState.WaitingForData;
                    result = Platform.LeaderboardDataState.Waiting;
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error requesting olmod leaderboard entries: {ex.Message}");
                    m_download_state = DownloadState.NoLeaderboard;
                    result = Platform.LeaderboardDataState.NoLeaderboard;
                }
                __result = null;
                return;
            }
            if (m_download_state != DownloadState.HaveData)
            {
                result = Platform.LeaderboardDataState.Waiting;
                __result = null;
                return;
            }

            LeaderboardEntry[] array = new LeaderboardEntry[m_entries.Length];
            for (int i = 0; i < m_entries.Length; i++)
            {
                if (m_entries[i].m_name == null)
                {
                    m_entries[i].m_name = "m_name here";
                    m_entries[i].m_rank = i + 1;
                }
                array[i] = m_entries[i];
            }
            leaderboard_length = m_entries.Length;
            result = Platform.LeaderboardDataState.HaveData;
            __result = array;
        }

        public static DownloadState m_download_state = DownloadState.RetryFromStart;
        public static int m_request_start;
        public static int m_request_num_entries = 0;
        public static int m_leaderboard_length = 0;
        public static LeaderboardEntry[] m_entries;

        public enum DownloadState
        {
            WaitingForData,
            NoLeaderboard,
            RetryFromStart,
            HaveData
        }
    }

    [HarmonyPatch(typeof(Platform), "RequestChallengeLeaderboardData")]
    internal static class CMRequestChallengeLeaderboardData
    {
        public static CloudDataYield Postfix(CloudDataYield __result, string level_name, bool submode, int difficulty_level, int range_start, int num_entries, bool friends)
        {
            var levelHash = MenuManager.ChallengeMission.IsLevelAnAddon(MenuManager.m_leaderboard_level_num)
                ? MenuManager.ChallengeMission.GetAddOnLevelIdStringHash(MenuManager.m_leaderboard_level_num)
                : MenuManager.ChallengeMission.GetLevelFileName(MenuManager.m_leaderboard_level_num) + ":STOCK";
            CMTracker_Platform_GetLeaderboardData.m_download_state = CMTracker_Platform_GetLeaderboardData.DownloadState.WaitingForData;
            GameManager.m_gm.StartCoroutine(DownloadLeaderboard(level_name, levelHash, Convert.ToInt32(submode), difficulty_level));
            CloudDataYield cdy = new CloudDataYield(() => CMTracker_Platform_GetLeaderboardData.m_download_state != CMTracker_Platform_GetLeaderboardData.DownloadState.HaveData &&
                CMTracker_Platform_GetLeaderboardData.m_download_state != CMTracker_Platform_GetLeaderboardData.DownloadState.NoLeaderboard);

            __result = cdy;
            return null;
        }

        static IEnumerator DownloadLeaderboard(string levelName, string levelHash, int modeId, int difficultyLevelId)
        {
            var url = $"{Config.Settings.Value<string>("trackerBaseUrl")}/api/challengemodeleaderboard?levelHash={levelHash}&difficultyLevelId={difficultyLevelId}&modeId={modeId}";
            
            UnityWebRequest www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log($"DownloadLeaderboard Error: {www.error}");
            }
            else
            {
                List<Models.LeaderboardEntry> results = JsonConvert.DeserializeObject<List<Models.LeaderboardEntry>>(www.downloadHandler.text);

                CMTracker_Platform_GetLeaderboardData.m_entries = results.Select(x => new LeaderboardEntry
                {
                    m_data_is_valid = true,
                    m_favorite_weapon = x.FavoriteWeaponId,
                    m_game_time = (int)Math.Round(x.AliveTime),
                    m_kills = x.RobotsDestroyed,
                    m_name = x.PilotName,
                    m_rank = 1,
                    m_score = x.Score,
                    m_time_stamp = DateTime.Now
                }).ToArray();
                CMTracker_Platform_GetLeaderboardData.m_download_state = CMTracker_Platform_GetLeaderboardData.DownloadState.HaveData;
            }
        }
    }
}