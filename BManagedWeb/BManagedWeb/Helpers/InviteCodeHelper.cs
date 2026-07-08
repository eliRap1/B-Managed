using System.Linq;
using System.Security.Cryptography;

namespace BManagedWeb.Helpers
{
    /// <summary>
    /// Generates cryptographically random invite codes.
    /// Format: PREFIX-XXXX where PREFIX is up to 4 alphanumeric characters
    /// derived from a seed (e.g. business name) and XXXX is 4 random
    /// characters from a confusable-free alphabet.
    ///
    /// Uses RandomNumberGenerator.GetInt32 (available in .NET 6+) rather than
    /// new Random() so simultaneous signups cannot produce the same code due
    /// to the seeded-with-clock-time collision that new Random() suffers in
    /// rapid succession on .NET Framework.
    /// </summary>
    public static class InviteCodeHelper
    {
        // Omits visually ambiguous characters I, O, 0, 1.
        private const string Alpha = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public static string NewInviteCode(string seed)
        {
            string prefix = new string((seed ?? "")
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(4)
                .ToArray());
            if (prefix.Length < 2) prefix = "BMNG";
            var tail = new string(Enumerable.Range(0, 4)
                .Select(_ => Alpha[RandomNumberGenerator.GetInt32(Alpha.Length)])
                .ToArray());
            return prefix + "-" + tail;
        }
    }
}
