using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GameMod
{

    /// <summary>
    /// Author: Tobias
    /// Created: 2019-05-30
    /// Email: tobiasksu@gmail.com
    /// Sort addon levels by DisplayName instead of arbitrary load
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "InitializeMissionList")]
    class AddOnLevelSort
    {
        private static FieldInfo _GameManager_m_mission_list_Field = typeof(GameManager).GetField("m_mission_list", BindingFlags.NonPublic | BindingFlags.Static);
        private static MethodInfo _GameManager_ScanForLevels_Method = typeof(GameManager).GetMethod("ScanForLevels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _Mission_Levels_Field = typeof(Mission).GetField("Levels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static PropertyInfo _LevelInfo_LevelNum_Property = typeof(LevelInfo).GetProperty("LevelNum");
        static void Postfix(GameManager __instance)
        {
            try
            {
                // Scan for levels in -missionpath directory
                if (Core.GameMod.FindArgVal("-missionpath", out var path) && Directory.Exists(path)) {
                    var lastMission = ((List<Mission>)_GameManager_m_mission_list_Field.GetValue(null)).LastOrDefault();

                    int index = 0;

                    if (lastMission != null) {
                        index = lastMission.MissionIndex + 1;
                    }

                    var args = new object[] { path, index };
                    _GameManager_ScanForLevels_Method.Invoke(__instance, args);
                }

                // Sort addon levels by DisplayName instead of arbitrary load
                object __obj = _Mission_Levels_Field.GetValue(GameManager.MultiplayerMission);
                List<LevelInfo> __m_mission_list = (List<LevelInfo>)__obj;

                __m_mission_list.Sort((a, b) => a.IsAddOn && b.IsAddOn ? a.DisplayName.CompareTo(b.DisplayName) : a.LevelNum-b.LevelNum);
                for (var i = 0; i < __m_mission_list.Count; i++)
                {
                    _LevelInfo_LevelNum_Property.DeclaringType.GetProperty("LevelNum");
                    _LevelInfo_LevelNum_Property.SetValue(__m_mission_list[i], i, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
                }
                _Mission_Levels_Field.SetValue(GameManager.MultiplayerMission, __m_mission_list);
            } catch (Exception ex)
            {
                Debug.Log("InitializeMissionList() patch failed - " + ex.Message);
            }
        }
    }
}
