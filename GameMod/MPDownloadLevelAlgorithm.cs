using Newtonsoft.Json.Linq;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using Path = System.IO.Path;

namespace GameMod
{
    public abstract class DownloadLevelCallbacks
    {
        public abstract IEnumerable<ILevelDownloadInfo> GetMPLevels();
        public abstract void AddMPLevel(string path);
        public abstract void RemoveMPLevel(int index);
        public abstract int GetAddOnLevelIndex(string levelIdHash);
        public abstract bool CanCreateFile(string path);
        public virtual bool FileExists(string path) => System.IO.File.Exists(path);
        public virtual void MoveFile(string sourceFileName, string destFileName) =>
            System.IO.File.Move(sourceFileName, destFileName);
        public virtual void DeleteFile(string path) => System.IO.File.Delete(path);
        public abstract bool ZipContainsLevel(string zipPath, string levelIdHash);
        public abstract string[] LevelDirectories { get; }
        public virtual bool DirectoryExists(string path) => System.IO.Directory.Exists(path);
        public virtual string[] GetDirectoryFiles(string path, string searchPattern) =>
            System.IO.Directory.GetFiles(path, searchPattern);
        public virtual System.IO.DirectoryInfo CreateDirectory(string path) =>
            System.IO.Directory.CreateDirectory(path);
        public abstract void ShowStatusMessage(string message, bool forceId);
        public abstract void LogError(string errorMessage, bool showInStatus, float flash = 1f);
        public abstract void LogDebug(object message);
        public abstract bool IsServer { get; }
        public abstract void DownloadFailed();
        public abstract void ServerDownloadCompleted(int newLevelIndex);

        /// <summary>
        /// Queries overloadmaps.com for a level download URL matching the specified hash.
        /// The final return value will contain the URL (which may be null if the query failed).
        /// </summary>
        public virtual IEnumerable<string> GetLevelDownloadUrl(string levelIdHash)
        {
            var li = levelIdHash.IndexOf(".MP");
            ShowStatusMessage("SEARCHING " + levelIdHash.Substring(0, li), true);

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
                LogDebug(ex);
            }
            if (result == null)
            {
                LogError("OVERLOADMAPS.COM LOOKUP FAILED", true, 2f);
                yield break;
            }

            if (!result.TryGetValue("url", out JToken urlVal))
            {
                LogError("LEVEL NOT FOUND ON OVERLOADMAPS.COM", true, 2f);
                yield break;
            }
            yield return urlVal.GetString();
        }

        public virtual IEnumerable DownloadLevel(string url, string fn)
        {
            LogDebug("Downloading " + url + " to " + fn);
            var basefn = Path.GetFileName(fn);
            var fntmp = fn + ".tmp";
            using (UnityWebRequest www = new UnityWebRequest(url, "GET"))
            {
                www.downloadHandler = new DownloadHandlerFile(fntmp);
                var request = www.SendWebRequest();
                while (!request.isDone)
                {
                    ShowStatusMessage("DOWNLOADING " + basefn + " ... " + Math.Round(request.progress * 100) + "%", true);
                    yield return null;
                }
                if (www.isNetworkError || www.isHttpError)
                {
                    DeleteFile(fntmp);
                    ShowStatusMessage("DOWNLOADING " + basefn + " FAILED, " + (www.isNetworkError ? "NETWORK ERROR" : "SERVER ERROR"), true);
                    yield break;
                }
            }
            ShowStatusMessage("DOWNLOADING " + basefn + " ... INSTALLING", true);
            string msg = null;
            try
            {
                MoveFile(fntmp, fn);
            }
            catch (Exception ex)
            {
                LogDebug(ex);
                msg = ex.Message;
            }
            if (msg != null)
            {
                ShowStatusMessage("DOWNLOADING " + basefn + " FAILED, " + msg, true);
            }
        }
    }

    public interface ILevelDownloadInfo
    {
        string FileName { get; }
        string DisplayName { get; }
        string ZipPath { get; }
        string FilePath { get; }
        string IdStringHash { get; }
    }

    public class MPDownloadLevelAlgorithm
    {
        private const string MapHiddenMarker = "_OCT_Hidden";
        private static Regex MapHiddenMarkerRE = new Regex(MapHiddenMarker + "[0-9]*$");
        private readonly DownloadLevelCallbacks _callbacks;

        public MPDownloadLevelAlgorithm(DownloadLevelCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        public IEnumerator DoGetLevel(string levelIdHash)
        {
            _callbacks.LogDebug("DoGetLevel " + levelIdHash);

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
                    _callbacks.DownloadFailed();
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

            int idx = _callbacks.GetAddOnLevelIndex(levelIdHash);
            if (idx < 0)
            {
                _callbacks.DownloadFailed(); // if we don't have the level the last message was the error message
            }
            else if (_callbacks.IsServer)
            {
                _callbacks.ServerDownloadCompleted(idx);
            }
        }

        /// <summary>
        /// Returns the path to a disabled .zip file that contains the specified level (whose version must match),
        /// or null if none exists.
        /// </summary>
        private string FindDisabledLevel(string levelIdHash)
        {
            foreach (var dir in _callbacks.LevelDirectories)
                try
                {
                    if (_callbacks.DirectoryExists(dir))
                        foreach (var zipFilename in _callbacks.GetDirectoryFiles(dir, "*.zip" + MapHiddenMarker + "*"))
                            if (MapHiddenMarkerRE.IsMatch(zipFilename) && _callbacks.ZipContainsLevel(zipFilename, levelIdHash))
                                return zipFilename;
                }
                catch (Exception ex)
                {
                    _callbacks.LogDebug("FindDisabledLevel: reading " + dir + ": " + ex);
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
                _callbacks.MoveFile(disabledFilename, originalFilename);
            }
            catch (Exception ex)
            {
                _callbacks.ShowStatusMessage("ENABLING " + Path.GetFileName(originalFilename) + " FAILED, " + ex.Message, false);
                return false; // try download
            }
            _callbacks.AddMPLevel(originalFilename);

            if (FindLevelIndex(_callbacks.GetMPLevels(), levelIdHash) < 0)
            {
                // not possible? would be bug in FindDisabledLevel
                _callbacks.LogError("ENABLING " + Path.GetFileName(originalFilename) + " FAILED, LEVEL NOT IN FILE", true);
                throw new Exception();
            }

            _callbacks.ShowStatusMessage("ENABLING " + Path.GetFileName(originalFilename) + " SUCCEEDED", false);
            return true;
        }

        /// <summary>
        /// Returns the path of a loaded multiplayer level that matches the filename from the given level ID hash,
        /// regardless of whether the version matches.
        /// Returns null if no matching level is loaded.
        /// </summary>
        private string DifferentVersionFilename(string levelIdHash)
        {
            var levels = _callbacks.GetMPLevels();
            int idx = FindLevelIndex(levels, levelIdHash);
            if (idx < 0)
                return null;
            var level = levels.ElementAt(idx);
            return level.ZipPath ?? level.FilePath;
        }

        /// <summary>
        /// Returns a list of level ID hashes found in the specified mission file.
        /// </summary>
        private IEnumerable<string> GetActiveLevelsFromFilename(string zipPath)
        {
            var levels = _callbacks.GetMPLevels();
            var matchingLevels = new List<string>();
            foreach (var level in levels)
            {
                if (_callbacks.ZipContainsLevel(zipPath, level.IdStringHash))
                {
                    matchingLevels.Add(level.IdStringHash);
                }
            }
            return matchingLevels;
        }

        /// <summary>
        /// Disables any active levels loaded from the specified zip file.
        /// </summary>
        private void DisableLevelsFromZip(string zipPath)
        {
            if (!_callbacks.FileExists(zipPath))
            {
                return;
            }
            var levelsToRemove = GetActiveLevelsFromFilename(zipPath);
            try
            {
                for (var i = 0; ; i++)
                {
                    string dest = zipPath + MapHiddenMarker + (i == 0 ? "" : i.ToString());
                    if (!_callbacks.FileExists(dest))
                    {
                        _callbacks.MoveFile(zipPath, dest);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _callbacks.LogDebug(ex);
                throw ex;
            }
            foreach (var level in levelsToRemove)
            {
                var levels = _callbacks.GetMPLevels();
                int idx = FindLevelIndex(levels, level);
                if (idx >= 0)
                {
                    _callbacks.RemoveMPLevel(idx);
                }
            }
        }

        /// <summary>
        /// Disables any active level matching the filename of the given level ID hash.
        /// Also disables other active levels from the same file, if any.
        /// </summary>
        private void DisableDifferentVersion(string levelIdHash)
        {
            string fn = DifferentVersionFilename(levelIdHash);
            if (fn == null)
                return;
            try
            {
                DisableLevelsFromZip(fn);
            }
            catch (Exception)
            {
                _callbacks.LogError("CANNOT DISABLE OTHER VERSION " + Path.GetFileName(fn), true);
                throw;
            }
            _callbacks.ShowStatusMessage("OTHER VERSION " + Path.GetFileName(fn) + " DISABLED", false);
        }

        /// <summary>
        /// Finds the index of a given level in the provided level list. Checks level file name and
        /// display name; does not guarantee matching contents.
        /// Same algorithm as Mission.AddAddOnLevel
        /// </summary>
        private static int FindLevelIndex(IEnumerable<ILevelDownloadInfo> levels, string levelIdHash)
        {
            string fileNameExt = levelIdHash.Split(new[] { ':' })[0];
            string fileName = Path.GetFileNameWithoutExtension(fileNameExt);
            string displayName = fileName.Replace('_', ' ').ToUpper();
            return levels.IndexOf(level => level.FileName == fileName || level.DisplayName == displayName);
        }

        private IEnumerable LookupAndDownloadLevel(string levelIdHash)
        {
            string url = null;
            foreach (var x in _callbacks.GetLevelDownloadUrl(levelIdHash))
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
            else if (_callbacks.ZipContainsLevel(fileName, levelIdHash))
            {
                // The correct version of the level is already there. Don't need to download, but do need to add it
            }
            else
            {
                try
                {
                    DisableDifferentVersion(levelIdHash);
                    DisableLevelsFromZip(fileName);
                }
                catch (Exception)
                {
                    yield break;
                }

                _callbacks.ShowStatusMessage("DOWNLOADING " + displayName, true);
                foreach (var x in _callbacks.DownloadLevel(url, fileName))
                {
                    yield return null;
                }
            }

            // Now add the level and ensure it's available
            _callbacks.AddMPLevel(fileName);
            if (FindLevelIndex(_callbacks.GetMPLevels(), levelIdHash) < 0)
            {
                _callbacks.LogError("DOWNLOADING " + displayName + " FAILED, LEVEL NOT IN FILE", true);
            }
            else
            {
                _callbacks.ShowStatusMessage("DOWNLOADING " + displayName + " COMPLETED", true);
            }
        }

        private string GetLocalDownloadPath(string url, string levelIdHash)
        {
            var i = url.LastIndexOf('/');
            var basefn = url.Substring(i + 1);
            foreach (var dir in _callbacks.LevelDirectories)
            {
                try { _callbacks.CreateDirectory(dir); } catch (Exception) { }
                var tryfn = Path.Combine(dir, basefn);
                if (_callbacks.CanCreateFile(tryfn + ".tmp"))
                {
                    return tryfn;
                }
            }

            _callbacks.LogError("DOWNLOAD FAILED: NO WRITABLE DIRECTORY", true);
            return null;
        }
    }
}