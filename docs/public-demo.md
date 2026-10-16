# Public demo

The public entry point is `/demo`. Each visitor receives a passwordless temporary Identity user, eight original sample recipes, one shopping list and three pantry staples. The existing application screens handle viewing and editing. Registration creates a separate, empty account.

## Enable and configure

The demo is disabled by default. Deploy the application, bundled photos and `AddPublicDemoSessions` migration together, then set `Demo__Enabled=true`. Link to `https://<application-host>/demo` from the portfolio website.

Configuration keys under `Demo` and their defaults:

| Key | Default |
| --- | ---: |
| `Enabled` | `false` |
| `LifetimeMinutes` | `60` |
| `CleanupIntervalMinutes` | `5` |
| `StartsPerIpPerHour` | `5` |
| `MaxActiveSessions` | `200` |
| `MaxResets` | `5` |
| `MaxRecipes` | `20` |
| `MaxLists` | `5` |
| `MaxItemsPerList` | `100` |
| `MaxPantryItems` | `100` |
| `MaxSaveAttempts` | `30` |

Lifetime is absolute. Refreshing, resuming and resetting do not extend it. Reset also preserves the save-attempt counter. A new revision invalidates existing circuits; those tabs must reload. Demo cookies cannot be renewed by Identity's sliding expiration or lose their demo claims through security-stamp renewal.

The start quota uses a durable one-hour window per hashed remote IP. Configure trusted forwarding at the hosting boundary correctly. Start, reset, cleanup and demo application operations use a SQL Server application lock to serialize changes across app instances. This deliberately favors predictable behavior over high demo throughput. Normal accounts do not acquire the demo lock.

Setting `Demo__Enabled=false` blocks new and existing demo sessions. Cleanup continues. Logical access stops at expiry; physical removal happens on the next cleanup pass while the application is running. With Container Apps scale-to-zero, cleanup resumes when the application next starts. Do not promise physical erasure exactly at the one-hour boundary.

## Restricted features and shared data

Importing pasted text, URLs or images, uploading recipe photos, editing shared ingredient tags, content reporting and moderation administration require a normal account. Both endpoint checks and application-handler decorators enforce these restrictions. New application operations are unavailable to demo users until explicitly added to `DemoOperationRunner.IsAllowed`.

Demo ingredient matching reads the shared catalog without writing match logs. An unmatched ingredient stays on its recipe with a null canonical ingredient ID. Demo recipe saves do not create ingredients or aliases. Quantity conversion uses existing controlled conversion rows and never calls AI or writes pending suggestions. Existing text-moderation settings still apply, and recipe save attempts are counted before moderation.

Recipe photos are bundled static assets. Demo recipe writes accept only an empty image or one of the eight exact demo image paths. Demo-created content is private to the temporary owner.

## Operations

Logs record starts, resets, ends, refused operations and cleanup failures without recipe content or raw IP addresses. Check cleanup errors after deployment. Expired sessions and their Identity users, recipes, shopping groups, pantry rows and moderation events are removed transactionally. Catalog records and static photos are preserved.

The background job waits for database readiness and runs every five minutes by default. Failures are logged and retried on the next pass. Start and reset are atomic; an exception rolls back partial content. Save attempts remain consumed after failed validation or moderation. Database unavailability uses the existing starting/failed pages, preserving the local destination.

## Verification

Run the normal application, component, integration and browser test projects. Integration tests use real Identity cookies in `DemoWebApplicationFactory`; existing persistence tests retain their fixed test identity. Demo browser tests use a one-minute lifetime to exercise expiry of an open circuit.

Docker SQL Server remains the default. On Windows without Docker, explicitly opt into an available SQL Server instance before running integration or browser tests:

```powershell
$env:RECIPELIBRARY_TEST_SQL = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true'
dotnet test tests/RecipeLibrary.Web.IntegrationTests
dotnet test tests/RecipeLibrary.Web.E2E.Tests -c Release
```

Each fixture creates a uniquely named `RecipeLibraryTests_<guid>` database and drops only that database on disposal. Use an instance on which creating and dropping these test databases is allowed.
