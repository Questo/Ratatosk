using System.Text.Json;

namespace Ratatosk.API.Middleware;

public sealed class CleanupResponseMiddleware
{
    private readonly RequestDelegate _next;

    public CleanupResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            if (buffer.Length == 0)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            // Only handle JSON payloads.
            if (
                context.Response.ContentType?.Contains(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase
                ) != true
            )
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            buffer.Position = 0;
            using var doc = await JsonDocument.ParseAsync(
                buffer,
                cancellationToken: context.RequestAborted
            );
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            await using var cleaned = new MemoryStream();
            using var writer = new Utf8JsonWriter(cleaned);
            writer.WriteStartObject();
            var dropErrors = context.Response.StatusCode < 400;
            var traceId = context.TraceIdentifier;

            foreach (var prop in root.EnumerateObject())
            {
                if (dropErrors && (prop.NameEquals("Errors") || prop.NameEquals("errors")))
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            writer.WriteString("traceId", traceId);

            writer.WriteEndObject();
            await writer.FlushAsync(context.RequestAborted);

            cleaned.Position = 0;
            context.Response.ContentLength = cleaned.Length;
            await cleaned.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}
