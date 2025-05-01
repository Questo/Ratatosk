namespace Ratatosk.Core.Primitives;

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static Error Null(string message) => new("Null", message);

    public static Error FromException(Exception ex)
        => new("exception", ex.Message);
}
