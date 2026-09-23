using System.Security.Cryptography;
using System.Text;

namespace SalesApp.Services
{
    public static class ScraperCredentialEncryption
    {
        private const int KeyHexLength = 64; // 32 bytes = 64 hex chars
        private const int IvLength = 12;     // 12 bytes standard for AES-GCM
        private const int TagLength = 16;    // 16 bytes standard for AES-GCM

        public static (string cipherTextB64, string ivB64, string authTagB64) Encrypt(string plainText, string hexKey)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                throw new ArgumentException("Texto para criptografia não pode ser vazio.", nameof(plainText));
            }

            if (string.IsNullOrWhiteSpace(hexKey) || hexKey.Trim().Length != KeyHexLength)
            {
                throw new ArgumentException($"Chave de criptografia deve possuir exatamente {KeyHexLength} caracteres hexadecimais (32 bytes).", nameof(hexKey));
            }

            var cleanHex = hexKey.Trim();
            var keyBytes = Convert.FromHexString(cleanHex);

            var iv = new byte[IvLength];
            RandomNumberGenerator.Fill(iv);

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagLength];

            using var aesGcm = new AesGcm(keyBytes, TagLength);
            aesGcm.Encrypt(iv, plainBytes, cipherBytes, tag);

            return (
                Convert.ToBase64String(cipherBytes),
                Convert.ToBase64String(iv),
                Convert.ToBase64String(tag)
            );
        }
    }
}
