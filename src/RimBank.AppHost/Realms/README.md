# Keycloak realms (local development only)

Keycloak imports these files the first time the AppHost starts it (`WithRealmImport`, ADR 0019). They are **for local development only**. Aspire does not publish realm imports, and a deployed environment configures its own identity provider and sets `Authentication:Authority` on `RimBank.Host`.

`rimbank-realm.json` defines:

- the realm `rimbank`;
- `rimbank-api`, the audience every access token must carry (no one signs in to it);
- `rimbank-dev-client`, a confidential client for the client-credentials grant. Its secret, `rimbank-dev-client-secret-local-only`, is published here on purpose and is not a secret. Never reuse it.
- `rimbank-mcp-client`, a public client for MCP clients such as MCP Inspector or Claude Code. It uses the authorization code flow with PKCE `S256` and only redirects to `http://127.0.0.1` (any port) or `http://localhost:6274` (MCP Inspector) (ADR 0022).
- `rimbank-developer`, the user who signs in through `rimbank-mcp-client`. Its password, `rimbank-developer-local-only`, is published here on purpose. Never reuse it.

The admin password is an Aspire-generated parameter kept in user secrets, never in source.

Keycloak keeps the realm in the `rimbank-keycloak-data` volume, so later edits to the file are not re-imported. Remove the volume (`docker volume rm rimbank-keycloak-data`) to import the file again.

To get a token (the Keycloak port is shown in the Aspire dashboard):

```bash
curl -s -X POST "http://localhost:<keycloak-port>/realms/rimbank/protocol/openid-connect/token" \
  -d grant_type=client_credentials \
  -d client_id=rimbank-dev-client \
  -d client_secret=rimbank-dev-client-secret-local-only
```
