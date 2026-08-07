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

## Signing keys

| Environment | Configuration |
|-------------|---------------|
| Development / Testing | Ephemeral keys (default), or set `OpenIddict:SigningKey` |
| Production / multi-instance | **Required:** `OpenIddict:SigningKey` (≥ 32 UTF-8 bytes), e.g. Key Vault secret |

Without a shared signing key, tokens issued on one Container App replica will not validate on another.

## Blazor vs HTTP

Blazor Server keeps calling `ICommandBus` / `IQueryBus` in-process. `/api/v1` maps to the same
handlers so mobile and web share business logic without forcing Blazor through an HTTP hop.

## ADO note

Epic E9.F4 originally mentioned Entra ID / MSAL. Implementation uses **OpenIddict + JWT** on
local Identity instead (no Entra).
