// <copyright file="Logger.cs" company="Google Inc.">
// Copyright (C) 2017 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>

namespace Google {
    using System;
    using System.IO;
    using System.Collections.Generic;
    using UnityEditor;

    /// <summary>
    /// Utility methods to assist with file management in Unity.
    /// </summary>
    internal class FileUtils {
        /// <summary>
        /// Extension of Unity metadata files.
        /// </summary>
        internal const string META_EXTENSION = ".meta";

        /// <summary>
        /// The name of Assets folder
        /// </summary>
        public readonly static string ASSETS_FOLDER = "Assets";

        /// <summary>
        /// The name of Packages folder
        /// </summary>
        public readonly static string PACKAGES_FOLDER = "Packages";

        /// <summary>
        /// Returns the project directory (e.g contains the Assets folder).
        /// </summary>
        /// <returns>Full path to the project directory.</returns>
        public static string ProjectDirectory {
            get {
                return Directory.GetParent(
                    Path.GetFullPath(
                        UnityEngine.Application.dataPath)).FullName;
            }
        }

        /// <summary>
        /// Get the project's temporary directory.
        /// </summary>
        /// <returns>Full path to the project's temporary directory.</returns>
        public static string ProjectTemporaryDirectory {
            get { return Path.Combine(ProjectDirectory, "Temp"); }
        }

        /// <summary>
        /// Format a file error.
        /// </summary>
        /// <param name="summary">Description of what went wrong.</param>
        /// <param name="errors">List of failures.</param>
        public static string FormatError(string summary, List<string> errors) {
            if (errors.Count > 0) {
                return String.Format("{0}\n{1}", summary, String.Format("\n", errors.ToArray()));
            }
            return "";
        }

        /// <summary>
        /// Delete a file or directory if it exists.
        /// </summary>
        /// <param name="path">Path to the file or directory to delete if it exists.</param>
        /// <param name="includeMetaFiles">Whether to delete Unity's associated .meta file(s).
        /// </param>
        /// <returns>List of files, with exception messages for files / directories that could
        /// not be deleted.</returns>
        public static List<string> DeleteExistingFileOrDirectory(string path,
                                                                 bool includeMetaFiles = true)
        {
            var failedToDelete = new List<string>();
            if (includeMetaFiles && !path.EndsWith(META_EXTENSION)) {
                failedToDelete.AddRange(DeleteExistingFileOrDirectory(path + META_EXTENSION));
            }
            try {
                if (Directory.Exists(path)) {
                    if (!UnityEditor.FileUtil.DeleteFileOrDirectory(path)) {
                        var di = new DirectoryInfo(path);
                        di.Attributes &= ~FileAttributes.ReadOnly;
                        foreach (string file in Directory.GetFileSystemEntries(path)) {
                            failedToDelete.AddRange(DeleteExistingFileOrDirectory(
                                                        file, includeMetaFiles: includeMetaFiles));
                        }
                        Directory.Delete(path);
                    }
                }
                else if (File.Exists(path)) {
                    if (!UnityEditor.FileUtil.DeleteFileOrDirectory(path)) {
                        File.SetAttributes(path, File.GetAttributes(path) &
                                           ~FileAttributes.ReadOnly);
                        File.Delete(path);
                    }
                }
            } catch (Exception ex) {
                failedToDelete.Add(String.Format("{0} ({1})", path, ex));
            }
            return failedToDelete;
        }

        /// <summary>
        /// Copy the contents of a directory to another directory.
        /// </summary>
        /// <param name="sourceDir">Path to copy the contents from.</param>
        /// <param name="targetDir">Path to copy to.</param>
        public static void CopyDirectory(string sourceDir, string targetDir) {
            Func<string, string> sourceToTargetPath = (path) => {
                return Path.Combine(targetDir, path.Substring(sourceDir.Length + 1));
            };
            foreach (string sourcePath in
                     Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(sourceToTargetPath(sourcePath));
            }
            foreach (string sourcePath in
                     Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)) {
                if (!sourcePath.EndsWith(META_EXTENSION)) {
                    File.Copy(sourcePath, sourceToTargetPath(sourcePath));
                }
            }
        }

        /// <summary>
        /// Create a temporary directory.
        /// </summary>
        /// <param name="useSystemTempPath">If true, uses the system wide temporary directory
        /// otherwise uses the Unity project's temporary directory.</param>
        /// <param name="retry">Number of times to attempt to create a temporary directory.</param>
        /// <returns>If temporary directory creation fails, return null.</returns>
        public static string CreateTemporaryDirectory(bool useSystemTempPath = false,
                                                      int retry = 100) {
            string tempPath = useSystemTempPath ? Path.GetTempPath() : ProjectTemporaryDirectory;
            while (retry-- > 0) {
                string temporaryDirectory = Path.Combine(tempPath, Path.GetRandomFileName());
                if (File.Exists(temporaryDirectory)) continue;
                Directory.CreateDirectory(temporaryDirectory);
                return temporaryDirectory;
            }
            return null;
        }

        /// <summary>
        /// Move a directory preserving permissions on Windows.
        /// </summary>
        /// <param name="sourceDir">Source directory.</param>
        /// <param name="targetDir">Target directory.  If this directory exists, it is
        /// deleted.</param>
        public static void MoveDirectory(string sourceDir, string targetDir) {
            sourceDir = Path.GetFullPath(sourceDir);
            targetDir = Path.GetFullPath(targetDir);
            DeleteExistingFileOrDirectory(targetDir);
            if (UnityEngine.RuntimePlatform.WindowsEditor == UnityEngine.Application.platform) {
                // On Windows permissions may not be propagated correctly to the target path when
                // using Directory.Move().
                // Since old versions of Unity use old versions of Mono that don't implement
                // Directory.GetAccessControl(), we copy the files to create file entries with the
                // correct permissions in the target folder.
                Directory.CreateDirectory(targetDir);
                CopyDirectory(sourceDir, targetDir);
                DeleteExistingFileOrDirectory(sourceDir);
            } else {
                Directory.Move(sourceDir, targetDir);
            }
        }

        /// <summary>
        /// Move a file.
        /// </summary>
        /// <param name="sourceFile">Source file.</param>
        /// <param name="targetFile">Target file.  If this directory exists, it is
        /// deleted.</param>
        public static void MoveFile(string sourceFile, string targetFile) {
            sourceFile = Path.GetFullPath(sourceFile);
            targetFile = Path.GetFullPath(targetFile);
            DeleteExistingFileOrDirectory(targetFile);
            if (UnityEngine.RuntimePlatform.WindowsEditor == UnityEngine.Application.platform) {
                // On Windows permissions may not be propagated correctly to the target path when
                // using File.Move().
                // Since old versions of Unity use old versions of Mono that don't implement
                // File.GetAccessControl(), we copy the files to create file entries with the
                // correct permissions in the target folder.
                File.Copy(sourceFile, targetFile);
                DeleteExistingFileOrDirectory(sourceFile);
            } else {
                File.Move(sourceFile, targetFile);
            }
        }

        /// <summary>
        /// Normalize a path using system directory separators.
        /// </summary>
        /// <param name="path">Path to normalize.</param>
        /// <returns>Path with consistent directory separators for the platform.</returns>
        public static string NormalizePathSeparators(string path) {
            return path != null ? path.Replace(Path.AltDirectorySeparatorChar,
                                               Path.DirectorySeparatorChar) : null;
        }

        /// <summary>
        /// Convert path to use POSIX directory separators.
        /// </summary>
        /// <param name="path">Path to convert.</param>
        /// <returns>Path with POSIX directory separators.</returns>
        public static string PosixPathSeparators(string path) {
            return path != null ? path.Replace("\\", "/") : null;
        }

        /// <summary>
        /// Find a path under the specified directory.
        /// </summary>
        /// <param name="directory">Directory to search.</param>
        /// <param name="pathToFind">Path to find.<param>
        /// <returns>The shortest path to the specified directory if found, null
        /// ptherwise.</returns>
        public static string FindPathUnderDirectory(string directory, string pathToFind) {
            directory = NormalizePathSeparators(directory);
            if (directory.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                directory = directory.Substring(0, directory.Length - 1);
            }
            var foundPaths = new List<string>();
            foreach (string path in
                     Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)) {
                var relativePath = NormalizePathSeparators(path.Substring(directory.Length + 1));
                if (relativePath.EndsWith(Path.DirectorySeparatorChar + pathToFind) ||
                    relativePath.Contains(Path.DirectorySeparatorChar + pathToFind +
                                          Path.DirectorySeparatorChar)) {
                    foundPaths.Add(relativePath);
                }
            }
            if (foundPaths.Count == 0) return null;
            foundPaths.Sort((string lhs, string rhs) => {
                    return lhs.Length - rhs.Length;
                });
            return foundPaths[0];
        }

        /// <summary>
        /// Split a path into directory components.
        /// </summary>
        /// <param name="path">Path to split.</param>
        /// <returns>Path components.</returns>
        public static string[] SplitPathIntoComponents(string path) {
            return NormalizePathSeparators(path).Split(new [] { Path.DirectorySeparatorChar });
        }

        /// <summary>
        /// Perform a case insensitive search for a path relative to the current directory.
        /// </summary>
        /// <remarks>
        /// Directory.Exists() is case insensitive, so this method finds a directory using a case
        /// insensitive search returning the name of the first matching directory found.
        /// </remarks>
        /// <param name="pathToFind">Path to find relative to the current directory.</param>
        /// <returns>First case insensitive match for the specified path.</returns>
        public static string FindDirectoryByCaseInsensitivePath(string pathToFind) {
            var searchDirectory = ".";
            // Components of the path.
            var components = SplitPathIntoComponents(pathToFind);
            for (int componentIndex = 0;
                 componentIndex < components.Length && searchDirectory != null;
                 componentIndex++) {
                var enumerateDirectory = searchDirectory;
                var expectedComponent = components[componentIndex];
                var expectedComponentLower = components[componentIndex].ToLowerInvariant();
                searchDirectory = null;
                var matchingPaths = new List<KeyValuePair<int, string>>();
                foreach (var currentDirectory in
                         Directory.GetDirectories(enumerateDirectory)) {
                    // Get the current component of the path we're traversing.
                    var currentComponent = Path.GetFileName(currentDirectory);
                    if (currentComponent.ToLowerInvariant() == expectedComponentLower) {
                        // Add the path to a list and remove "./" from the first component.
                        matchingPaths.Add(new KeyValuePair<int, string>(
                            Math.Abs(String.CompareOrdinal(expectedComponent, currentComponent)),
                            (componentIndex == 0) ? Path.GetFileName(currentDirectory) :
                                currentDirectory));
                        break;
                    }
                }
                if (matchingPaths.Count == 0) break;
                // Sort list in order of ordinal string comparison result.
                matchingPaths.Sort(
                    (KeyValuePair<int, string> lhs, KeyValuePair<int, string> rhs) => {
                        return lhs.Key - rhs.Key;
                    });
                searchDirectory = matchingPaths[0].Value;
            }
            return NormalizePathSeparators(searchDirectory);
        }

        /// <summary>
        /// Checks out a file should Version Control be active and valid.
        /// </summary>
        /// <param name="path">Path to the file that needs checking out.</param>
        /// <param name="logger">Logger, used to log any error messages.</param>
        /// <returns>False should the checkout fail, otherwise true.</returns>
        public static bool CheckoutFile(string path, Logger logger) {
            try {
                if (UnityEditor.VersionControl.Provider.enabled &&
                    UnityEditor.VersionControl.Provider.isActive &&
                    (!UnityEditor.VersionControl.Provider.requiresNetwork ||
                     UnityEditor.VersionControl.Provider.onlineState ==
                     UnityEditor.VersionControl.OnlineState.Online)) {
                    // Unity 2019.1+ broke backwards compatibility of Checkout() by adding an
                    // optional argument to the method so we dynamically invoke the method to add
                    // the optional
                    // argument for the Unity 2019.1+ overload at runtime.
                    var task = (UnityEditor.VersionControl.Task)VersionHandler.InvokeStaticMethod(
                        typeof(UnityEditor.VersionControl.Provider),
                        "Checkout",
                        new object[] { path, UnityEditor.VersionControl.CheckoutMode.Exact },
                        namedArgs: null);
                    task.Wait();
                    if (!task.success) {
                        var errorMessage = new List<string>();
                        errorMessage.Add(String.Format("Failed to checkout {0}.", path));
                        if (task.messages != null) {
                            foreach (var message in task.messages) {
                                if (message != null) errorMessage.Add(message.message);
                            }
                        }
                        logger.Log(String.Join("\n", errorMessage.ToArray()),
                                   level: LogLevel.Warning);
                        return false;
                    }
                }
                return true;
            } catch (Exception ex) {
                logger.Log(String.Format("Failed to checkout {0} ({1}.", path, ex),
                           level: LogLevel.Warning);
                return false;
            }
        }

        /// <summary>
        /// Checks if the given path is under the given directory.
        /// Ex. "A/B/C" is under "A", while "A/B/C" is NOT under "E/F"
        /// </summary>
        /// <param name="path">Path to the file/directory that needs checking.</param>
        /// <param name="directory">Directory to check whether the path is under.</param>
        /// <returns>True if the path is under the directory.</returns>
        public static bool IsUnderDirectory(string path, string directory) {
            if (!directory.EndsWith(Path.DirectorySeparatorChar.ToString())) {
                directory = directory + Path.DirectorySeparatorChar;
            }
            return NormalizePathSeparators(path.ToLower()).StartsWith(directory.ToLower());
        }

        /// <summary>
        /// Get the package directory from the path.
        /// The package directory should look like "Packages/package-id", where package id is
        /// the Unity Package Manager package name.
        /// Ex. "Packages/com.google.unity-jar-resolver" if actualPath is false, or
        /// "Library/PackageCache/com.google.unity-jar-resolver@1.2.120/" if true.
        /// Logical path is
        /// Note that there is no way to get physical path if the package is installed from disk.
        /// </summary>
        /// <param name="path">Path to the file/directory.</param>
        /// <param name="actualPath">If false, return logical path only recognizable by
        /// AssetDatabase. Otherwise, return physical path of the package.</param>
        /// <returns>Package directory part of the given path. Empty string if the path is not valid
        /// .</returns>
        public static string GetPackageDirectory(string path, bool actualPath) {
            if (!IsUnderDirectory(path, PACKAGES_FOLDER)) {
                return "";
            }

            string[] components = SplitPathIntoComponents(path);
            if (components.Length < 2) {
                return "";
            }

            string packageDir = components[0] + Path.DirectorySeparatorChar + components[1];
            if (actualPath) {
                // This only works if the package is NOT installed from disk. That is, it should
                // work if the package is installed from a local tarball or from a registry server.
                string absolutePath = Path.GetFullPath(packageDir);
                packageDir = absolutePath.Substring(ProjectDirectory.Length + 1);
            }

            return packageDir;
        }

        /// <summary>
        /// Result of RemoveAssets().
        /// </summary>
        public class RemoveAssetsResult {

            /// <summary>
            /// Assets that were removed.
            /// </summary>
            public List<string> Removed { get; set; }

            /// <summary>
            /// Assets that failed to be removed.
            /// </summary>
            public List<string> RemoveFailed { get; set; }

            /// <summary>
            /// Assets that were missing.
            /// </summary>
            public List<string> Missing { get; set; }

            /// <summary>
            /// Whether the operation was successful.
            /// </summary>
            public bool Success { get { return RemoveFailed.Count == 0 && Missing.Count == 0; } }

            /// <summary>
            /// Construct an empty result.
            /// </summary>
            public RemoveAssetsResult() {
                Removed = new List<string>();
                RemoveFailed = new List<string>();
                Missing = new List<string>();
            }
        }

        /// <summary>
        /// Remove the given set of files and their folders.
        /// </summary>
        /// <param name = "filenames">Files to be removed/</param>
        /// <param name = "logger">Logger to log results.</param>
        /// <return>True if all files are removed.  False if failed to remove any file or
        /// if any file is missing.</return>
        public static RemoveAssetsResult RemoveAssets(IEnumerable<string> filenames,
                                                      Logger logger = null) {
            var result = new RemoveAssetsResult();

            HashSet<string> folderToRemove = new HashSet<string>();
            foreach (var filename in filenames) {
                if (File.Exists(filename)) {
                    if (AssetDatabase.DeleteAsset(filename)) {
                        result.Removed.Add(filename);
                    } else {
                        result.RemoveFailed.Add(filename);
                    }

                    // Add folder and parent folders to be removed later.
                    var folder = Path.GetDirectoryName(filename);
                    while (!String.IsNullOrEmpty(folder) &&
                           !Path.IsPathRooted(folder) &&
                           String.Compare(folder, ASSETS_FOLDER) != 0 &&
                           String.Compare(folder, PACKAGES_FOLDER) != 0) {
                        folderToRemove.Add(folder);
                        folder = Path.GetDirectoryName(folder);
                    }
                } else {
                    result.Missing.Add(filename);
                }
            }

            // Attempt to remove folders from bottom to top.
            // This is an unreliable way to remove folder as long as Directory class is used.
            // Directory.GetFiles() and Directory.GetDirectories() may return non-empty list
            // even everything under the folder is removed.  This may due to Unity overriding
            // Files and Directory class.  While Asset.DeleteAsset() can delete the folder,
            // regardless it is empty or not, the current approach still have some chance to
            // leave some empty folders.
            // TODO: Change the implementation to remove folder directly as long as every files
            //       and folders are planned to be removed.
            List<string> sortedFolders = new List<string>(folderToRemove);
            // Sort folders in descending order so that sub-folder is removed first.
            sortedFolders.Sort((lhs, rhs) => {
                return String.Compare(rhs, lhs);
            });
            List<string> folderRemoveFailed = new List<string>();
            foreach (var folder in sortedFolders) {
                if (Directory.GetFiles(folder).Length == 0 &&
                    Directory.GetDirectories(folder).Length == 0) {
                    if (!AssetDatabase.DeleteAsset(folder)) {
                        folderRemoveFailed.Add(folder);
                        result.RemoveFailed.Add(folder);
                    }
                }
            }

            if(logger != null) {
                logger.Log(
                    String.Format("Removed:\n{0}\nFailed to Remove:\n{1}\nMissing:\n{2}\n" +
                        "Failed to Remove Folders:\n{3}\n",
                        String.Join("\n", result.Removed.ToArray()),
                        String.Join("\n", result.RemoveFailed.ToArray()),
                        String.Join("\n", result.Missing.ToArray()),
                        String.Join("\n", folderRemoveFailed.ToArray())), level : LogLevel.Verbose);
            }
            return result;
        }
   }
}
