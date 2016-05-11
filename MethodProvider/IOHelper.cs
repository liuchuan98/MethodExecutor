using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace MethodProvider
{
    public static class IOHelper
    {
        private static object _obj1ForStaticMethod = new object();
        private static object _obj2ForStaticMethod = new object();

        /// <summary>
        /// Ensure directory exist for certain file path, this is,
        /// If the directory does not exist, create it.
        /// </summary>
        /// <param name="dirPaths">Directory paths to process.</param>
        public static void EnsureFoldersExist(params string[] dirPaths)
        {
            foreach (var dirPath in dirPaths)
            {
                EnsureFolderExist(dirPath);
            }
        }

        /// Ensure directory exist for certain file path, this is,
        /// If the directory does not exist, create it.
        /// </summary>
        /// <param name="filePath">File paths to process.</param>
        public static void EnsureFoldersExistForFiles(params string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                string dir = Path.GetDirectoryName(filePath);
                EnsureFolderExist(dir);
            }
        }

        /// <summary>
        /// Ensure directory exist for certain file path, this is,
        /// If the directory does not exist, create it.
        /// </summary>
        /// <param name="dirPath">Directory path to process.</param>
        private static void EnsureFolderExist(string dirPath)
        {
            if (!string.IsNullOrEmpty(dirPath) &&
                !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
        }

        /// <summary>
        /// Compare two txt files, and output compare result file.
        /// </summary>
        /// <param name="aFilePath">The first file path.</param>
        /// <param name="bFilePath">The second file path.</param>
        /// <param name="diffLogPath">Output file path.</param>
        public static void Diff2FileInfo(string aFilePath, string bFilePath, string diffLogPath)
        {
            string aFileName = Path.GetFileName(aFilePath);
            string bFileName = Path.GetFileName(bFilePath);
            List<string> aLines = File.ReadAllLines(aFilePath).ToList();
            aLines.RemoveAll(s => string.IsNullOrEmpty(s));
            List<string> bLines = File.ReadAllLines(bFilePath).ToList();
            bLines.RemoveAll(s => string.IsNullOrEmpty(s));

            List<string> inAnotinB = aLines.Where(it => !bLines.Contains(it)).ToList();
            List<string> inBnotinA = bLines.Where(it => !aLines.Contains(it)).ToList();
            List<string> common = aLines.Where(it => bLines.Contains(it)).ToList();

            List<string> allLogs = new List<string>();
            allLogs.Add(string.Format("in file {0} not in file {1}, count {2}", aFileName, bFileName, inAnotinB.Count()));
            allLogs.AddRange(inAnotinB);
            allLogs.Add("");
            allLogs.Add("");

            allLogs.Add(string.Format("in file {0} not in file {1}, count {2}", bFileName, aFileName, inBnotinA.Count()));
            allLogs.AddRange(inBnotinA);
            allLogs.Add("");
            allLogs.Add("");

            allLogs.Add(string.Format("common, count {0}:", common.Count()));
            allLogs.AddRange(common);
            allLogs.Add("");
            allLogs.Add("");

            File.WriteAllLines(diffLogPath, allLogs);
        }

        /// <summary>
        /// Get the relative path based on the absolute path.
        /// </summary>
        /// <param name="targetPath">The target path that need to get the relative path, 一般来做是文件绝对路径.</param>
        /// <param name="referencePath">The reference path, 一般来说是文件夹路径.</param>
        /// <returns></returns>
        private static string GetRelativePath(string targetPath, string referencePath)
        {
            if (targetPath.EndsWith("\\") || targetPath.EndsWith("/"))
                targetPath = targetPath.TrimEnd('\\', '/');

            if (referencePath.EndsWith("\\") || referencePath.EndsWith("/"))
                referencePath = referencePath.TrimEnd('\\', '/');

            var referencePathItems = referencePath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var targetPathItems = targetPath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var result = targetPathItems.Skip(referencePathItems.Length).Take(targetPathItems.Length - referencePathItems.Length);
            return string.Join(Path.DirectorySeparatorChar.ToString(), result);
        }

        private static IEnumerable<string> EnumerateFilesWithExtension(string srcFolder, params string[] extList)
        {
            List<string> result = new List<string>();
            if (extList.Count() == 0)
            {
                return Directory.EnumerateFiles(srcFolder, "*.*", SearchOption.AllDirectories).ToList();
            }
            else
            {
                foreach (string ext in extList)
                {
                    var list = Directory.EnumerateFiles(srcFolder, "*" + ext, SearchOption.AllDirectories).ToList();
                    result.AddRange(list);
                }
            }
            return result;
        }

        /// <summary>
        /// Compare two file as binary mode.
        /// </summary>
        /// <param name="leftFile">Path of left file to compare.</param>
        /// <param name="rightFile">Path of right file to compare.</param>
        /// <returns>True if equal, otherwise false.</returns>
        public static bool CompareBinary(string leftFile, string rightFile)
        {
            using (FileStream leftStream =
                new FileStream(leftFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream rightStream =
                new FileStream(rightFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (leftStream.Length != rightStream.Length)
                {
                    return false;
                }

                int bufferLen = 4 * 1024; // 4k
                byte[] bufLeft = new byte[bufferLen];
                byte[] bufRight = new byte[bufferLen];

                int lenLeft, lenRight;
                for (int offset = 0; offset < leftStream.Length; offset += lenLeft)
                {
                    lenLeft = leftStream.Read(bufLeft, 0, bufferLen);
                    lenRight = rightStream.Read(bufRight, 0, bufferLen);

                    if (lenLeft != lenRight)
                    {
                        return false;
                    }

                    for (int i = 0; i < lenLeft; ++i)
                    {
                        if (bufLeft[i] != bufRight[i])
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Copy directory.
        /// </summary>
        /// <param name="source">Source Directory.</param>
        /// <param name="dstPath">Destination Directiory.</param>
        public static void CopyDirectory(String source, String dstPath)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            if (!Directory.Exists(dstPath))
            {
                Directory.CreateDirectory(dstPath);
            }
            DirectoryInfo di = new DirectoryInfo(source);

            foreach (FileSystemInfo fsi in di.GetFileSystemInfos())
            {
                String destName = Path.Combine(dstPath, fsi.Name);
                if (fsi is FileInfo)
                {
                    if (!File.Exists(destName))
                    {
                        File.Copy(fsi.FullName, destName, true);
                    }
                }
                else
                {
                    if (!Directory.Exists(destName))
                    {
                        Directory.CreateDirectory(destName);
                    }
                    CopyDirectory(fsi.FullName, destName);
                }
            }
        }

        /// <summary>
        /// Generate file list for one folder.
        /// </summary>
        /// <param name="srcFolder">Source folder.</param>
        /// <param name="dstFileList">Dst file list file.</param>
        public static void GenFileList(string srcFolder, string dstFileList)
        {
            List<string> fileList = GetFileListFromFolder(srcFolder);
            File.WriteAllLines(dstFileList, fileList);
        }

        private static List<string> GetFileListFromFolder(string srcFolder)
        {
            if (!Directory.Exists(srcFolder))
            {
                return new List<string>();
            }

            List<string> fileList = new List<string>();
            List<string> allFiles = Directory.GetFiles(srcFolder, "*.*", SearchOption.AllDirectories).ToList();
            foreach (var filePath in allFiles)
            {
                string relativePath = IOHelper.GetRelativePath(filePath, srcFolder);
                fileList.Add(relativePath);
            }

            return fileList;
        }
    }
}
