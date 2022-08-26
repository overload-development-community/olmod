using System;
using System.Collections.Generic;
using System.IO;
using GameMod;

namespace OLModUnitTest.HelperClasses {
    internal class FakeGameList
    {
        private readonly List<LevelDownloadInfo> _levels = new List<LevelDownloadInfo>();
        private readonly FakeFileSystem _fileSystem;

        public FakeGameList(FakeFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        internal IEnumerable<ILevelDownloadInfo> GetMPLevels()
        {
            return _levels;
        }

        internal void AddMPLevel(string filename)
        {
            if (filename.EndsWith(".mp", StringComparison.InvariantCultureIgnoreCase))
            {
                string filenameInternal = Path.GetFileNameWithoutExtension(filename);
                string displayName = filenameInternal.Replace('_', ' ').ToUpper();
                var levelInfo = new LevelDownloadInfo(filenameInternal, displayName, null, filename, null);
                _levels.Add(levelInfo);
            }
            else if (filename.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                FakeFileSystem.FakeFile zip;
                try
                {
                    zip = _fileSystem.GetFile(filename);
                }
                catch (Exception)
                {
                    return;
                }
                foreach (var level in zip.Levels)
                {
                    string filenameInternal = Path.GetFileNameWithoutExtension(level.Key);
                    string displayName = filenameInternal.Replace('_', ' ').ToUpper();
                    string levelIdHash = string.Format("{0}:{1:X8}", level.Key.ToUpper(), level.Value);
                    var levelInfo = new LevelDownloadInfo(filenameInternal, displayName, filename, null, levelIdHash);
                    _levels.Add(levelInfo);
                }
            }
        }

        internal void RemoveMPLevel(int index)
        {
            _levels.RemoveAt(index);
        }

        internal int GetAddOnLevelIndex(string levelIdHash)
        {
            return _levels.FindIndex(levelInfo => levelInfo.IdStringHash == levelIdHash);
        }

        private class LevelDownloadInfo : ILevelDownloadInfo
        {
            public LevelDownloadInfo(string fileName, string displayName, string zipPath, string filePath,
                string idStringHash)
            {
                FileName = fileName;
                DisplayName = displayName;
                ZipPath = zipPath;
                FilePath = filePath;
                IdStringHash = idStringHash;
            }

            public string FileName { get; }

            public string DisplayName { get; }

            public string ZipPath { get; }

            public string FilePath { get; }

            public string IdStringHash { get; }
        }
    }
}
