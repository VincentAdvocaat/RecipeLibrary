# Getting started (≈ 30 minutes)

Goal: clone this repo and run Recipe Library locally with Docker SQL.

## Prerequisites

| Tool | Notes |
|------|--------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Project targets `net10.0` (`global.json` pins the major) |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Running before you start SQL |
| [Git](https://git-scm.com/) | Clone + worktree workflow |
| Optional: [VS Code](https://code.visualstudio.com/) / Cursor / Visual Studio 2022+ | Open the repo; accept recommended extensions in VS Code/Cursor |

No Node.js or npm is required (Tailwind runs via a NuGet standalone CLI on `dotnet build`).

## Fast path (CLI)

From a PowerShell prompt:

```powershell
git clone https://github.com/VincentAdvocaat/RecipeLibrary.git
cd RecipeLibrary

# Creates .env from .env.example if missing
./scripts/start-local.ps1
```

When the script prints the URLs:

- Frontend HTTP: `http://localhost:5197`
- Frontend HTTPS: `https://localhost:5196`

Open the HTTP URL, register an account (or use a Development seed user — see below), and you should reach the recipes overview.

Press Ctrl+C to stop the web app. SQL keeps running until you run `docker compose stop sql`.

### Optional: global `rlstart` command

```powershell
./scripts/install-cli.ps1
# restart PowerShell, then from any directory:
rlstart
```

## Fast path (VS Code / Cursor)

1. Copy `.env.example` → `.env` (or let `start-local.ps1` do it).
2. Ensure Docker Desktop is running.
3. Open the repo folder.
4. Install recommended extensions when prompted (C# / .NET).
5. Run **Debug (local)** (F5). That starts SQL, builds the web project, then launches the app.

## Local seed user (optional)

Auth uses ASP.NET Core Identity. For a Development-only login without registering, set in `.env`:

```
Identity__SeedUser__Email=dev@example.com
Identity__SeedUser__UserName=dev
Identity__SeedUser__Password=ChangeMe!123
```

On startup in Development the user is created/updated if missing. Do not use these values outside local machines.

## Verify

- `docker compose ps` — `sql` is **healthy**
- Browser opens `http://localhost:5197` and shows login/register
- After sign-in you can open `/recipes`

## Next reading

| Topic | Doc |
|-------|-----|
| Local SQL details & troubleshooting | [local-debug.md](local-debug.md) |
| Connect SSMS / Azure Data Studio | [database-connection.md](database-connection.md) |
| Tests | [testing.md](testing.md) |
| Content moderation (feature flag) | [content-moderation.md](content-moderation.md) |
| Azure test environment | [azure/test-runbook.md](azure/test-runbook.md) |
| How we contribute | [../CONTRIBUTING.md](../CONTRIBUTING.md) |

## Out of scope for this guide

Azure DevOps pipeline setup is covered under epic E4 / [azure/pipeline-setup.md](azure/pipeline-setup.md), not the local 30-minute path.
