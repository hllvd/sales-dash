using System.Security.Cryptography;
using System.Text;

namespace SalesApp.Notifications.Utils
{
    /// <summary>
    /// Pure, deterministic ULID (Universally Unique Lexicographically Sortable Identifier) generator.
    /// 128-bit identifier: 48-bit timestamp (milliseconds) + 80-bit cryptographic randomness.
    /// Encoded in Crockford's Base32 (26 characters).
    /// Safe for sorting and lexicographical indexing in DynamoDB Sort Keys.
    /// </summary>
    public static class UlidGenerator
    {
        private const string CrockfordBase32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string NewUlid()
        {
            var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var randomBytes = new byte[10];
            RandomNumberGenerator.Fill(randomBytes);

            return Generate(timestampMs, randomBytes);
        }

        public static string Generate(long timestampMs, byte[] randomBytes)
        {
            if (randomBytes == null || randomBytes.Length < 10)
            {
                throw new ArgumentException("10 random bytes are required for ULID entropy.");
            }

            Span<byte> data = stackalloc byte[16];
            // 48-bit timestamp in big-endian
            data[0] = (byte)(timestampMs >> 40);
            data[1] = (byte)(timestampMs >> 32);
            data[2] = (byte)(timestampMs >> 24);
            data[3] = (byte)(timestampMs >> 16);
            data[4] = (byte)(timestampMs >> 8);
            data[5] = (byte)(timestampMs);

            // 80-bit random entropy
            for (int i = 0; i < 10; i++)
            {
                data[6 + i] = randomBytes[i];
            }

            Span<char> chars = stackalloc char[26];

            // 128 bits into 26 5-bit characters (26 * 5 = 130 bits, 2 leading zeros)
            chars[0] = CrockfordBase32[(data[0] & 224) >> 5];
            chars[1] = CrockfordBase32[data[0] & 31];
            chars[2] = CrockfordBase32[(data[1] & 248) >> 3];
            chars[3] = CrockfordBase32[((data[1] & 7) << 2) | ((data[2] & 192) >> 6)];
            chars[4] = CrockfordBase32[(data[2] & 62) >> 1];
            chars[5] = CrockfordBase32[((data[2] & 1) << 4) | ((data[3] & 240) >> 4)];
            chars[6] = CrockfordBase32[((data[3] & 15) << 1) | ((data[4] & 128) >> 7)];
            chars[7] = CrockfordBase32[(data[4] & 124) >> 2];
            chars[8] = CrockfordBase32[((data[4] & 3) << 3) | ((data[5] & 224) >> 5)];
            chars[9] = CrockfordBase32[data[5] & 31];

            chars[10] = CrockfordBase32[(data[6] & 248) >> 3];
            chars[11] = CrockfordBase32[((data[6] & 7) << 2) | ((data[7] & 192) >> 6)];
            chars[12] = CrockfordBase32[(data[7] & 62) >> 1];
            chars[13] = CrockfordBase32[((data[7] & 1) << 4) | ((data[8] & 240) >> 4)];
            chars[14] = CrockfordBase32[((data[8] & 15) << 1) | ((data[9] & 128) >> 7)];
            chars[15] = CrockfordBase32[(data[9] & 124) >> 2];
            chars[16] = CrockfordBase32[((data[9] & 3) << 3) | ((data[10] & 224) >> 5)];
            chars[17] = CrockfordBase32[data[10] & 31];

            chars[18] = CrockfordBase32[(data[11] & 248) >> 3];
            chars[19] = CrockfordBase32[((data[11] & 7) << 2) | ((data[12] & 192) >> 6)];
            chars[20] = CrockfordBase32[(data[12] & 62) >> 1];
            chars[21] = CrockfordBase32[((data[12] & 1) << 4) | ((data[13] & 240) >> 4)];
            chars[22] = CrockfordBase32[((data[13] & 15) << 1) | ((data[14] & 128) >> 7)];
            chars[23] = CrockfordBase32[(data[14] & 124) >> 2];
            chars[24] = CrockfordBase32[((data[14] & 3) << 3) | ((data[15] & 224) >> 5)];
            chars[25] = CrockfordBase32[data[15] & 31];

            return new string(chars);
        }
    }
}
