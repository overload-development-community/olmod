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

namespace GameMod {
    static class MPDownloadLevel
    {
        private const string MapHiddenMarker = "_OCT_Hidden";
        private static Regex MapHiddenMarkerRE = new Regex(MapHiddenMarker + "[0-9]*$");

        public static bool DownloadBusy;
        private static string LastDownloadAttempt;
        public static string LastStatus;

        public static void Reset()
        {
            DownloadBusy = false;
            LastDownloadAttempt = null;
            LastStatus = null;
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

        // same algorithm as Mission.AddAddOnLevel
        private static int FindLevelIndex(List<LevelInfo> levels, string levelIdHash)
        {
            string fileNameExt = levelIdHash.Split(new[] { ':' })[0];
            string fileName = Path.GetFileNameWithoutExtension(fileNameExt);
            string displayName = fileName.Replace('_', ' ').ToUpper();
            for (int i = 0, count = levels.Count; i < count; i++)
            {
                    var level = levels[i];
                    if (level.FileName == fileName || level.DisplayName == displayName)
                            return i;
            }
            return -1;
        }

        private static FieldInfo _Mission_Levels_Field = typeof(Mission).GetField("Levels", BindingFlags.NonPublic | BindingFlags.Instance);
        private static List<LevelInfo> GetMPLevels()
        {
            return _Mission_Levels_Field.GetValue(GameManager.MultiplayerMission) as List<LevelInfo>;
        }

        private static string DifferentVersionFilename(string levelIdHash, out List<LevelInfo> levels, out int idx)
        {
            levels = GetMPLevels();
            idx = FindLevelIndex(levels, levelIdHash);
            if (idx < 0)
                return null;
            LevelInfo level = levels[idx];
            return level.ZipPath ?? level.FilePath;
        }

        private static PropertyInfo _LevelInfo_LevelNum_property = typeof(LevelInfo).GetProperty("LevelNum", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        // return false if failed
        private static bool DisableDifferentVersion(string levelIdHash, Action<string, bool> log)
        {
            string fn = DifferentVersionFilename(levelIdHash, out List<LevelInfo> levels, out int idx);
            if (fn == null)
                return true;
            try
            {
                for (var i = 0; ; i++)
                {
                    string dest = fn + MapHiddenMarker + (i == 0 ? "" : i.ToString());
                    if (!File.Exists(dest))
                    {
                        File.Move(fn, dest);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                log("CANNOT DISABLE OTHER VERSION " + Path.GetFileName(fn), true);
                return false;
            }
            levels.RemoveAt(idx);
            for (int i = 0, count = levels.Count; i < count; i++) {
                _LevelInfo_LevelNum_property.SetValue(levels[i], i, null);
            }
            log("OTHER VERSION " + Path.GetFileName(fn) + " DISABLED", false);
            return true;
        }

        private static string DataLevelDir {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Path.Combine("Revival", "Overload")); }
        }

        private static string DLCLevelDir
        {
            get { return Path.Combine(Environment.CurrentDirectory, "DLC"); }
        }

        private static bool ZipContainsLevel(string zipFilename, string levelIdHash)
        {
            string[] parts = levelIdHash.Split(new[] { ':' });
            string levelFile = parts[0];
            if (!int.TryParse(parts[1], NumberStyles.HexNumber, null, out int hash))
            {
                //Debug.Log("FindDisabledLevel: invalid levelIdHash " + levelIdHash);
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
                Debug.Log("FindDisabledLevel: reading " + zipFilename + ": " + ex);
            }
            return false;
        }

        private static string FindDisabledLevel(string levelIdHash)
        {
            foreach (var dir in new [] { DataLevelDir, DLCLevelDir })
                try
                {
                    if (Directory.Exists(dir))
                        foreach (var zipFilename in Directory.GetFiles(dir, "*.zip" + MapHiddenMarker + "*"))
                            if (MapHiddenMarkerRE.IsMatch(zipFilename) && ZipContainsLevel(zipFilename, levelIdHash))
                                return zipFilename;
                }
                catch (Exception ex)
                {
                    Debug.Log("FindDisabledLevel: reading " + dir + ": " + ex);
                }
            return null;
        }

        private static void AddMPLevel(string fn)
        {
            Debug.Log("MPDownloadLevel.AddMPLevel " + fn);
            if (fn.EndsWith(".mp", StringComparison.InvariantCultureIgnoreCase))
                GameManager.MultiplayerMission.AddAddOnLevel(fn, null, "OUTER_05", new[] { 2, 3, 4, 4 }, false);
            else if (fn.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                using (var zip = ZipFile.Read(fn))
                    foreach (var entry in zip)
                        if (entry.FileName.EndsWith(".mp", StringComparison.InvariantCultureIgnoreCase))
                            GameManager.MultiplayerMission.AddAddOnLevel(entry.FileName, fn, "OUTER_05", new[] { 2, 3, 4, 4 }, false);
        }

        private static IEnumerable<string> DoDownloadLevel(string url, string fn)
        {
            Debug.Log("Downloading " + url + " to " + fn);
            var basefn = Path.GetFileName(fn);
            var fntmp = fn + ".tmp";
            using (UnityWebRequest www = new UnityWebRequest(url, "GET"))
            {
                www.downloadHandler = new DownloadHandlerFile(fntmp);
                var request = www.SendWebRequest();
                while (!request.isDone)
                {
                    yield return "DOWNLOADING " + basefn + " ... " + Math.Round(request.progress * 100) + "%";
                }
                if (www.isNetworkError || www.isHttpError)
                {
                    File.Delete(fntmp);
                    yield return "DOWNLOADING " + basefn + " FAILED, " + (www.isNetworkError ? "NETWORK ERROR" : "SERVER ERROR");
                    yield break;
                }
            }
            yield return "DOWNLOADING " + basefn + " ... INSTALLING";
            string msg = null;
            try
            {
                File.Move(fntmp, fn);
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                msg = ex.Message;
            }
            if (msg != null)
                yield return "DOWNLOADING " + basefn + " FAILED, " + msg;
        }

        private static IEnumerable<string> MaybeDownloadLevel(string url, string levelIdHash)
        {
            var i = url.LastIndexOf('/');
            var basefn = url.Substring(i + 1);
            string fn = null;
            bool done = false;
            foreach (var dir in new [] { DataLevelDir, DLCLevelDir })
            {
                try { Directory.CreateDirectory(dir); } catch (Exception) { }
                var tryfn = Path.Combine(dir, basefn);
                if (File.Exists(tryfn))
                {
                    if (ZipContainsLevel(tryfn, levelIdHash))
                    {
                        fn = tryfn;
                        done = true;
                        break;
                    }
                    string curVersionFile = DifferentVersionFilename(levelIdHash, out List<LevelInfo> levels, out int idx);
                    if (tryfn != curVersionFile)
                    {
                        Debug.Log("Download: " + basefn + " already exists, current file: " + curVersionFile);
                        yield return "DOWNLOAD FAILED: " + basefn + " ALREADY EXISTS";
                        yield break;
                    }
                }
                if (CanCreateFile(tryfn + ".tmp")) {
                    fn = tryfn;
                    break;
                }
            }
            if (fn == null)
            {
                yield return "DOWNLOAD FAILED: NO WRITABLE DIRECTORY";
                yield break;
            }
            string disMsg = null;
            var disOk = DisableDifferentVersion(levelIdHash, (msg, isErr) => { disMsg = msg; });
            if (disMsg != null)
            {
                Debug.Log(disMsg);
                yield return disMsg;
            }
            if (!disOk)
                yield break;
            if (!done)
                foreach (var msg in DoDownloadLevel(url, fn))
                    yield return msg;
            AddMPLevel(fn);
            if (FindLevelIndex(GetMPLevels(), levelIdHash) < 0)
                yield return "DOWNLOADING " + basefn + " FAILED, LEVEL NOT IN FILE";
            else
                yield return "DOWNLOADING " + basefn + " COMPLETED";
        }

        private static bool MaybeEnableLevel(string levelIdHash, Action<string, bool> log)
        {
            string disabledFilename = FindDisabledLevel(levelIdHash);
            if (disabledFilename == null)
                return false;
            if (!DisableDifferentVersion(levelIdHash, log))
                return true; // abort
            string orgFilename = MapHiddenMarkerRE.Replace(disabledFilename, "");
            try
            {
                File.Move(disabledFilename, orgFilename);
            }
            catch (Exception ex)
            {
                MenuManager.AddMpStatus("ENABLING " + Path.GetFileName(orgFilename) + " FAILED, " + ex.Message);
                return false; // try download
            }
            AddMPLevel(orgFilename);
            if (FindLevelIndex(GetMPLevels(), levelIdHash) < 0)
                log("ENABLING " + Path.GetFileName(orgFilename) + " FAILED, LEVEL NOT IN FILE", true); // not possible? would be bug in FindDisabledLevel
            else
                log("ENABLING " + Path.GetFileName(orgFilename) + " SUCCEEDED", false);
            return true;
        }

        private static void SendStatus(string msg)
        {
            if (Overload.NetworkManager.IsServer())
            {
                LastStatus = "SERVER: " + msg;
                JIPClientHandlers.SendAddMpStatus(LastStatus);
            }
        }

        private static void DownloadFailed(string msg)
        {
            if (!Overload.NetworkManager.IsServer())
                return;
            SendStatus(msg + "! SELECTED WRAITH");
        }

        private static IEnumerable<string> LookupAndDownloadLevel(string levelIdHash)
        {
            var li = levelIdHash.IndexOf(".MP");
            MenuManager.AddMpStatus("SEARCHING " + levelIdHash.Substring(0, li), 1f, 9);
            string lastData = null;
            foreach (var x in NetworkMatch.Get("mpget", new Dictionary<string, string> { { "level", levelIdHash } }, "https://www.overloadmaps.com/api/"))
            {
                lastData = x;
                yield return null;
            }
            JObject ret = null;
            try
            {
                ret = JObject.Parse(lastData);
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
            if (ret == null || !ret.TryGetValue("url", out JToken urlVal))
            {
                string msg = ret == null ? "OVERLOADMAPS.COM LOOKUP FAILED" : "LEVEL NOT FOUND ON OVERLOADMAPS.COM";
                Debug.Log(msg);
                MenuManager.AddMpStatus(msg, 2f, 9);
                yield return msg;
                yield break;
            }

            string url = urlVal.GetString();
            var i = url.LastIndexOf('/');
            MenuManager.AddMpStatus("DOWNLOADING " + url.Substring(i + 1), 1f, 9);
            string lastMsg = null;
            foreach (var msg in MaybeDownloadLevel(url, levelIdHash))
            {
                if (msg != lastMsg)
                {
                    MenuManager.AddMpStatus(msg, 1f, 9);
                    lastMsg = msg;
                }
                yield return msg;
            }
            Debug.Log("DoGetLevel last download status: " + lastMsg);
        }

        private static FieldInfo _NetworkMatch_m_match_force_playlist_level_idx_Field = typeof(NetworkMatch).GetField("m_match_force_playlist_level_idx", BindingFlags.NonPublic | BindingFlags.Static);
        private static IEnumerator DoGetLevel(string levelIdHash)
        {
            Debug.Log("DoGetLevel " + levelIdHash);

            string errMsg = null, lastMsg = null;
            if (MaybeEnableLevel(levelIdHash, (msg, isError) => { Debug.Log(msg); MenuManager.AddMpStatus(msg); if (isError) errMsg = msg; }))
            {
                if (errMsg != null)
                {
                    DownloadFailed(errMsg);
                    DownloadBusy = false;
                    yield break;
                }
            }
            else
            {
                foreach (var msg in LookupAndDownloadLevel(levelIdHash))
                {
                    lastMsg = msg;
                    yield return null;
                }
            }
            if (Overload.NetworkManager.IsServer())
            {
                int idx = GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(levelIdHash);
                if (idx < 0) {
                    DownloadFailed(lastMsg); // if we don't have the level the last message was the error message
                } else {
                    _NetworkMatch_m_match_force_playlist_level_idx_Field.SetValue(null, idx);
                }
            }
            DownloadBusy = false;
        }

        public static void StartGetLevel(string levelIdHash)
        {
            if (DownloadBusy || levelIdHash == LastDownloadAttempt)
                return;
            DownloadBusy = true;
            LastDownloadAttempt = levelIdHash;
            GameManager.m_gm.StartCoroutine(DoGetLevel(levelIdHash));
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
            while (MPDownloadLevel.DownloadBusy)
                yield return null;
            if (GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(name) >= 0) // test here to prevent loop
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
            if (!name.Contains(":") || Config.NoDownload)
                return true;
            if (!MPDownloadLevel.DownloadBusy && GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(name) < 0)
                MPDownloadLevel.StartGetLevel(name);
            if (MPDownloadLevel.DownloadBusy) {
                Debug.Log("Level still downloading when loading match scene, delay scene load " + name);
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
