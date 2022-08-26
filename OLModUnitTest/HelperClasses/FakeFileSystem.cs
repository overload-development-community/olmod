using GameMod;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OLModUnitTest.HelperClasses
{
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
            var file = fromDirectory.Files[fromFilename];
            if (fromDirectory.Locked || toDirectory.Locked || file.Locked)
            {
                throw new UnauthorizedAccessException();
            }
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

            var requestedLevelFilename = levelIdHash.Split(':')[0];
            var requestedLevelCrc32 = uint.Parse(levelIdHash.Split(':')[1], NumberStyles.HexNumber);
            var foundLevelFilename = file.Levels.Keys.FirstOrDefault(
                key => key.Equals(requestedLevelFilename, StringComparison.OrdinalIgnoreCase));
            return foundLevelFilename != default(string) &&
                file.Levels[foundLevelFilename] == requestedLevelCrc32;
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
                          where Regex.IsMatch(filename, regexSearchPattern, RegexOptions.IgnoreCase)
                          select pathWithSeparator + filename;
            return matches.ToArray();
        }

        internal DirectoryInfo CreateDirectory(string path)
        {
            var currentDirectories = _directories;
            var remainingPath = path.TrimStart(Path.DirectorySeparatorChar);

            // Search until we reach a directory that already exists (no remaining path to find/create)
            while (remainingPath.Split(Path.DirectorySeparatorChar)[0].Length > 0)
            {
                var subdirectoryName = remainingPath.Split(Path.DirectorySeparatorChar)[0];
                foreach (var directory in currentDirectories)
                {
                    if (subdirectoryName.Equals(directory.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found a match, continue looking down this directory
                        if (directory.Value.Locked)
                        {
                            throw new UnauthorizedAccessException();
                        }
                        currentDirectories = directory.Value.Subdirectories;
                        remainingPath = remainingPath.Substring(subdirectoryName.Length)
                            .TrimStart(Path.DirectorySeparatorChar);
                        break;
                    }
                }

                // No match found, create a directory here
                currentDirectories[subdirectoryName] = new FakeDirectory();
                currentDirectories = currentDirectories[subdirectoryName].Subdirectories;
                remainingPath = remainingPath.Substring(subdirectoryName.Length)
                    .TrimStart(Path.DirectorySeparatorChar);
            }

            // Currently the caller doesn't use DirectoryInfo, and it isn't safe to create anyway
            return null;
        }

        internal FakeDirectory GetDirectory(string path)
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

        internal class FakeDirectory
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

            public Dictionary<string, uint> Levels { get; } = new Dictionary<string, uint>();
        }
    }
}
