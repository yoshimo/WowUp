using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WowUp.WPF.Extensions;

namespace WowUp.WPF.Utilities
{
    public static class HashUtilities
    {
        private class HashItem
        {
            public string Path { get; set; }
            public string Hash { get; set; }
        }

        private static readonly List<string> IgnoredFolders = new List<string>
        {
            ".*"
        };

        public static async Task<string> HashDirectory(string srcPath)
        {
            var dirs = Directory.GetDirectories(srcPath, "*", SearchOption.AllDirectories)
                .Where(dir => IsValidDir(new DirectoryInfo(dir).Name))
                .ToList();

            // Dont forget to scan the orignal path's files as well
            dirs.Add(srcPath);

            try
            {
                var finalHash = await HashDirectoryPathList(dirs);
                return finalHash;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "fail");
            }

            return string.Empty;
        }

        private static async Task<string> HashDirectoryPathList(IEnumerable<string> directoryPaths)
        {
            var directoryHashes = new ConcurrentBag<HashItem>();
            await directoryPaths.ForEachAsync(2, async (directoryPath) =>
            {
                var files = Directory.GetFiles(directoryPath);
                var filesHash = await HashFilePathList(files);

                directoryHashes.Add(new HashItem
                {
                    Hash = filesHash,
                    Path = directoryPath
                });
            });

            var orderedHashes = directoryHashes.ToList()
                .OrderBy(hashItem => hashItem.Hash)
                .Select(hashItem => hashItem.Hash);

            var hash = HashList(orderedHashes);
            return hash;
        }

        private static async Task<string> HashFilePathList(IEnumerable<string> filePaths)
        {
            var fileHashes = new ConcurrentBag<HashItem>();
            await filePaths.ForEachAsync(2, async (filePath) =>
            {
                var hash = await HashFile(filePath);
                fileHashes.Add(new HashItem
                {
                    Hash = hash,
                    Path = filePath
                });
            });

            var orderedHashes = fileHashes.ToList()
                .OrderBy(hashItem => hashItem.Hash)
                .Select(hashItem => hashItem.Hash);

            var hash = HashList(orderedHashes);
            return hash;
        }

        private static string HashList(IEnumerable<string> filePath)
        {
            var hashStr = string.Join(string.Empty, filePath);
            return HashString(hashStr);
        }

        private static async Task<string> HashFile(string filePath)
        {
            var fileText = await FileUtilities.GetFileTextAsync(filePath);

            return HashString(fileText);
        }

        private static string HashString(string str)
        {
            using var sha1 = new SHA1Managed();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(str));

            return string.Concat(hash.Select(b => b.ToString("x2")));
        }

        private static bool IsValidDir(string dirName)
        {
            foreach (var pattern in IgnoredFolders)
            {
                var regex = new Regex(
                    "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline
                );

                if (regex.IsMatch(dirName))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
