using Newtonsoft.Json.Linq;
using Overload;
using Path = System.IO.Path;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public class MPDownloadLevelAlgorithm
    {
        private const string MapHiddenMarker = "_OCT_Hidden";
        private static Regex MapHiddenMarkerRE = new Regex(MapHiddenMarker + "[0-9]*$");

        internal Func<List<LevelInfo>> _getMPLevels;
        internal Action<int> _removeMPLevel;
        internal Action<string> _addMPLevel;
        internal Func<string, bool> _canCreateFile;
        internal Func<string, bool> _fileExists = System.IO.File.Exists;
        internal Action<string, string> _moveFile = System.IO.File.Move;
        internal Action<string> _deleteFile = System.IO.File.Delete;
        internal Func<string, string, bool> _zipContainsLevel;
        internal Func<string[]> _getLevelDirectories;
        internal Func<string, bool> _directoryExists = System.IO.Directory.Exists;
        internal Func<string, string, string[]> _getDirectoryFiles = System.IO.Directory.GetFiles;
        internal Func<string, System.IO.DirectoryInfo> _createDirectory = System.IO.Directory.CreateDirectory;
        internal Action<string, bool> _showStatusMessage;
        internal delegate void LogErrorHandler(string errorMessage, bool showInStatus, float flash = 1f);
        internal LogErrorHandler _logError;
        internal Action _downloadFailed;
        internal delegate void ServerDownloadCompletedHandler(int newLevelIndex);
        internal ServerDownloadCompletedHandler _serverDownloadCompleted;

        public IEnumerator DoGetLevel(string levelIdHash)
        {
            Debug.Log("DoGetLevel " + levelIdHash);

            bool downloadRequired = true;
            if (FindDisabledLevel(levelIdHash) != null)
            {
                try
                {
                    if (EnableLevel(levelIdHash))
                    {
                        downloadRequired = false;
                    }
                }
                catch (Exception)
                {
                    _downloadFailed();
                    yield break;
                }
            }
            
            if (downloadRequired)
            {
                foreach (var x in LookupAndDownloadLevel(levelIdHash))
                {
                    yield return null;
                }
            }

            if (Overload.NetworkManager.IsServer())
            {
                int idx = GameManager.MultiplayerMission.FindAddOnLevelNumByIdStringHash(levelIdHash);
                if (idx < 0)
                {
                    _downloadFailed(); // if we don't have the level the last message was the error message
                }
                else
                {
                    _serverDownloadCompleted(idx);
                }
            }
        }

        /// <summary>
        /// Returns the path to a disabled .zip file that contains the specified level (whose version must match),
        /// or null if none exists.
        /// </summary>
        private string FindDisabledLevel(string levelIdHash)
        {
            foreach (var dir in _getLevelDirectories())
                try
                {
                    if (_directoryExists(dir))
                        foreach (var zipFilename in _getDirectoryFiles(dir, "*.zip" + MapHiddenMarker + "*"))
                            if (MapHiddenMarkerRE.IsMatch(zipFilename) && _zipContainsLevel(zipFilename, levelIdHash))
                                return zipFilename;
                }
                catch (Exception ex)
                {
                    Debug.Log("FindDisabledLevel: reading " + dir + ": " + ex);
                }
            return null;
        }

        /// <summary>
        /// Attempts to enable a level with the specified hash.
        /// Returns true if successful, false otherwise. Throws exceptions on fatal errors.
        /// </summary>
        private bool EnableLevel(string levelIdHash)
        {
            string disabledFilename = FindDisabledLevel(levelIdHash);
            if (disabledFilename == null)
            {
                return false;
            }

            DisableDifferentVersion(levelIdHash);

            string originalFilename = MapHiddenMarkerRE.Replace(disabledFilename, "");
            try
            {
                _moveFile(disabledFilename, originalFilename);
            }
            catch (Exception ex)
            {
                _showStatusMessage("ENABLING " + Path.GetFileName(originalFilename) + " FAILED, " + ex.Message, false);
                return false; // try download
            }
            _addMPLevel(originalFilename);

            if (FindLevelIndex(_getMPLevels(), levelIdHash) < 0)
            {
                // not possible? would be bug in FindDisabledLevel
                _logError("ENABLING " + Path.GetFileName(originalFilename) + " FAILED, LEVEL NOT IN FILE", true);
                throw new Exception();
            }

            _showStatusMessage("ENABLING " + Path.GetFileName(originalFilename) + " SUCCEEDED", false);
            return true;
        }

        /// <summary>
        /// Returns the path of a loaded multiplayer level that matches the filename from the given level ID hash,
        /// regardless of whether the version matches. Also returns the position of the matching level within the
        /// loaded multiplayer level list.
        /// Returns null if no matching level is loaded.
        /// </summary>
        private string DifferentVersionFilename(string levelIdHash, out int idx)
        {
            List<LevelInfo> levels = _getMPLevels();
            idx = FindLevelIndex(levels, levelIdHash);
            if (idx < 0)
                return null;
            LevelInfo level = levels[idx];
            return level.ZipPath ?? level.FilePath;
        }

        /// <summary>
        /// Disables any active level matching the filename of the given level ID hash.
        /// </summary>
        private void DisableDifferentVersion(string levelIdHash)
        {
            string fn = DifferentVersionFilename(levelIdHash, out int idx);
            if (fn == null)
                return;
            try
            {
                for (var i = 0; ; i++)
                {
                    string dest = fn + MapHiddenMarker + (i == 0 ? "" : i.ToString());
                    if (!_fileExists(dest))
                    {
                        _moveFile(fn, dest);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                _logError("CANNOT DISABLE OTHER VERSION " + Path.GetFileName(fn), true);
                throw ex;
            }
            _removeMPLevel(idx);
            _showStatusMessage("OTHER VERSION " + Path.GetFileName(fn) + " DISABLED", false);
        }

        /// <summary>
        /// Finds the index of a given level in the provided level list. Checks level file name and
        /// display name; does not guarantee matching contents.
        /// Same algorithm as Mission.AddAddOnLevel
        /// </summary>
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

        private IEnumerable LookupAndDownloadLevel(string levelIdHash)
        {
            string url = null;
            foreach (var x in GetLevelDownloadUrl(levelIdHash))
            {
                url = x;
                yield return null;
            }
            if (url == null)
            {
                yield break;
            }
            string displayName = url.Substring(url.LastIndexOf('/') + 1);

            string fileName = GetLocalDownloadPath(url, levelIdHash);
            if (fileName == null)
            {
                // Failed to find a usable local path
                yield break;
            }
            else if (_zipContainsLevel(fileName, levelIdHash))
            {
                // The correct version of the level is already there. Don't need to download, but do need to enable it
            }
            else
            {
                try
                {
                    DisableDifferentVersion(levelIdHash);
                }
                catch (Exception)
                {
                    yield break;
                }

                _showStatusMessage("DOWNLOADING " + displayName, true);
                foreach (var x in DownloadLevel(url, fileName))
                {
                    yield return null;
                }
            }

            // Now enable the level and ensure it's available
            _addMPLevel(fileName);
            if (FindLevelIndex(_getMPLevels(), levelIdHash) < 0)
            {
                _logError("DOWNLOADING " + displayName + " FAILED, LEVEL NOT IN FILE", true);
            }
            else
            {
                _showStatusMessage("DOWNLOADING " + displayName + " COMPLETED", true);
            }
        }

        /// <summary>
        /// Queries overloadmaps.com for a level download URL matching the specified hash.
        /// The final return value will contain the URL (which may be null if the query failed).
        /// </summary>
        private IEnumerable<string> GetLevelDownloadUrl(string levelIdHash)
        {
            var li = levelIdHash.IndexOf(".MP");
            _showStatusMessage("SEARCHING " + levelIdHash.Substring(0, li), true);

            string lastData = null;
            foreach (var x in NetworkMatch.Get("mpget", new Dictionary<string, string> { { "level", levelIdHash } }, "https://www.overloadmaps.com/api/"))
            {
                lastData = x;
                yield return null;
            }

            JObject result = null;
            try
            {
                result = JObject.Parse(lastData);
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
            if (result == null)
            {
                _logError("OVERLOADMAPS.COM LOOKUP FAILED", true, 2f);
                yield break;
            }

            if (!result.TryGetValue("url", out JToken urlVal))
            {
                _logError("LEVEL NOT FOUND ON OVERLOADMAPS.COM", true, 2f);
                yield break;
            }
            yield return urlVal.GetString();
        }

        private string GetLocalDownloadPath(string url, string levelIdHash)
        {
            var i = url.LastIndexOf('/');
            var basefn = url.Substring(i + 1);
            foreach (var dir in _getLevelDirectories())
            {
                try { _createDirectory(dir); } catch (Exception) { }
                var tryfn = Path.Combine(dir, basefn);
                if (_fileExists(tryfn))
                {
                    if (_zipContainsLevel(tryfn, levelIdHash))
                    {
                        return tryfn;
                    }
                    string curVersionFile = DifferentVersionFilename(levelIdHash, out int idx);
                    if (tryfn != curVersionFile)
                    {
                        Debug.Log("Download: " + basefn + " already exists, current file: " + curVersionFile);
                        _logError("DOWNLOAD FAILED: " + basefn + " ALREADY EXISTS", true);
                        return null;
                    }
                }
                if (_canCreateFile(tryfn + ".tmp"))
                {
                    return tryfn;
                }
            }

            _logError("DOWNLOAD FAILED: NO WRITABLE DIRECTORY", true);
            return null;
        }

        private IEnumerable DownloadLevel(string url, string fn)
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
                    _showStatusMessage("DOWNLOADING " + basefn + " ... " + Math.Round(request.progress * 100) + "%", true);
                    yield return null;
                }
                if (www.isNetworkError || www.isHttpError)
                {
                    _deleteFile(fntmp);
                    _showStatusMessage("DOWNLOADING " + basefn + " FAILED, " + (www.isNetworkError ? "NETWORK ERROR" : "SERVER ERROR"), true);
                    yield break;
                }
            }
            _showStatusMessage("DOWNLOADING " + basefn + " ... INSTALLING", true);
            string msg = null;
            try
            {
                _moveFile(fntmp, fn);
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                msg = ex.Message;
            }
            if (msg != null)
            {
                _showStatusMessage("DOWNLOADING " + basefn + " FAILED, " + msg, true);
            }
        }
    }
}