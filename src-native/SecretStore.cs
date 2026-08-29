using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace QoderCN.GatewayManager
{
    public static class SecretStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("QoderCN-GatewayManager/3.2.0/API-Key");
        private static readonly byte[] LegacyEntropy310 = Encoding.UTF8.GetBytes("QoderCN-GatewayManager/3.1.0/API-Key");
        private static readonly byte[] LegacyEntropy301 = Encoding.UTF8.GetBytes("QoderCN-GatewayManager/3.0.1/API-Key");

        private static string GetStoreDirectory()
        {
            string overrideDirectory = Environment.GetEnvironmentVariable("QODER_CN_SECRET_STORE_DIR");
            if (!string.IsNullOrWhiteSpace(overrideDirectory)) return Path.GetFullPath(overrideDirectory);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, @"QoderCNOpenAICompatiblePatcher\secrets");
        }

        private static string GetSecretPath(string identifier)
        {
            string normalized = (identifier ?? "").Trim().ToUpperInvariant();
            byte[] digest;
            using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            StringBuilder name = new StringBuilder();
            foreach (byte b in digest) name.Append(b.ToString("x2"));
            return Path.Combine(GetStoreDirectory(), name + ".bin");
        }

        public static void Save(string identifier, string secret)
        {
            if (string.IsNullOrWhiteSpace(identifier)) throw new ArgumentException("Identifier is required.", "identifier");
            if (string.IsNullOrWhiteSpace(secret))
            {
                Delete(identifier);
                return;
            }

            string directory = GetStoreDirectory();
            Directory.CreateDirectory(directory);
            byte[] plaintext = Encoding.UTF8.GetBytes(secret.Trim());
            byte[] protectedBytes = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            string target = GetSecretPath(identifier);
            string temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temp, protectedBytes);
                if (File.Exists(target)) File.Replace(temp, target, null, true);
                else File.Move(temp, target);
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        public static string Load(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return "";
            string path = GetSecretPath(identifier);
            if (!File.Exists(path)) return "";
            byte[] protectedBytes = File.ReadAllBytes(path);
            byte[] plaintext = null;
            bool upgraded = false;
            try
            {
                try
                {
                    plaintext = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                }
                catch (CryptographicException)
                {
                    try
                    {
                        plaintext = ProtectedData.Unprotect(protectedBytes, LegacyEntropy310, DataProtectionScope.CurrentUser);
                        upgraded = true;
                    }
                    catch (CryptographicException)
                    {
                        plaintext = ProtectedData.Unprotect(protectedBytes, LegacyEntropy301, DataProtectionScope.CurrentUser);
                        upgraded = true;
                    }
                }

                string secret = Encoding.UTF8.GetString(plaintext);
                if (upgraded && !string.IsNullOrWhiteSpace(secret))
                {
                    Save(identifier, secret);
                }
                return secret;
            }
            catch
            {
                return "";
            }
            finally
            {
                if (plaintext != null) Array.Clear(plaintext, 0, plaintext.Length);
            }
        }

        public static void Delete(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return;
            string path = GetSecretPath(identifier);
            if (File.Exists(path)) File.Delete(path);
        }

        public static bool HasSecret(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return false;
            string key = Load(identifier);
            return !string.IsNullOrWhiteSpace(key);
        }

        public static void SaveProviderKey(string providerId, string apiKey)
        {
            Save("provider:" + providerId, apiKey);
        }

        public static string LoadProviderKey(string providerId)
        {
            return Load("provider:" + providerId);
        }

        public static void DeleteProviderKey(string providerId)
        {
            Delete("provider:" + providerId);
        }

        public static bool HasProviderKey(string providerId)
        {
            return HasSecret("provider:" + providerId);
        }
    }
}
