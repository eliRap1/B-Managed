using System;
using System.Security.Cryptography;
using System.Text;

namespace Model.Helpers
{
    /// <summary>
    /// Provides password hashing and verification using PBKDF2
    /// </summary>
    public static class SecurityHelper
    {
        private const int SaltSize = 16; // 128 bits
        private const int HashSize = 20; // 160 bits
        private const int Iterations = 10000; // PBKDF2 iterations

        /// <summary>
        /// Hashes a password using PBKDF2
        /// </summary>
        /// <param name="password">Plain text password</param>
        /// <returns>Base64 encoded hash (salt + hash)</returns>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // Generate a random salt
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            // Hash the password
            byte[] hash = HashPasswordWithSalt(password, salt);

            // Combine salt and hash
            byte[] hashBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, hashBytes, 0, SaltSize);
            Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

            // Convert to base64 for storage
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verifies a password against a hash
        /// </summary>
        /// <param name="password">Plain text password to verify</param>
        /// <param name="hashedPassword">Base64 encoded hash from database</param>
        /// <returns>True if password matches</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            if (string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                // Decode the hash
                byte[] hashBytes = Convert.FromBase64String(hashedPassword);

                // Extract salt
                byte[] salt = new byte[SaltSize];
                Array.Copy(hashBytes, 0, salt, 0, SaltSize);

                // Extract hash
                byte[] storedHash = new byte[HashSize];
                Array.Copy(hashBytes, SaltSize, storedHash, 0, HashSize);

                // Hash the input password with the extracted salt
                byte[] computedHash = HashPasswordWithSalt(password, salt);

                // Compare hashes
                return SlowEquals(storedHash, computedHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hashes a password with a given salt using PBKDF2
        /// </summary>
        private static byte[] HashPasswordWithSalt(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        /// <summary>
        /// Constant-time comparison to prevent timing attacks
        /// </summary>
        private static bool SlowEquals(byte[] a, byte[] b)
        {
            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }

        /// <summary>
        /// Sanitizes input to prevent SQL injection.
        /// </summary>
        /// <remarks>
        /// DO NOT USE — this blacklist-based approach is bypassable (Unicode
        /// lookalikes, CHAR() injections, Access-specific syntax, etc.) and
        /// creates a false sense of security. All callers must use
        /// OleDbParameter (parameterised queries) instead.
        /// </remarks>
        [Obsolete("Do not use SanitizeInput. Use OleDbParameter / parameterised queries exclusively.")]
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove or escape dangerous characters
            return input.Replace("'", "''")
                       .Replace("--", "")
                       .Replace(";", "")
                       .Replace("/*", "")
                       .Replace("*/", "")
                       .Replace("xp_", "")
                       .Replace("sp_", "");
        }

        /// <summary>
        /// Validates that a string is safe for SQL (alphanumeric + limited special chars)
        /// </summary>
        public static bool IsSafeString(string input, int maxLength = 100)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            if (input.Length > maxLength)
                return false;

            // Allow only alphanumeric, spaces, and safe special characters
            foreach (char c in input)
            {
                if (!char.IsLetterOrDigit(c) &&
                    c != ' ' &&
                    c != '@' &&
                    c != '.' &&
                    c != '-' &&
                    c != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}