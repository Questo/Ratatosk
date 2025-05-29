using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Catalog.ValueObjects;

/// <summary>
/// Represents the name of a product in the catalog.
/// Product names must be between 3 and 100 characters and consist of letters, numbers, spaces,
/// hyphens, apostrophes, or ampersands.
/// </summary>
public sealed partial class ProductName : ValueObject
{
    [GeneratedRegex("^[A-Za-z0-9 '&-]{3,100}$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    public string Value { get; } = default!;

    private ProductName(string value)
    {
        Value = value;
    }

    public static Result<ProductName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !NamePattern().IsMatch(value))
        {
            return Result<ProductName>.Failure("Invalid product name format");
        }

        return Result<ProductName>.Success(new ProductName(value.Trim()));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
