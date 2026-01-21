using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MazSvc.Models;

namespace MazSvc
{
    /// <summary>
    /// Manages encrypted configuration file for protected executables
    /// </summary>
    public class ConfigManager
    {
        private const string CONFIG_FILE_NAME = "config.enc";
        private const string ENCRYPTION_KEY_SALT = "MAZ_SVC_CONFIG_SALT_2024";

        /// <summary>
        /// Loads the protected executable list from encrypted config file
        /// </summary>
        public List<ProtectedExecutable> LoadConfig()
        {
            List<ProtectedExecutable> executables = new List<ProtectedExecutable>();

            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(appDirectory, CONFIG_FILE_NAME);

                if (!File.Exists(configPath))
                {
                    return executables;
                }

                // Read encrypted data
                byte[] encryptedData = File.ReadAllBytes(configPath);
                
                // Decrypt data
                string decryptedJson = Decrypt(encryptedData);
                
                if (string.IsNullOrEmpty(decryptedJson))
                {
                    return executables;
                }

                // Parse JSON manually (no external dependencies for .NET 4.0)
                executables = ParseJsonToExecutables(decryptedJson);
            }
            catch
            {
                // Return empty list on error
            }

            return executables;
        }

        /// <summary>
        /// Saves the protected executable list to encrypted config file
        /// </summary>
        public void SaveConfig(List<ProtectedExecutable> executables, string configDirectory)
        {
            try
            {
                string configPath = Path.Combine(configDirectory, CONFIG_FILE_NAME);

                // Convert to JSON
                string json = ExecutablesToJson(executables);

                // Encrypt data
                byte[] encryptedData = Encrypt(json);

                // Write to file
                File.WriteAllBytes(configPath, encryptedData);
            }
            catch
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Gets the encryption key derived from hardware fingerprint
        /// </summary>
        private byte[] GetEncryptionKey()
        {
            try
            {
                // Use machine-specific key derived from hardware fingerprint
                string hardwareFingerprint = HardwareFingerprint.GetHardwareInfo().GetCombinedString();
                string keySource = hardwareFingerprint + ENCRYPTION_KEY_SALT;

                using (SHA256 sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(Encoding.UTF8.GetBytes(keySource));
                }
            }
            catch
            {
                // Fallback to salt-only key
                using (SHA256 sha256 = SHA256.Create())
                {
                    return sha256.ComputeHash(Encoding.UTF8.GetBytes(ENCRYPTION_KEY_SALT));
                }
            }
        }

        /// <summary>
        /// Encrypts a string using AES-256
        /// </summary>
        private byte[] Encrypt(string plainText)
        {
            byte[] key = GetEncryptionKey();

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor();

                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                // Prepend IV to encrypted data
                byte[] result = new byte[aes.IV.Length + encryptedBytes.Length];
                Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
                Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

                return result;
            }
        }

        /// <summary>
        /// Decrypts AES-256 encrypted data
        /// </summary>
        private string Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length < 17) // IV (16) + at least 1 byte
            {
                return null;
            }

            byte[] key = GetEncryptionKey();

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Extract IV from beginning of data
                byte[] iv = new byte[16];
                Array.Copy(encryptedData, 0, iv, 0, 16);
                aes.IV = iv;

                // Extract encrypted content
                byte[] encryptedContent = new byte[encryptedData.Length - 16];
                Array.Copy(encryptedData, 16, encryptedContent, 0, encryptedContent.Length);

                ICryptoTransform decryptor = aes.CreateDecryptor();

                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedContent, 0, encryptedContent.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
        }

        /// <summary>
        /// Converts executable list to simple JSON format
        /// </summary>
        private string ExecutablesToJson(List<ProtectedExecutable> executables)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");

            for (int i = 0; i < executables.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }

                sb.Append("{");
                sb.Append("\"Name\":\"");
                sb.Append(EscapeJsonString(executables[i].Name));
                sb.Append("\",\"FullPath\":\"");
                sb.Append(EscapeJsonString(executables[i].FullPath));
                sb.Append("\"}");
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Parses simple JSON to executable list
        /// </summary>
        private List<ProtectedExecutable> ParseJsonToExecutables(string json)
        {
            List<ProtectedExecutable> executables = new List<ProtectedExecutable>();

            if (string.IsNullOrEmpty(json))
            {
                return executables;
            }

            // Simple JSON parser for our specific format
            int index = 0;
            while (index < json.Length)
            {
                // Find next object start
                int objStart = json.IndexOf('{', index);
                if (objStart < 0)
                {
                    break;
                }

                int objEnd = json.IndexOf('}', objStart);
                if (objEnd < 0)
                {
                    break;
                }

                string objContent = json.Substring(objStart + 1, objEnd - objStart - 1);

                // Parse Name
                string name = ExtractJsonValue(objContent, "Name");
                string fullPath = ExtractJsonValue(objContent, "FullPath");

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(fullPath))
                {
                    ProtectedExecutable exe = new ProtectedExecutable();
                    exe.Name = UnescapeJsonString(name);
                    exe.FullPath = UnescapeJsonString(fullPath);
                    executables.Add(exe);
                }

                index = objEnd + 1;
            }

            return executables;
        }

        private string ExtractJsonValue(string json, string key)
        {
            string searchKey = "\"" + key + "\":\"";
            int keyIndex = json.IndexOf(searchKey);
            if (keyIndex < 0)
            {
                return null;
            }

            int valueStart = keyIndex + searchKey.Length;
            int valueEnd = valueStart;

            // Find the end of the value (unescaped quote)
            while (valueEnd < json.Length)
            {
                if (json[valueEnd] == '"' && (valueEnd == valueStart || json[valueEnd - 1] != '\\'))
                {
                    break;
                }
                valueEnd++;
            }

            if (valueEnd > valueStart)
            {
                return json.Substring(valueStart, valueEnd - valueStart);
            }

            return null;
        }

        private string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private string UnescapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < value.Length)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    char next = value[i + 1];
                    switch (next)
                    {
                        case '\\':
                            sb.Append('\\');
                            i += 2;
                            break;
                        case '"':
                            sb.Append('"');
                            i += 2;
                            break;
                        case 'n':
                            sb.Append('\n');
                            i += 2;
                            break;
                        case 'r':
                            sb.Append('\r');
                            i += 2;
                            break;
                        case 't':
                            sb.Append('\t');
                            i += 2;
                            break;
                        default:
                            sb.Append(value[i]);
                            i++;
                            break;
                    }
                }
                else
                {
                    sb.Append(value[i]);
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
