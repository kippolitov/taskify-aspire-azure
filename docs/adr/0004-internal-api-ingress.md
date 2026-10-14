# The API is internal to the Container Apps environment

The API's ingress is `external: false`: only apps in the same Container Apps environment, which means the web app, can reach it, and requests from outside are rejected by the environment proxy. No environment-level HTTP route may target it, because such a route would expose it again. Since the API cannot be called from outside, the post-deploy smoke tests go through the web app: its home page must list the seeded Users, which proves the web-to-API-to-database path, and a request to the API's address from the runner must not succeed (D4 in `docs/hardening-modernization-plan.md`, FR-038).

## Consequences

- Manual API checks against Azure, such as `curl` to the API or the OpenAPI document, need a shell inside the environment, for example `az containerapp exec` on the web app.
- The weekly `load-test` job in `.github/workflows/benchmark.yml` calls the API's address directly and can no longer reach it.
