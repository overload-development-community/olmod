using GameMod;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
            (var fileSystem, var gameList, var callbacks, var algorithm) = CreateDefaultAlgorithm();
            var iterator = algorithm.DoGetLevel("TESTLEVEL.MP");
            while (iterator.MoveNext()) ;
            callbacks.Verify(c => c.AddMPLevel(@"C:\ProgramData\Revival\Overload\testlevel.zip"));
        }

        [TestMethod]
        public void TestBasicServerDownload()
        {
            (var fileSystem, var gameList, var callbacks, var algorithm) = CreateDefaultAlgorithm();
            callbacks.SetupGet(c => c.IsServer).Returns(true);
            callbacks.Setup(c => c.ServerDownloadCompleted(It.IsAny<int>()));
            var iterator = algorithm.DoGetLevel("TESTLEVEL.MP");
            while (iterator.MoveNext()) ;
            callbacks.Verify(c => c.ServerDownloadCompleted(It.IsAny<int>()));
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestEnableLevel()
        {
            (var fileSystem, var gameList, var callbacks, var algorithm) = CreateDefaultAlgorithm();

            // Create a simulated level already on disk but not in the game list
            var file = fileSystem.CreateFile(fileSystem.GetLevelDirectories()[0] + "\\testlevel.zip_OCT_Hidden");
            file.IsMission = true;
            file.Levels.Add("testlevel.mp");

            callbacks.Setup(c => c.DownloadLevel(It.IsAny<string>(), It.IsAny<string>()));
            callbacks.Setup(c => c.AddMPLevel(It.IsAny<string>()));

            var iterator = algorithm.DoGetLevel("TESTLEVEL.MP");
            while (iterator.MoveNext()) ;

            // The algorithm should not download the level
            callbacks.Verify(c => c.DownloadLevel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            // The algorithm should still add the level
            callbacks.Verify(c => c.AddMPLevel(It.IsAny<string>()));
            // We expect the file to have been renamed too
            Assert.AreEqual("testlevel.zip", file.Filename);
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestEnableLevel_UpperCase()
        {
            Assert.Fail();
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestEnableLevel_DifferentVersionDisabled()
        {
            Assert.Fail();
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestEnableLevel_MultipleDifferentVersions()
        {
            Assert.Fail();
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestEnableLevel_DifferentVersionDisableFails_Abort()
        {
            Assert.Fail();
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestEnableLevelFails_Download()
        {
            Assert.Fail();
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestFindDisabledLevel_LevelNotPresent_Skip()
        {
            Assert.Fail();
        }

        [TestCategory("EnableLevel")]
        [TestMethod]
        public void TestFindDisabledLevel_LevelNotDisabled_Skip()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestDownloadLevel_UrlNotFound_Abort()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_CreateDirectory()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_CannotWriteFirstDirectory_UseSecondDirectory()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_FileExistsLevelPresent_AddLevel()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_FileExistsDifferentVersionPresent_DisableExistingLevel()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_FileExistsLevelNotPresent_DisableExistingLevel()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_FileExistsWriteProtected_Abort()
        {
            Assert.Fail();
        }

        [TestCategory("GetLocalDownloadPath")]
        [TestMethod]
        public void TestGetLocalDownloadPath_NoWriteablePath_Abort()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestDownloadFails_Abort()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestDownloadBadMission_Abort()
        {
            Assert.Fail();
        }

        [TestMethod]
        public void TestServerDownloadFails_ReportError()
        {
            Assert.Fail();
        }

        private (FakeFileSystem fileSystem, FakeGameList gameList, Mock<DownloadLevelCallbacks> callbacks,
            MPDownloadLevelAlgorithm algorithm) CreateDefaultAlgorithm()
        {
            var fileSystem = new FakeFileSystem(@"C:\ProgramData\Revival\Overload", @"E:\steamapps\common\Overload\DLC");
            var gameList = new FakeGameList(fileSystem);
            var callbacks = new Mock<DownloadLevelCallbacks> { CallBase = true };
            var algorithm = new MPDownloadLevelAlgorithm(callbacks.Object);

            callbacks.Setup(c => c.GetLevelDownloadUrl(It.IsAny<string>()))
                .Returns<string>(levelId => new List<string>
                {
                    $"https://overloadmaps.com/files/{Guid.NewGuid():N}/{Path.GetFileNameWithoutExtension(levelId).ToLower()}.zip"
                });
            callbacks.Setup(c => c.DownloadLevel(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string url, string filename) =>
                {
                    var file = fileSystem.CreateFile(filename);
                    file.Levels.Add(Path.GetFileNameWithoutExtension(filename) + ".MP");
                    return new List<string>();
                });
            callbacks.Setup(c => c.GetMPLevels()).Returns(gameList.GetMPLevels());
            callbacks.Setup(c => c.AddMPLevel(It.IsAny<string>()))
                .Callback<string>(filename => gameList.AddMPLevel(filename));
            callbacks.Setup(c => c.RemoveMPLevel(It.IsAny<int>()))
                .Callback<int>(index => gameList.RemoveMPLevel(index));
            callbacks.Setup(c => c.GetAddOnLevelIndex(It.IsAny<string>()))
                .Returns<string>(levelId => gameList.GetAddOnLevelIndex(levelId));
            callbacks.SetupGet(c => c.IsServer).Returns(false);
            callbacks.Setup(c => c.CanCreateFile(It.IsAny<string>()))
                .Returns((string filename) => fileSystem.CanCreateFile(filename));
            callbacks.Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns((string filename) => fileSystem.FileExists(filename));
            callbacks.Setup(c => c.MoveFile(It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string filePath, string newFilePath) => fileSystem.MoveFile(filePath, newFilePath));
            callbacks.Setup(c => c.DeleteFile(It.IsAny<string>()))
                .Callback<string>(filePath => fileSystem.DeleteFile(filePath));
            callbacks.Setup(c => c.ZipContainsLevel(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string zipFilename, string levelIdHash) =>
                fileSystem.ZipContainsLevel(zipFilename, levelIdHash));
            callbacks.SetupGet(c => c.LevelDirectories).Returns(fileSystem.GetLevelDirectories());
            callbacks.Setup(c => c.DirectoryExists(It.IsAny<string>()))
                .Returns((string path) => fileSystem.DirectoryExists(path));
            callbacks.Setup(c => c.GetDirectoryFiles(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string path, string searchPattern) => fileSystem.GetDirectoryFiles(path, searchPattern));
            callbacks.Setup(c => c.CreateDirectory(It.IsAny<string>()))
                .Callback<string>(path => fileSystem.CreateDirectory(path));

            return (fileSystem, gameList, callbacks, algorithm);
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
            file.Filename = toFilename;
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
            return file.IsMission && file.Levels.Contains(levelIdHash, StringComparer.OrdinalIgnoreCase);
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
            var pathWithSeparator = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var regexSearchPattern = "^" + Regex.Escape(searchPattern).Replace("\\?", ".").Replace("\\*", ".*") + "$";
            var matches = from filename in directory.Files.Keys
                          where Regex.IsMatch(filename, regexSearchPattern)
                          select pathWithSeparator + filename;
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

            public bool IsMission { get; set; }

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
            return _levels.FindIndex(levelInfo => StringComparer.OrdinalIgnoreCase.Compare(levelInfo.FileName,
                Path.GetFileNameWithoutExtension(levelId)) == 0);
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
