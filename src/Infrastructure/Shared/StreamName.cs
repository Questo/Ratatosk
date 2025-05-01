namespace Ratatosk.Infrastructure.Shared
{
    public static class StreamName
    {
        public static string For<T>(Guid id) => $"{typeof(T).Name.ToLowerInvariant()}-{id}";
    }
}