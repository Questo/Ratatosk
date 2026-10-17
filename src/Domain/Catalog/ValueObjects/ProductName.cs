using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Catalog.ValueObjects;

/// <summary>
/// Represents the name of a product in the catalog.
/// Product names must not be blank, must be between <see cref="MinLength"/> and
/// <see cref="MaxLength"/> characters and consist of letters, numbers, spaces, hyphens,
/// apostrophes, or ampersands. Each rule has its own error message, and the first rule that
/// fails is reported.
/// </summary>
public sealed partial class ProductName : ValueObject
{
    private const int MinLength = 3;
    private const int MaxLength = 100;

    [GeneratedRegex(
        "^[A-Za-z0-9 '&-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    )]
    private static partial Regex NamePattern();

    public string Value { get; } = default!;

    private ProductName(string value)
    {
        Value = value;
    }

    public static Result<ProductName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<ProductName>.Failure("Product name cannot be empty");
        }

        if (value.Length < MinLength)
        {
            return Result<ProductName>.Failure(
                $"Product name must be at least {MinLength} characters"
            );
        }

        if (value.Length > MaxLength)
        {
            return Result<ProductName>.Failure(
                $"Product name must be at most {MaxLength} characters"
            );
        }

        if (!NamePattern().IsMatch(value))
        {
            return Result<ProductName>.Failure(
                "Product name can only contain letters, numbers, spaces, hyphens, apostrophes and ampersands"
            );
        }

        return Result<ProductName>.Success(new ProductName(value.Trim()));
    }

    // Rebuilds a value that was valid when it was stored, without re-running today's validation.
    internal static ProductName FromStored(string value) => new(value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
