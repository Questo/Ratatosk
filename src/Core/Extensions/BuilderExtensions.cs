using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.Extensions;

public static class BuilderExtensions
{
    public static Result<T> BuildResult<T>(this IBuilder<T> builder)
        where T : class
    {
        try
        {
            return Result<T>.Success(builder.Build());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Failed to build {typeof(T).Name}: {ex.Message}");
        }
    }
}
