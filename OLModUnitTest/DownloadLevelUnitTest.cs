using GameMod;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OLModUnitTest
{
    [TestClass]
    public class DownloadLevelUnitTest
    {
        [TestMethod]
        public void TestBasicClientDownload()
        {
            (var fileSystem, var gameList, var algorithm) = CreateDefaultAlgorithm();
            bool downloadComplete = false;
            algorithm._addMPLevel = (filename) => downloadComplete = true;
            var iterator = algorithm.DoGetLevel("TESTLEVEL.MP");
            while (iterator.MoveNext()) ;
            Assert.IsTrue(downloadComplete);
        }

        [TestMethod]
        public void TestBasicServerDownload()
        {
            (var fileSystem, var gameList, var algorithm) = CreateDefaultAlgorithm();
            bool downloadComplete = false;
            algorithm._isServer = () => true;
            algorithm._serverDownloadCompleted = (newIndex) => downloadComplete = true;
            var iterator = algorithm.DoGetLevel("TESTLEVEL.MP");
            while (iterator.MoveNext()) ;
            Assert.IsTrue(downloadComplete);
        }

        private (FakeFileSystem fileSystem, FakeGameList gameList, MPDownloadLevelAlgorithm algorithm) CreateDefaultAlgorithm()
        {
            var fileSystem = new FakeFileSystem(@"C:\ProgramData\Revival\Overload", @"E:\steamapps\common\Overload\DLC");
            var gameList = new FakeGameList(fileSystem);
            var algorithm = new MPDownloadLevelAlgorithm
            {
                _getLevelDownloadUrl = (levelId) => new List<string> {
                    $"https://overloadmaps.com/files/{Guid.NewGuid():N}/{Path.GetFileNameWithoutExtension(levelId)}.zip"
                },
                _downloadLevel = (url, filename) =>
                {
                    var file = fileSystem.CreateFile(filename);
                    file.Levels.Add(Path.GetFileNameWithoutExtension(filename) + ".MP");
                    return new List<string> { null };
                },
                _getMPLevels = () => gameList.GetMPLevels(),
                _addMPLevel = (filename) => gameList.AddMPLevel(filename),
                _removeMPLevel = (index) => gameList.RemoveMPLevel(index),
                _getAddOnLevelIndex = (levelId) => gameList.GetAddOnLevelIndex(levelId),
                _showStatusMessage = (m, forceId) => { },
                _logError = (e, show, flash) => { },
                _logDebug = (m) => { },
                _isServer = () => false,
                _downloadFailed = () => { },
                _serverDownloadCompleted = (newIndex) => { },
                _canCreateFile = fileSystem.CanCreateFile,
                _fileExists = fileSystem.FileExists,
                _moveFile = fileSystem.MoveFile,
                _deleteFile = fileSystem.DeleteFile,
                _zipContainsLevel = fileSystem.ZipContainsLevel,
                _getLevelDirectories = fileSystem.GetLevelDirectories,
                _directoryExists = fileSystem.DirectoryExists,
                _getDirectoryFiles = fileSystem.GetDirectoryFiles,
                _createDirectory = fileSystem.CreateDirectory
            };
            return (fileSystem, gameList, algorithm);
        }
    }

    internal class FakeFileSystem
    {
        private Dictionary<string, FakeDirectory> _directories;

        public FakeFileSystem(params string[] baseDirectories)
        {
            _directories = new Dictionary<string, FakeDirectory>();
            foreach (var baseDirectory in baseDirectories)
            {
                _directories[baseDirectory] = new FakeDirectory();
            }
        }

        internal bool CanCreateFile(string filename)
        {
            var directoryName = Path.GetDirectoryName(filename);
            return DirectoryExists(directoryName) && !GetDirectory(directoryName).Locked;
        }

        internal bool FileExists(string filename)
        {
            var directory = GetDirectory(Path.GetDirectoryName(filename));
            return directory != null && directory.Files.ContainsKey(Path.GetFileName(filename));
        }

        internal void MoveFile(string filePath, string newFilePath)
        {
            var fromDirectory = GetDirectory(Path.GetDirectoryName(filePath));
            var toDirectory = GetDirectory(Path.GetDirectoryName(newFilePath));
            var fromFilename = Path.GetFileName(filePath);
            var toFilename = Path.GetFileName(newFilePath);
            if (fromDirectory == null || !fromDirectory.Files.ContainsKey(fromFilename) || toDirectory == null)
            {
                throw new FileNotFoundException();
            }
            if (fromDirectory.Locked || toDirectory.Locked)
            {
                throw new UnauthorizedAccessException();
            }
            var file = fromDirectory.Files[fromFilename];
            fromDirectory.Files.Remove(fromFilename);
            toDirectory.Files.Add(toFilename, file);
        }

        internal FakeFile CreateFile(string filePath)
        {
            var directory = GetDirectory(Path.GetDirectoryName(filePath));
            var filename = Path.GetFileName(filePath);
            if (directory == null)
            {
                throw new DirectoryNotFoundException();
            }
            if (directory.Locked)
            {
                throw new UnauthorizedAccessException();
            }
            directory.Files.Add(filename, new FakeFile { Filename = filename });
            return directory.Files[filename];
        }

        internal void DeleteFile(string filePath)
        {
            var directory = GetDirectory(Path.GetDirectoryName(filePath));
            var filename = Path.GetFileName(filePath);
            if (directory == null || !directory.Files.ContainsKey(filename))
            {
                throw new FileNotFoundException();
            }
            if (directory.Locked || directory.Files[filename].Locked)
            {
                throw new UnauthorizedAccessException();
            }
            directory.Files.Remove(filename);
        }

        internal bool ZipContainsLevel(string zipFilename, string levelIdHash)
        {
            var directory = GetDirectory(Path.GetDirectoryName(zipFilename));
            var filename = Path.GetFileName(zipFilename);
            if (directory == null || !directory.Files.ContainsKey(filename))
            {
                return false;
            }
            var file = directory.Files[filename];
            return file.IsMission && file.Levels.Contains(levelIdHash);
        }

        internal string[] GetLevelDirectories()
        {
            return _directories.Keys.ToArray();
        }

        internal bool DirectoryExists(string path)
        {
            return GetDirectory(path) != null;
        }

        internal string[] GetDirectoryFiles(string path, string searchPattern)
        {
            var directory = GetDirectory(path);
            if (directory == null)
            {
                throw new DirectoryNotFoundException();
            }
            var regexSearchPattern = "^" + Regex.Escape(searchPattern).Replace("\\?", ".").Replace("\\*", ".*") + "$";
            var matches = directory.Files.Keys.Where(filename => Regex.IsMatch(filename, regexSearchPattern));
            return matches.ToArray();
        }

        internal DirectoryInfo CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        private FakeDirectory GetDirectory(string path)
        {
            foreach (var directory in _directories)
            {
                if (path.Equals(directory.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return directory.Value;
                }
                else
                {
                    var pathWithSeparator = directory.Key.TrimEnd(Path.DirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
                    if (path.StartsWith(pathWithSeparator, StringComparison.OrdinalIgnoreCase))
                    {
                        return directory.Value.GetDirectory(path.Substring(pathWithSeparator.Length));
                    }
                }
            }
            return null;
        }

        internal FakeFile GetFile(string filePath)
        {
            var directory = GetDirectory(Path.GetDirectoryName(filePath));
            var filename = Path.GetFileName(filePath);
            if (directory == null || !directory.Files.ContainsKey(filename))
            {
                throw new FileNotFoundException();
            }
            return directory.Files[filename];
        }

        private class FakeDirectory
        {
            public bool Locked { get; internal set; } = false;

            public Dictionary<string, FakeDirectory> Subdirectories { get; } = new Dictionary<string, FakeDirectory>();

            public Dictionary<string, FakeFile> Files { get; } = new Dictionary<string, FakeFile>();

            public FakeDirectory GetDirectory(string path)
            {
                foreach (var subdirectory in Subdirectories)
                {
                    if (path.Equals(subdirectory.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return subdirectory.Value;
                    }
                    else
                    {
                        var pathWithSeparator = subdirectory.Key.TrimEnd(Path.DirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
                        if (path.StartsWith(pathWithSeparator, StringComparison.OrdinalIgnoreCase))
                        {
                            return subdirectory.Value.GetDirectory(path.Substring(pathWithSeparator.Length));
                        }
                    }
                }
                return null;
            }
        }

        internal class FakeFile
        {
            public string Filename { get; set; }

            public bool Locked { get; set; } = false;

            public bool IsMission { get; }

            public List<string> Levels { get; } = new List<string>();
        }
    }

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
                var levelInfo = new LevelDownloadInfo(filenameInternal, displayName, null, filename);
                _levels.Add(levelInfo);
            }
            else if (filename.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                FakeFileSystem.FakeFile zip = _fileSystem.GetFile(filename);
                foreach (string levelFilename in zip.Levels)
                {
                    string filenameInternal = Path.GetFileNameWithoutExtension(levelFilename);
                    string displayName = filenameInternal.Replace('_', ' ').ToUpper();
                    var levelInfo = new LevelDownloadInfo(filenameInternal, displayName, filename, null);
                    _levels.Add(levelInfo);
                }
            }
        }

        internal void RemoveMPLevel(int index)
        {
            _levels.RemoveAt(index);
        }

        internal int GetAddOnLevelIndex(string levelId)
        {
            return _levels.FindIndex(levelInfo => levelInfo.FileName == Path.GetFileNameWithoutExtension(levelId));
        }

        private class LevelDownloadInfo : ILevelDownloadInfo
        {
            public LevelDownloadInfo(string fileName, string displayName, string zipPath, string filePath)
            {
                FileName = fileName;
                DisplayName = displayName;
                ZipPath = zipPath;
                FilePath = filePath;
            }

            public string FileName { get; }

            public string DisplayName { get; }

            public string ZipPath { get; }

            public string FilePath { get; }
        }
    }
}
