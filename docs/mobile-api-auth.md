# Mobile API authentication (OpenIddict / JWT)

Recipe Library issues JWTs for first-party mobile clients via **OpenIddict** on top of
ASP.NET Core Identity. Blazor continues to use **application cookies**. Both schemes
authenticate the same `AspNetUsers` store and the same `OwnerUserId` ownership model.

## Endpoints

| Endpoint | Purpose |
|----------|---------|
| `POST /connect/token` | Password + refresh token grants (`client_id=maui-app`) |
| `POST /connect/revoke` | Refresh/access token revocation (OpenIddict built-in) |
| `POST /api/v1/auth/register` | Create a local Identity user (then call token) |
| `GET/POST/PUT/DELETE /api/v1/recipes` | Recipe API (Bearer with `api` scope, or cookie) |
| `POST/GET /api/v1/recipe-images` | Image upload/download (Bearer with `api` scope, or cookie) |
| `GET /openapi/v1.json` | OpenAPI document (Development / Testing) |

Legacy browser routes (`/api/upload-recipe-image`, `/ingredients/*`, `/recipes/import*`) remain
for Blazor; prefer `/api/v1` for new mobile clients.

## Explicit application startup

Production hosts keep SQL asleep until an explicit user action. Before login or a foreground data load, the native client retrieves `/api/system/start-token`, retaining its antiforgery cookie, then posts `/api/system/start` with the returned token in `RequestVerificationToken` and `X-Application-Start: user`. Poll `/api/system/readiness` until `Ready`; a `Starting` response may be followed by another explicit start POST. Keep the original five-minute deadline, respect cancellation and retry temporary ingress failures. A `Failed` response stops the operation without sending credentials. Ordinary GET, HEAD and readiness requests do not start SQL.

The included `RecipeApiClient` performs this handshake for login and recipe loading. Direct API consumers must implement the same protected handshake when the host is cold.

## Token request (password)

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=...&password=...&client_id=maui-app&scope=api offline_access
```

Refresh:

```http
grant_type=refresh_token&refresh_token=...&client_id=maui-app
```

## Token keys

| Environment | Configuration |
|-------------|---------------|
| Development / Testing | Ephemeral keys by default |
| Production / multi-instance | **Required:** `OpenIddict:SigningKey` (≥ 32 UTF-8 bytes), e.g. Key Vault secret |

The Azure template creates both keys in Key Vault on first deployment and reuses them across revisions. All replicas must share both keys. Replacing a key without retaining its previous version in the application invalidates tokens that depend on it.

The mobile password flow does not support accounts with two-factor authentication enabled. Refresh also requires a current security stamp and an account that is allowed to sign in and is not locked out.

## Blazor vs HTTP

Blazor Server keeps calling `ICommandBus` / `IQueryBus` in-process. `/api/v1` maps to the same
handlers so mobile and web share business logic without forcing Blazor through an HTTP hop.

## ADO note

Epic E9.F4 originally mentioned Entra ID / MSAL. Implementation uses **OpenIddict + JWT** on
local Identity instead (no Entra).
