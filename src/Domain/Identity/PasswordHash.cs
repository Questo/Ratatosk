using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Identity;

public sealed class PasswordHash : ValueObject
{
    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    // Rebuilds a value that was valid when it was stored, without re-running today's validation.
    internal static PasswordHash FromStored(string value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static Result<PasswordHash> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<PasswordHash>.Failure("Hashes cannot be empty.");
        }

        return Result<PasswordHash>.Success(new PasswordHash(value));
    }

    public override string ToString() => Value;
}

