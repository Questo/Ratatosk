using System.Text.RegularExpressions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Identity;

public sealed partial class Email : ValueObject
{
    [GeneratedRegex("^[\\w-\\.]+@([\\w-]+\\.)+[\\w-]{2,4}$")]
    private static partial Regex EmailPattern();

    public string Value { get; } = default!;

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !EmailPattern().IsMatch(value))
        {
            return Result<Email>.Failure("Invalid email address");
        }

        return Result<Email>.Success(new Email(value));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
