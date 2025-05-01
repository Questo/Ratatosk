using System.Globalization;
using Ratatosk.Core.BuildingBlocks;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Domain.Catalog;

public sealed class Price : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; } = default!;

    private Price(decimal amount, string Currency)
    {
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = Currency.ToUpperInvariant();
    }

    public static Result<Price> Create(decimal amount, string currency = "SEK")
    {
        if (amount < 0)
            return Result<Price>.Failure("Price cannot be negative");

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Result<Price>.Failure("Currency must be a valid 3-letter ISO code");

        return Result<Price>.Success(new Price(amount, currency));
    }

    public static Price Free() => new(0, "SEK");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0} {1:0.00}", Currency, Amount);
}