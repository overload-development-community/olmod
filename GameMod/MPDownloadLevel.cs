using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Ionic.Zip;
using Newtonsoft.Json.Linq;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod
{
    static class MPDownloadLevel
    {
        public static bool DownloadBusy;
        private static string LastDownloadAttempt;
        public static string LastStatus;
        private static string LastError;

        public static void Reset()
        {
            DownloadBusy = false;
            LastDownloadAttempt = null;
            LastStatus = null;
            LastError = null;
        }

        private static bool CanCreateFile(string filename)
        {
            try
            {
                File.Create(filename).Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ZipContainsLevel(string zipFilename, string levelIdHash)
        {
            if (!File.Exists(zipFilename))
            {
                Debug.Log($"ZipContainsLevel: File \"{zipFilename}\" not found");
                return false;
            }
            string[] parts = levelIdHash.Split(new[] { ':' });
            string levelFile = parts[0];
            if (!int.TryParse(parts[1], NumberStyles.HexNumber, null, out int hash))
            {
                //Debug.Log("ZipContainsLevel: invalid levelIdHash " + levelIdHash);
                return false;
            }
            try
            {
                using (var zip = ZipFile.Read(zipFilename))
                    foreach (var entry in zip)
                        if (!entry.IsDirectory && entry.Crc == hash &&
                            Path.GetFileName(entry.FileName).Equals(levelFile, StringComparison.InvariantCultureIgnoreCase))
                            return true;
            }
            catch (Exception ex)
            {
                Debug.Log("ZipContainsLevel: reading " + zipFilename + ": " + ex);
            }
            return false;
        }

        private static FieldInfo _Mission_Levels_Field = typeof(Mission).GetField("Levels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static IEnumerable<ILevelDownloadInfo> GetMPLevels()
        {
            var levels = _Mission_Levels_Field.GetValue(GameManager.MultiplayerMission) as List<LevelInfo>;
            foreach (var level in levels)
            {
                yield return new LevelDownloadInfoAdapter(level);
            }
        }

        private class LevelDownloadInfoAdapter : ILevelDownloadInfo
        {
            private readonly LevelInfo level;

            public LevelDownloadInfoAdapter(LevelInfo level)
            {
                this.level = level;
            }

            public string FileName => level.FileName;

            public string DisplayName => level.DisplayName;

            public string ZipPath => level.ZipPath;

            public string FilePath => level.FilePath;

            public string IdStringHash => level.GetAddOnLevelIdStringHash();
        }

        private static void AddMPLevel(string filename)
        {
            Debug.Log("MPDownloadLevel.AddMPLevel " + filename);
            if (filename.EndsWith(".mp", StringComparison.InvariantCultureIgnoreCase))
                GameManager.MultiplayerMission.AddAddOnLevel(filename, null, "OUTER_05", new[] { 2, 3, 4, 4 }, false);
            else if (filename.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                using (var zip = ZipFile.Read(filename))
                    foreach (var entry in zip)
                        if (entry.FileName.EndsWith(".mp", StringComparison.InvariantCultureIgnoreCase))
                            GameManager.MultiplayerMission.AddAddOnLevel(entry.FileName, filename, "OUTER_05", new[] { 2, 3, 4, 4 }, false);
        }

        private static PropertyInfo _LevelInfo_LevelNum_property = typeof(LevelInfo).GetProperty("LevelNum", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static void RemoveMPLevel(int index)
        {
            var levels = _Mission_Levels_Field.GetValue(GameManager.MultiplayerMission) as List<LevelInfo>;
            levels.RemoveAt(index);
            for (int i = 0, count = levels.Count; i < count; i++)
            {
                _LevelInfo_LevelNum_property.SetValue(levels[i], i, null);
            }
        }

        private static string DataLevelDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Path.Combine("Revival", "Overload")); }
        }

        private static string DLCLevelDir
        {
            get { return Path.Combine(Environment.CurrentDirectory, "DLC"); }
        }

        private static void ShowStatus(string message, bool forceId)
        {
            if (forceId)
            {
                MenuManager.AddMpStatus(message, 1f, 9);
            }
            else
            {
                MenuManager.AddMpStatus(message);
            }
        }

        private static void OnLogError(string errorMessage, bool showInStatus, float flash)
        {
            Debug.Log(errorMessage);
            if (showInStatus)
            {
                MenuManager.AddMpStatus(errorMessage, flash, 9);
            }
            LastError = errorMessage;
        }

        private static void OnDownloadFailed()
        {
            Debug.Log("MPDownloadLevel.OnDownloadFailed");
            DownloadBusy = false;
            if (Overload.NetworkManager.IsServer())
            {
                LastStatus = "SERVER: " + LastError + "! SELECTED WRAITH";
                JIPClientHandlers.SendAddMpStatus(LastStatus);
            }
        }

        private static FieldInfo _NetworkMatch_m_match_force_playlist_level_idx_Field = typeof(NetworkMatch).GetField("m_match_force_playlist_level_idx", BindingFlags.NonPublic | BindingFlags.Static);
        private static void OnDownloadCompleted(int newLevelIndex)
        {
            Debug.Log($"MPDownloadLevel.OnDownloadCompleted (newLevelIndex={newLevelIndex})");
            if (Overload.NetworkManager.IsServer())
            {
                _NetworkMatch_m_match_force_playlist_level_idx_Field.SetValue(null, newLevelIndex);
            }
            DownloadBusy = false;
        }

        public static void StartGetLevel(string levelIdHash)
        {
            if (DownloadBusy || levelIdHash == LastDownloadAttempt)
                return;
            DownloadBusy = true;
            LastDownloadAttempt = levelIdHash;
            var algorithm = new MPDownloadLevelAlgorithm(new DownloadLevelCallbacksImpl());
            GameManager.m_gm.StartCoroutine(algorithm.DoGetLevel(levelIdHash));
        }

        class DownloadLevelCallbacksImpl : DownloadLevelCallbacks
        {
            public override bool IsServer => Overload.NetworkManager.IsServer();
            public override void AddMPLevel(string path) => MPDownloadLevel.AddMPLevel(path);
            public override bool CanCreateFile(string path) => MPDownloadLevel.CanCreateFile(path);
            public override void DownloadFailed() => OnDownloadFailed();
            public override int GetAddOnLevelIndex(string levelIdHash) =>
                GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(levelIdHash);
            public override string[] LevelDirectories => new[] { DataLevelDir, DLCLevelDir };
            public override IEnumerable<ILevelDownloadInfo> GetMPLevels() => MPDownloadLevel.GetMPLevels();
            public override void LogDebug(object message) => Debug.Log(message);
            public override void LogError(string errorMessage, bool showInStatus, float flash = 1) =>
                OnLogError(errorMessage, showInStatus, flash);
            public override void RemoveMPLevel(int index) => MPDownloadLevel.RemoveMPLevel(index);
            public override void DownloadCompleted(int newLevelIndex) =>
                OnDownloadCompleted(newLevelIndex);
            public override void ShowStatusMessage(string message, bool forceId) => ShowStatus(message, forceId);
            public override bool ZipContainsLevel(string zipPath, string levelIdHash) =>
                MPDownloadLevel.ZipContainsLevel(zipPath, levelIdHash);
        }
    }

    [HarmonyPatch(typeof(Client), "OnLobbyStatusToClient")]
    class MPSetupClientLobbyStatus
    {
        static void Postfix()
        {
            if (Config.NoDownload)
                return;
            string level = NetworkMatch.m_last_lobby_status.m_match_playlist_addon_idstringhash;
            if (level != null && level != "" && GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(level) < 0)
                MPDownloadLevel.StartGetLevel(level);
        }
    }

    // wait for download when starting match before level downloaded, eg with JIP
    [HarmonyPatch(typeof(Overload.NetworkManager), "LoadScene")]
    class MPDownloadLoadScene
    {
        static IEnumerator WaitLevel(string name)
        {
            while (MPDownloadLevel.DownloadBusy || MPSpawnExtension.DownloadBusy)
                yield return null;
            if (!name.Contains(":") || Config.NoDownload || GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(name) >= 0) // test here to prevent loop
            {
                Debug.Log("Level downloaded, loading scene");
                Overload.NetworkManager.LoadScene(name);
            }
            else
            {
                Debug.Log("Match scene load failed: level still not found after download finished " + name);
                UIManager.DestroyAll(true);
                NetworkMatch.ExitMatchToMainMenu();
            }
        }

        static bool Prefix(string name)
        {
            MPSpawnExtension.CheckExtraSpawnpoints(name); // we want to trigger even for stock levels

            if (name.Contains(":") && !Config.NoDownload)
            {
                if (!MPDownloadLevel.DownloadBusy && GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(name) < 0)
                    MPDownloadLevel.StartGetLevel(name);
            }

            if (MPDownloadLevel.DownloadBusy || MPSpawnExtension.DownloadBusy)
            {
                //Debug.Log("Level still downloading when loading match scene, delay scene load " + name);
                GameManager.m_gm.StartCoroutine(WaitLevel(name));
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Server), "OnConnect")]
    class MPDownloadLevelStatusOnConnect
    {
        private static void Postfix(NetworkMessage msg)
        {
            if (MPDownloadLevel.LastStatus != null)
                NetworkServer.SendToClient(msg.conn.connectionId, MessageTypes.MsgAddMpStatus, new StringMessage(MPDownloadLevel.LastStatus));
        }
    }

    // speed up hash (=crc32) calcuation for levels in zips by reading it from the zip header
    [HarmonyPatch(typeof(LevelInfo), "GetAddOnLevelIdStringHash")]
    class MPDownloadLevelZipHash
    {
        private static bool Prefix(LevelInfo __instance, ref string ___m_cachedIdStringHash, ref string __result)
        {
            if (!__instance.IsAddOn || ___m_cachedIdStringHash != null || __instance.ZipPath == null)
                return true;
            try
            {
                using (var zipFile = ZipFile.Read(__instance.ZipPath))
                {
                    var entry = zipFile[__instance.FilePath];
                    if (entry == null)
                        return true;
                    ___m_cachedIdStringHash = string.Format("{0}:{1}", Path.GetFileName(__instance.FilePath).ToUpperInvariant(), entry.Crc.ToString("X8"));
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
            Debug.Log("GetAddOnLevelIdStringHash " + __instance.ZipPath + " " + __instance.FilePath + " " + ___m_cachedIdStringHash);
            __result = ___m_cachedIdStringHash;
            return false;
        }
    }
}
