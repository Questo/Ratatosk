using System;
using System.Text;

namespace Ratatosk.Infrastructure.Configuration;

public class AuthOptions
{
    public const string SectionName = "Authentication";

    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int ExpiresInMinutes { get; set; } = 60;

    /// <summary>
    /// Returns the signing key bytes, accepting either a UTF8 string or a base64-encoded secret.
    /// Throws if the resulting key is shorter than 256 bits (32 bytes), which HS256 requires.
    /// </summary>
    public byte[] GetKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(Secret))
        {
            throw new InvalidOperationException(
                "Authentication:Secret is missing. Provide a 256-bit (32-byte) secret."
            );
        }

        byte[] keyBytes;

        // If the secret looks like base64 (length divisible by 4 and uses padding), prefer decoding.
        if (Secret.Length % 4 == 0 && Secret.Contains('='))
        {
            try
            {
                keyBytes = Convert.FromBase64String(Secret);
            }
            catch (FormatException)
            {
                keyBytes = Encoding.UTF8.GetBytes(Secret);
            }
        }
        else
        {
            keyBytes = Encoding.UTF8.GetBytes(Secret);
        }

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"Authentication:Secret must be at least 32 bytes (256 bits). Current: {keyBytes.Length} bytes."
            );
        }

        return keyBytes;
    }
}
