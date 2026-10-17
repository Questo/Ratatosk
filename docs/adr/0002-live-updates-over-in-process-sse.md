# Live updates over in-process SSE

The back office learns about changes (an Order going from Placed to Confirmed or Cancelled, Stock moving, Products changing) from a Server-Sent Events stream. Each message is only a change signal (`kind`, `id`, `version`), and the UI refetches through the normal read endpoints. The stream is fed by a domain event handler that writes to the connected clients of its own API instance, so **live updates only work with a single API instance**.

## Considered Options

- **Polling** `GET /orders/{id}`: simplest, but only covers the Order the user is looking at, not a live back office.
- **SignalR**: two-way, which we don't need. It also adds a client library and its own protocol on top of what plain HTTP SSE does.
- **Full read models in the stream**: would create a second read path that has to stay consistent with the query endpoints. With signals only, a missed message costs nothing, because the next refetch corrects the view.
- **Postgres `LISTEN/NOTIFY` fan-out**: needed for several instances; deferred. It can sit behind the same broadcaster interface.
