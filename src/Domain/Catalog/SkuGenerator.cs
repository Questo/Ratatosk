namespace Ratatosk.Domain.Catalog;

public static class SkuGenerator
{
    public static string Generate(string categoryCode)
    {
        var uniquePart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(); // e.g., "3FA7C1"
        return $"{categoryCode}-{uniquePart}";
    }
}
