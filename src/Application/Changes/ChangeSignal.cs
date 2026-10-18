namespace Ratatosk.Application.Changes;

/// <summary>
/// Tells a client that a read model changed: <paramref name="Kind"/> is <c>order</c>,
/// <c>stock</c> or <c>product</c>, and <paramref name="Version"/> is the version of the aggregate
/// stream the change came from. It carries no payload; clients refetch the read model.
/// </summary>
public sealed record ChangeSignal(string Kind, Guid Id, int Version);
