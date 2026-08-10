using System;
using System.Security.Cryptography;
using System.Text;
using VeilWar.Core;

namespace VeilWar.Fog
{
    /// <summary>
    /// Commit-reveal helpers for PLAN mechanic A.
    /// Hash format: SHA256("{x},{y},{salt}") as lowercase hex — keep identical onchain later.
    /// </summary>
    public static class CommitReveal
    {
        public static string CreateSalt()
        {
            Span<byte> bytes = stackalloc byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string HashPosition(GridCoord coord, string salt)
        {
            if (string.IsNullOrWhiteSpace(salt))
                throw new ArgumentException("Salt required.", nameof(salt));

            var payload = $"{coord.X},{coord.Y},{salt}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static bool Verify(GridCoord coord, string salt, string commitHashHex)
        {
            if (string.IsNullOrWhiteSpace(commitHashHex)) return false;
            var expected = HashPosition(coord, salt);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(commitHashHex.ToLowerInvariant()));
        }
    }
}
