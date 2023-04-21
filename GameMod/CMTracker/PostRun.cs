using GameMod.CMTracker.Models;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.CMTracker
{
    [HarmonyPatch(typeof(Overload.GameplayManager), "DoneLevel")]
    internal class CMTracker_PostRun_GameplayManager_DoneLevel
    {
        static void Postfix(GameplayManager.DoneReason reason)
        {
            if (GameplayManager.IsChallengeMode && GameplayManager.m_level_info.Mission.FileName != "_EDITOR" && (int)ChallengeManager.ChallengeRobotsDestroyed > 0)
            {
                string url = $"{Config.Settings.Value<string>("trackerBaseUrl")}/api/challengemoderun";
                Post(url, GetTrackerPost());
            }
        }

        private static void Post(string url, Models.Run cmRun)
        {
            var request = new UnityWebRequest(url)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    JsonConvert.SerializeObject(cmRun, new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }))),
                downloadHandler = new DownloadHandlerBuffer(),
                method = UnityWebRequest.kHttpVerbPOST
            };
            request.SetRequestHeader("Content-Type", "application/json");
            GameManager.m_gm.StartCoroutine(RequestCoroutine(request.SendWebRequest()));
        }

        private static IEnumerator RequestCoroutine(UnityWebRequestAsyncOperation op)
        {
            yield return op;
        }

        private static Models.Run GetTrackerPost()
        {
            var request = new Models.Run
            {
                PilotName = PilotManager.PilotName,
                PlayerId = PlayerPrefs.GetString("UserID"),
                RobotsDestroyed = (int)ChallengeManager.ChallengeRobotsDestroyed,
                AliveTime = GameplayManager.AliveTime,
                Score = (int)ChallengeManager.ChallengeScore,
                LevelName = GameplayManager.Level.DisplayName,
                LevelHash = GameplayManager.Level.IsAddOn ? GameplayManager.Level.GetAddOnLevelIdStringHash() : GameplayManager.Level.FileName + ":STOCK",
                ModeId = Convert.ToInt32(ChallengeManager.CountdownMode),
                FavoriteWeaponId = GameplayManager.MostDamagingWeapon(),
                DifficultyLevelId = (int)GameplayManager.DifficultyLevel,
                KillerId = GameplayManager.m_stats_player_killer,
                SmashDamage = GameplayManager.m_robot_cumulative_damage_by_other_type[0],
                SmashKills = GameplayManager.m_other_killer[0],
                AutoOpDamage = GameplayManager.m_robot_cumulative_damage_by_other_type[1],
                AutoOpKills = GameplayManager.m_other_killer[1],
                SelfDamage = GameplayManager.m_stats_player_self_damage,
                StatsRobot = new List<StatRobot>(),
                StatsPlayer = new List<StatPlayer>()
            };

            var enemyTypes = Enum.GetNames(typeof(EnemyType));
            for (int i = 0; i < GameplayManager.m_player_cumulative_damage_by_robot_type.Length; i++)
            {
                if (GameplayManager.m_player_cumulative_damage_by_robot_type[i] != 0)
                {
                    request.StatsRobot.Add(new StatRobot
                    {
                        EnemyTypeId = i,
                        IsSuper = false,
                        DamageReceived = GameplayManager.m_player_cumulative_damage_by_robot_type[i],
                        DamageDealt = 0,
                        NumKilled = GameplayManager.m_robots_killed.Count(x => x.robot_type == (EnemyType)i)
                    });
                }

                if (GameplayManager.m_player_cumulative_damage_by_super_robot_type[i] != 0)
                {
                    request.StatsRobot.Add(new StatRobot
                    {
                        EnemyTypeId = i,
                        IsSuper = true,
                        DamageReceived = GameplayManager.m_player_cumulative_damage_by_super_robot_type[i],
                        DamageDealt = 0,
                        NumKilled = GameplayManager.m_super_robots_killed.Count(x => x.robot_type == (EnemyType)i)
                    });
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if (GameplayManager.m_robot_cumulative_damage_by_weapon_type[i] != 0 || GameplayManager.m_weapon_killer[i] != 0)
                {
                    request.StatsPlayer.Add(new StatPlayer
                    {
                        IsPrimary = true,
                        WeaponTypeId = i,
                        DamageDealt = GameplayManager.m_robot_cumulative_damage_by_weapon_type[i],
                        NumKilled = GameplayManager.m_weapon_killer[i]
                    });
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if (GameplayManager.m_robot_cumulative_damage_by_missile_type[i] != 0 || GameplayManager.m_missile_killer[i] != 0)
                {
                    request.StatsPlayer.Add(new StatPlayer
                    {
                        IsPrimary = false,
                        WeaponTypeId = i,
                        DamageDealt = GameplayManager.m_robot_cumulative_damage_by_missile_type[i],
                        NumKilled = GameplayManager.m_missile_killer[i]
                    });
                }
            }

            return request;
        }
    }
}