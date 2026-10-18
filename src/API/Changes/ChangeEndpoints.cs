using System.Runtime.CompilerServices;
using Ratatosk.Application.Changes;

namespace Ratatosk.API.Changes;

public static class ChangeEndpoints
{
    private const string ChangesTag = "Changes";
    public const string StreamChangesName = "StreamChanges";

    public static void MapChangeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/changes",
                (IChangeBroadcaster broadcaster, HttpContext context) =>
                    TypedResults.ServerSentEvents(
                        StreamAsync(
                            context.Response,
                            broadcaster.SubscribeAsync(context.RequestAborted),
                            context.RequestAborted
                        ),
                        eventType: "change"
                    )
            )
            .RequireAuthorization(Policies.Admin)
            .WithTags(ChangesTag)
            .WithName(StreamChangesName)
            .WithSummary("Stream change signals")
            .WithDescription(
                "Server-Sent Events stream of change signals ({ kind: order | stock | product, id, version }) from this API instance. Refetch the read model on each signal."
            )
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");
    }

    // The result only sends its headers with the first event; flush them up front so a client
    // sees the stream open (200, text/event-stream) before anything changes.
    private static async IAsyncEnumerable<ChangeSignal> StreamAsync(
        HttpResponse response,
        IAsyncEnumerable<ChangeSignal> signals,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await response.Body.FlushAsync(cancellationToken);

        await foreach (var signal in signals.WithCancellation(cancellationToken))
            yield return signal;
    }
}
