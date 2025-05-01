namespace Ratatosk.Core.Primitives;

public static class Guard
{
    public static void AgainstEmpty(Guid input, string paramName)
    {
        if (input == Guid.Empty)
            throw new ArgumentException(paramName);
    }
    public static void AgainstNull(object? input, string paramName)
    {
        if (input is null)
            throw new ArgumentNullException(paramName);
    }

    public static void AgainstNullOrEmpty(string? input, string paramName)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Value cannot be null or empty.", paramName);
    }

    public static void AgainstOutOfRange(int value, int min, int max, string paramName)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be between {min} and {max}.");
    }
}
