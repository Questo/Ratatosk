using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Ratatosk.Application.Authentication;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Infrastructure.Authentication;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public PasswordHash Hash(Password password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password.Value))
        {
            Salt = salt,
            Iterations = 4,
            MemorySize = 1024 * 64, // 64 MB
            DegreeOfParallelism = Environment.ProcessorCount,
        };

        var hash = argon2.GetBytes(HashSize);

        // Store salt + hash together
        var combined = Convert.ToBase64String(salt.Concat(hash).ToArray());

        return PasswordHash.Create(combined).Value!;
    }

    public bool Verify(Password password, PasswordHash stored)
    {
        var bytes = Convert.FromBase64String(stored.Value);

        var salt = bytes[..SaltSize];
        var expectedHash = bytes[SaltSize..];

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password.Value))
        {
            Salt = salt,
            Iterations = 4,
            MemorySize = 1024 * 64,
            DegreeOfParallelism = Environment.ProcessorCount,
        };

        var actualHash = argon2.GetBytes(HashSize);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
