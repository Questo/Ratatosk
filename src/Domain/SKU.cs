using System.Text.RegularExpressions;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain;

/// <summary>
/// Represents a Stock Keeping Unit (SKU), a unique identifier for a product used in inventory systems.
/// The SKU format enforced here follows a strict pattern:
/// 
/// <para><c>^[A-Z]{2,4}-[A-Z0-9]{4,8}(-[A-Z0-9]+)*$</c></para>
/// 
/// <para>- Starts with 2–4 uppercase letters (typically a category or vendor code)  </para>
/// <para>- Followed by a hyphen and 4–8 uppercase alphanumeric characters  </para>
/// <para>- Optionally followed by additional hyphen-separated uppercase alphanumeric segments  </para>
/// 
/// <para>Examples: <c>TS-3FA7C1</c>, <c>BK-ABCD1234-XYZ</c></para>
/// </summary>
public sealed partial class SKU : ValueObject
{
    [GeneratedRegex(@"^[A-Z]{2,4}-[A-Z0-9]{4,8}(-[A-Z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();

    public string Value { get; } = default!;

    private SKU(string value)
    {
        Value = value;
    }

    public static Result<SKU> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !SkuPattern().IsMatch(value))
        {
            return Result<SKU>.Failure("Invalid SKU format");
        }

        return Result<SKU>.Success(new SKU(value));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
