namespace Ratatosk.Application.Shared;

/// <summary>
/// Represents the result of a paginated query, including the items on the current page
/// and metadata such as total item count and total page count.
/// </summary>
public sealed class Pagination<T> where T : class
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalItems { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
}
