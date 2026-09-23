using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace KWHMonitoring.Services
{
    public class AesEncryptionService
    {
        private readonly byte[] _key;

        public AesEncryptionService(IConfiguration configuration)
        {
            var encryptionKey = configuration["Encryption:Key"]?.Trim();
            if (string.IsNullOrWhiteSpace(encryptionKey) ||
                encryptionKey == "YOUR_ENCRYPTION_KEY")
            {
                throw new InvalidOperationException("Encryption key is not configured. Set the environment variable 'Encryption__Key' or the configuration key 'Encryption:Key' to a strong secret value.");
            }

            using (var sha256 = SHA256.Create())
            {
                _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKey));
            }
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.GenerateIV();
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // Prepend IV to ciphertext for decryption
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        var bytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(bytes, 0, bytes.Length);
                        cs.FlushFinalBlock();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                var fullBytes = Convert.FromBase64String(cipherText);
                if (fullBytes.Length < 16 + 1)
                    return null;

                // Try new format: IV (16 bytes) + ciphertext
                if (fullBytes.Length > 32)
                {
                    var iv = new byte[16];
                    Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
                    var cipherBytes = new byte[fullBytes.Length - 16];
                    Buffer.BlockCopy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);

                    using (var aes = Aes.Create())
                    using (var decryptor = aes.CreateDecryptor(_key, iv))
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                    {
                        try
                        {
                            return sr.ReadToEnd();
                        }
                        catch
                        {
                            // New format failed, try legacy format below
                        }
                    }
                }

                // Legacy format: deterministic IV from key
                var legacyIv = new byte[16];
                Buffer.BlockCopy(_key, 0, legacyIv, 0, 16);
                using (var aes = Aes.Create())
                using (var decryptor = aes.CreateDecryptor(_key, legacyIv))
                using (var ms = new MemoryStream(fullBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
