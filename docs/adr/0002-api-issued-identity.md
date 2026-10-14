# The API issues a short-lived identity after the user picker

After a team member picks a User on the landing screen, the web app calls `POST /api/auth/token`, and the API returns a JWT signed with HMAC-SHA256 that names that User in `sub` and expires after 15 minutes. Every other operation except the User list requires it, and the caller is always taken from `sub`, never from the request, so no request can act as another User by naming them (D2 in `docs/hardening-modernization-plan.md`). The web app holds the token per circuit and renews it silently when less than two minutes remain, so the picker experience is unchanged.

## Considered Options

- **Microsoft Entra ID sign-in**: real authentication, but out of scope; the product deliberately has no login.
- **Cookie authentication**: browser-oriented, and awkward for the server-to-server SignalR connection from the web app to the API.

## Consequences

- Known limit: anyone who can reach the landing screen can still pick any User. The identity proves which User the session picked, not who the person is.
- The signing key is a per-environment secret (an Aspire secret parameter locally, a Container Apps secret in Azure, fed from the `AUTH_SIGNING_KEY` GitHub secret). Rotating it ends every outstanding identity.
- An open live-update connection outlives its token; reconnecting needs a valid one.
