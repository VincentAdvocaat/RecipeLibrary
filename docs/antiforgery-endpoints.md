# Antiforgery decisions (E16.F2.T5)

Cookie auth (Identity) + `SameSite=Lax` is the baseline CSRF control. Decisions for mutating routes:

| Route | Method | DisableAntiforgery | Decision |
|-------|--------|---------------------|----------|
| `/api/upload-recipe-image` | POST | Yes | Browser `fetch` FormData from `photoUploadZone.js`; keep disabled; rely on SameSite + auth. Prefer antiforgery header later. |
| `/api/recipe-images/{fileName}` | GET | No (removed) | Safe method; auth + ownership check only. |
| `/ingredients/match` | POST | Yes | Blazor loopback `HttpClient`; migrate to `ICommandBus`, then delete endpoint. |
| `/ingredients/parse-line` | POST | Yes | Blazor loopback; migrate to in-process parser. |
| `/recipes/import` | POST | Yes | Legacy API; Blazor uses `IQueryBus`. Keep for API clients; SameSite + auth. |
| `/recipes/import-url` | POST | Yes | Same as import. |
| `/recipes/import-image` | POST | Yes | Multipart API; SameSite + auth. |
| `/ingredients/search` | GET | N/A | Safe method. |
| `/tags/search` | GET | N/A | Safe method. |
| `/Account/Logout` | POST | Yes | Intentional: logout without token. |
| `/ingredients/{id}/tags` | POST | Yes | Blazor loopback; migrate to bus. |

**Not enabling full antiforgery in this change** for remaining Blazor loopback POSTs: circuit `HttpClient` does not send request tokens today. Follow-up: bus-ify those calls and delete the HTTP surface.
