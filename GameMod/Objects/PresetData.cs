using System;
using System.IO;
using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    /// <summary>
    /// Handles custom projdata and robotdata files.
    /// </summary>
    [Mod(Mods.PresetData)]
    public static class PresetData {
        public static bool ProjDataExists {
            get {
                return !string.IsNullOrEmpty(MPModPrivateData.CustomProjdata);
            }
        }

        public static void UpdateLobbyStatus() {
            if (ProjDataExists) {
                MenuManager.AddMpStatus("USING CUSTOM PROJDATA FOR THIS MATCH", 1f, 21);
            } else {
                // Clear status 21 so it doesn't incorrectly persist between lobbies
                var idx = Array.IndexOf(MenuManager.m_mp_status_id, 21);
                if (idx >= 0) {
                    MenuManager.m_mp_status_details[idx] = String.Empty;
                    MenuManager.m_mp_status_flash[idx] = 0f;
                    MenuManager.m_mp_status_id[idx] = -1;
                }
            }
        }

        public static string GetData(TextAsset ta, string filename) {
            string dir = Environment.GetEnvironmentVariable("OLMODDIR");
            try {
                return File.ReadAllText(dir + Path.DirectorySeparatorChar + filename);
            } catch (FileNotFoundException) { }
            return ta.text;
        }

        public static string GetProjData(TextAsset ta) {
            if (PresetData.ProjDataExists) {
                return MPModPrivateData.CustomProjdata;
            } else if (GameplayManager.IsMultiplayer) {
                return DefaultProjData.DEFAULT_PROJ_DATA;
            } else {
                // Look for "projdata.txt" in SP/CM zip files and use if possible
                if (!GameplayManager.IsMultiplayer && GameplayManager.Level.IsAddOn) {
                    var filePaths = new string[]
                        {
                            Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.FileName}-projdata"),
                            Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.Mission.FileName}-projdata"),
                            Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), "projdata")
                        };
                    foreach (var filepath in filePaths) {
                        string text3 = null;
                        byte[] array = Mission.LoadAddonData(GameplayManager.Level.ZipPath, filepath, ref text3, new string[]
                        {
                        ".txt"
                        });
                        if (array != null) {
                            return System.Text.Encoding.UTF8.GetString(array);
                        }
                    }
                }
                return GetData(ta, "projdata.txt");
            }

        }

        public static string GetRobotData(TextAsset ta) {
            // Look for "robotdata.txt" in SP/CM zip files and use if possible
            if (!GameplayManager.IsMultiplayer && GameplayManager.Level.IsAddOn) {
                var filePaths = new string[]
                    {
                        Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.FileName}-robotdata"),
                        Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.Mission.FileName}-robotdata"),
                        Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), "robotdata")
                    };
                foreach (var filepath in filePaths) {
                    string text3 = null;
                    byte[] array = Mission.LoadAddonData(GameplayManager.Level.ZipPath, filepath, ref text3, new string[]
                    {
                        ".txt"
                    });
                    if (array != null) {
                        return System.Text.Encoding.UTF8.GetString(array);
                    }
                }
            }
            return GetData(ta, "robotdata.txt");
        }
    }
}
