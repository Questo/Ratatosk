using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Ordering;

namespace Ratatosk.Application.Ordering.Queries;

/// <summary>
/// Resolves an optional status filter to its canonical <see cref="OrderStatus"/> name (what the
/// projection stores), ignoring case. Numeric values are not names, so they are rejected.
/// </summary>
internal static class OrderStatusFilter
{
    public static Result<string?> Resolve(string? status)
    {
        if (status is null)
            return Result<string?>.Success(null);

        var names = Enum.GetNames<OrderStatus>();
        var match = names.FirstOrDefault(n => string.Equals(n, status, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? Result<string?>.Failure(
                $"Unknown status '{status}'. Valid statuses: {string.Join(", ", names)}."
            )
            : Result<string?>.Success(match);
    }
}
