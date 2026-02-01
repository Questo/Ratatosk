using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Identity;

public sealed class Password : ValueObject
{
    public string Value { get; }

    private Password(string value)
    {
        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static Result<Password> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Password>.Failure("Passwords cannot be empty.");
        }

        return Result<Password>.Success(new Password(value));
    }

    public override string ToString() => Value;
}
