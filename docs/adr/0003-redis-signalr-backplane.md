# Redis backplane for live updates, with WebSockets only from the web app

With more than one API replica, a change saved by one replica must reach viewers connected to another, so the API uses the SignalR Redis backplane (channel prefix `taskify`) on Azure Managed Redis, Balanced B0: one node in dev, two with high availability in prod. Microsoft's scaling guidance says the backplane still needs sticky sessions unless every client uses WebSockets only with negotiation skipped; the API's only hub clients are the web app's servers, so `BoardHubClient` connects with `SkipNegotiation` over WebSockets to an address resolved through service discovery, and the API needs no session affinity (D3 in `docs/hardening-modernization-plan.md`).

## Considered Options

- **Azure SignalR Service**: Microsoft's general recommendation for SignalR on Azure, and it needs no affinity, but it changes the hosting model.
- **Azure Cache for Redis**: being retired on 2028-09-30; Microsoft advises against new work on it.
- **Redis as a container app**: cheapest, but an unmanaged stateful service with no SLA.
- **Sticky sessions on the API with negotiation**: needs cookie handling in the .NET client.

## Consequences

- A broadcast that fails, for example while Redis is down, is logged and counted (`taskify.broadcasts.failed`); it never fails the saved change. Viewers on other replicas may miss that update. Readiness does not depend on Redis.
- `HttpMessageHandlerFactory` is not used for WebSockets, so the web app must resolve the API's real address before connecting.
- Redis authentication uses an access key in a Container Apps secret. Microsoft Entra authentication is a follow-up, like managed identity for PostgreSQL.
