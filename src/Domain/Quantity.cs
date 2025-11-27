using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain;

/// <summary>
/// Represents a measurable amount of something, such as stock, weight, or volume,
/// paired with a unit of measurement. This value object is used to capture both the
/// magnitude and the semantic meaning of the quantity in domain models.
///
/// <para>Examples:</para>
/// <para>- <c>Quantity.Create(5, "pcs")</c> (five individual units)  </para>
/// <para>- <c>Quantity.Create(200, "ml")</c> (two hundred milliliters)  </para>
///
/// <para>Rules enforced:</para>
/// <para>- <c>amount</c> must be zero or positive  </para>
/// <para>- <c>unit</c> must be one of the supported units (e.g., "pcs", "kg", "g", "L", "ml", "m", "cm", "mm")  </para>
/// </summary>
public sealed class Quantity : ValueObject
{
    public int Amount { get; }
    public string Unit { get; } = default!;

    private static readonly HashSet<string> ValidUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "pcs",
        "kg",
        "g",
        "L",
        "ml",
        "m",
        "cm",
        "mm",
    };

    private Quantity(int amount, string unit)
    {
        Amount = amount;
        Unit = ValidUnits.First(u => u.Equals(unit, StringComparison.OrdinalIgnoreCase));
    }

    public static Result<Quantity> Create(int amount, string unit)
    {
        if (amount < 0)
            return Result<Quantity>.Failure("Quantity cannot be negative");

        if (string.IsNullOrWhiteSpace(unit))
            return Result<Quantity>.Failure("Unit cannot be empty");

        if (!ValidUnits.Contains(unit))
            return Result<Quantity>.Failure($"Unit '{unit}' is not supported");

        return Result<Quantity>.Success(new Quantity(amount, unit));
    }

    public static Quantity Pieces(int amount) => new(amount, "pcs");

    public static Quantity Kilograms(int amount) => new(amount, "kg");

    public static Quantity Grams(int amount) => new(amount, "g");

    public static Quantity Liters(int amount) => new(amount, "L");

    public static Quantity Milliliters(int amount) => new(amount, "ml");

    public static Quantity Meters(int amount) => new(amount, "m");

    public static Quantity Centimeters(int amount) => new(amount, "cm");

    public static Quantity Millimeters(int amount) => new(amount, "mm");

    public static Quantity operator +(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);
        return new Quantity(left.Amount + right.Amount, left.Unit);
    }

    public static Quantity operator -(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);

        var resultAmount = left.Amount - right.Amount;
        if (resultAmount < 0)
            throw new InvalidOperationException("Resulting quantity cannot be negative.");

        return new Quantity(resultAmount, left.Unit);
    }

    public static bool operator >(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Quantity left, Quantity right)
    {
        EnsureSameUnit(left, right);
        return left.Amount <= right.Amount;
    }

    private static void EnsureSameUnit(Quantity a, Quantity b)
    {
        if (!string.Equals(a.Unit, b.Unit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot operate on quantities with different units: '{a.Unit}' vs '{b.Unit}'."
            );
        }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Unit;
    }
}
