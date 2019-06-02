using Harmony;
using Overload;
using System;
using System.Collections.Generic;
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
    [HarmonyPatch(typeof(Overload.GameManager), "InitializeMissionList")]
    class AddOnLevelSort
    {
        static void Postfix()
        {
            try
            {
                FieldInfo fi = typeof(Mission).GetField("Levels", BindingFlags.NonPublic | BindingFlags.Instance);
                object __obj = fi.GetValue(GameManager.MultiplayerMission);
                List<LevelInfo> __m_mission_list = (List<LevelInfo>)__obj;

                __m_mission_list.Sort((a, b) => a.IsAddOn && b.IsAddOn ? a.DisplayName.CompareTo(b.DisplayName) : a.LevelNum-b.LevelNum);
                for (var i = 0; i < __m_mission_list.Count; i++)
                {
                    PropertyInfo ln = typeof(LevelInfo).GetProperty("LevelNum");
                    ln.DeclaringType.GetProperty("LevelNum");
                    ln.SetValue(__m_mission_list[i], i, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
                }
                fi.SetValue(GameManager.MultiplayerMission, __m_mission_list);
            } catch (Exception ex)
            {
                Debug.Log("InitializeMissionList() patch failed - " + ex.Message);
            }
        }
    }
}
