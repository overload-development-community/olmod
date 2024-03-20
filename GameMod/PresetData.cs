using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{

    public static class PresetData
    {
        public static bool ProjDataExists
        {
            get
            {
                return !String.IsNullOrEmpty(MPModPrivateData.CustomProjdata);
            }
        }

        public static void UpdateLobbyStatus()
        {
            if (ProjDataExists)
            {
                MenuManager.AddMpStatus("USING CUSTOM PROJDATA FOR THIS MATCH", 1f, 21);
            }
            else
            {
                // Clear status 21 so it doesn't incorrectly persist between lobbies
                var idx = Array.IndexOf(MenuManager.m_mp_status_id, 21);
                if (idx >= 0)
                {
                    MenuManager.m_mp_status_details[idx] = String.Empty;
                    MenuManager.m_mp_status_flash[idx] = 0f;
                    MenuManager.m_mp_status_id[idx] = -1;
                }
            }
        }

    }

    static class DataReader
    {
        public static string GetData(TextAsset ta, string filename)
        {
            string dir = Environment.GetEnvironmentVariable("OLMODDIR");
            try
            {
                return File.ReadAllText(dir + Path.DirectorySeparatorChar + filename);
            }
            catch (FileNotFoundException)
            {
            }
            return ta.text;
        }
        public static string GetProjData(TextAsset ta)
        {
            string projData;
            if (PresetData.ProjDataExists)
            {
                projData = MPModPrivateData.CustomProjdata;
            }
            else if (GameplayManager.IsMultiplayer)
            {
                projData = MPModPrivateData.DEFAULT_PROJ_DATA;
            }
            else
            {
                // Look for "projdata.txt" in SP/CM zip files and use if possible
                if (!GameplayManager.IsMultiplayer && GameplayManager.Level.IsAddOn)
                {
                    var filePaths = new string[]
                        {
                            Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.FileName}-projdata"),
                            Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.Mission.FileName}-projdata"),
                            Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), "projdata")
                        };
                    foreach (var filepath in filePaths)
                    {
                        string text3 = null;
                        byte[] array = Mission.LoadAddonData(GameplayManager.Level.ZipPath, filepath, ref text3, new string[]
                        {
                        ".txt"
                        });
                        if (array != null)
                        {
                            return System.Text.Encoding.UTF8.GetString(array);
                        }
                    }                    
                }
                projData = GetData(ta, "projdata.txt");
            }

            // Initialize any default round-start values for custom projdata entries here

            Cyclone.CycloneSpinupStartingStep = 0;

            return projData;
        }

        public static string GetRobotData(TextAsset ta)
        {
            // Look for "robotdata.txt" in SP/CM zip files and use if possible
            if (!GameplayManager.IsMultiplayer && GameplayManager.Level.IsAddOn)
            {
                var filePaths = new string[]
                    {
                        Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.FileName}-robotdata"),
                        Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.Mission.FileName}-robotdata"),
                        Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), "robotdata")
                    };
                foreach (var filepath in filePaths)
                {
                    string text3 = null;
                    byte[] array = Mission.LoadAddonData(GameplayManager.Level.ZipPath, filepath, ref text3, new string[]
                    {
                        ".txt"
                    });
                    if (array != null)
                    {
                        return System.Text.Encoding.UTF8.GetString(array);
                    }
                }                
            }
            return GetData(ta, "robotdata.txt");
        }

        public static void ReadFieldFilter(object obj, string name, string value, ProjPrefab id)
        {
            // define entries here for any custom data that should be read
            if (id == ProjPrefab.proj_vortex && name == "m_spinup_starting_time")
            {
                try
                {
                    int spinup = int.Parse(value);
                    if (spinup < 0 || spinup > 8)
                    {
                        Debug.LogError("Invalid m_spinup_starting_time, must be 0 to 8.");
                    }
                    else
                    {
                        Cyclone.CycloneSpinupStartingStep = spinup;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Invalid m_spinup_starting_time, error reading value: " + e);
                }
            }
            else // all other fields fall through like normal
            {
                RUtility.ReadField(obj, name, value);
            }
        }
    }

    [HarmonyPatch(typeof(ProjectileManager), "ReadProjPresetData")]
    class ReadProjPresetDataPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var dataReader_GetProjData_Method = typeof(DataReader).GetMethod("GetProjData");
            var dataReader_ReadFieldFilter_Method = typeof(DataReader).GetMethod("ReadFieldFilter");

            foreach (var code in instructions)
            {
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                {
                    yield return new CodeInstruction(OpCodes.Call, dataReader_GetProjData_Method);
                }
                else if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(RUtility), "ReadField"))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)6); // current ProjPrefab
                    yield return new CodeInstruction(OpCodes.Call, dataReader_ReadFieldFilter_Method);
                }
                else
                {
                    yield return code;
                }
            }
        }
    }

    [HarmonyPatch(typeof(RobotManager), "ReadPresetData")]
    class ReadRobotPresetDataPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var dataReader_GetRobotData_Method = typeof(DataReader).GetMethod("GetRobotData");
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                    yield return new CodeInstruction(OpCodes.Call, dataReader_GetRobotData_Method);
                else
                    yield return code;
        }
    }

    // Add annoying custom projdata HUD message when playing MP
    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class Preset_UIElement_DrawHUD
    {
        static void Postfix(UIElement __instance)
        {
            if (PresetData.ProjDataExists)
            {
                Vector2 vector = default(Vector2);
                vector.x = UIManager.UI_LEFT + 110;
                vector.y = UIManager.UI_TOP + 120f;
                __instance.DrawStringSmall("Using custom projdata", vector, 0.5f, StringOffset.CENTER, UIManager.m_col_damage, 1f);
            }
        }
    }

    // Update lobby status display
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class PresetData_MenuManager_MpMatchSetup
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE)
            {
                if (MenuManager.m_menu_micro_state != 2)
                {
                    PresetData.UpdateLobbyStatus();
                }
            }
        }
    }

    // Update lobby status display
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    class MPModifiers_NetworkMatch_OnAcceptedToLobby
    {
        static void Postfix()
        {
            PresetData.UpdateLobbyStatus();
        }
    }

    // Update lobby status display
    [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
    class MPModifiers_UIElement_DrawMpPreMatchMenu
    {
        static void Prefix()
        {
            PresetData.UpdateLobbyStatus();
        }
    }

    // Heavy-handed, re-init robot/projdatas on scene loaded
    [HarmonyPatch(typeof(GameplayManager), "OnSceneLoaded")]
    class PresetData_GameplayManager_OnSceneLoaded
    {
        static void LoadCustomPresets()
        {
            ProjectileManager.ReadProjPresetData(ProjectileManager.proj_prefabs);
            RobotManager.ReadPresetData(RobotManager.m_enemy_prefab);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(GameplayManager), "StartLevel"))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PresetData_GameplayManager_OnSceneLoaded), "LoadCustomPresets"));
                yield return code;
            }
        }
    }
}
