using System.Text.RegularExpressions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Catalog.ValueObjects;

/// <summary>
/// Represents a product description, ensuring valid length and basic HTML sanitization.
/// </summary>
public sealed partial class Description : ValueObject
{
    private const int MinLength = 10;
    private const int MaxLength = 1000;

    public string Value { get; }

    private Description(string value)
    {
        Value = value;
    }

    public static Result<Description> Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<Description>.Failure("Description cannot be empty");
        }

        string sanitized = StripHtmlTags(input).Trim();

        if (sanitized.Length < MinLength)
        {
            return Result<Description>.Failure(
                $"Description must be at least {MinLength} characters long"
            );
        }

        if (sanitized.Length > MaxLength)
        {
            return Result<Description>.Failure(
                $"Description must not exceed {MaxLength} characters"
            );
        }

        return Result<Description>.Success(new Description(sanitized));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    private static string StripHtmlTags(string input)
    {
        return HtmlTagPattern().Replace(input, string.Empty);
    }

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();
}
