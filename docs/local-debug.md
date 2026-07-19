# Local debug with Docker SQL

When you run the web app on your machine (F5 / "Debug (local)"), it connects to a **local** SQL Server running in Docker. This doc describes how to start the database and verify the setup.

## 1. Start the SQL container

From the repository root:

```powershell
docker compose up -d sql --wait
```

Or run the batch file (works from any directory):

```cmd
start-database.bat
```

- `sql` is the SQL Server 2022 service defined in `docker-compose.yml`.
- `--wait` blocks until the service is **healthy** (Docker Compose v2).

If your Compose version does not support `--wait`, run:

```powershell
docker compose up -d sql
```

Then wait until the container is healthy (e.g. check with `docker compose ps`).

## Quick start (one command)

Install global CLI commands once:

```powershell
./scripts/install-cli.ps1
```

Restart PowerShell, then from any directory:

```powershell
rlstart
# or
recipelibrary start
```

This starts SQL (Docker), runs the web app, waits until it responds, and prints:

- `http://localhost:5197`
- `https://localhost:5196`

Press Ctrl+C to stop the web app. SQL keeps running until `docker compose stop sql`.

Script details: `scripts/start-local.ps1`

## 2. Connection string (local)

The app reads the connection string from configuration. For **local debug**:

- Copy `.env.example` to `.env` and set `MSSQL_SA_PASSWORD` (same password used by the Docker SQL container).
- In Development, if `ConnectionStrings__RecipeDb` is not set, the app builds a fallback using `localhost,1433` and `MSSQL_SA_PASSWORD` from the environment (loaded from `.env` via DotNetEnv).

So one `.env` file is used both by `docker compose` (for the container) and by the app when debugging on the host.

## 3. Verificatie

- **Container status**: `docker compose ps` — the `sql` service should show as **healthy**.
- **Connectivity**: From the host you can test with a SQL client using:
  - Server: `localhost,1433`
  - User: `sa`
  - Password: value of `MSSQL_SA_PASSWORD` from your `.env`

  Or from a shell (with `.env` loaded or `$env:MSSQL_SA_PASSWORD` set):

  ```powershell
  docker compose exec sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $env:MSSQL_SA_PASSWORD -C -Q "SELECT 1"
  ```

- **App startup**: In Development, if the app cannot connect to RecipeDb at startup, it logs: *"Cannot connect to RecipeDb. Is the SQL container running?"* (it does not fail hard, to avoid frustration with transient startup issues).

## 4. VS Code / Cursor

Use the **"Debug (local)"** launch configuration. It runs the `start-sql` pre-launch task (which runs `docker compose up -d sql --wait`) before starting the app, so the database is up when the debugger attaches.

## 5. Visual Studio

Visual Studio does not run a pre-launch task automatically. **Start the SQL container first**:

```powershell
docker compose up -d sql --wait
```

Then start the Web project (F5) as usual. Ensure `.env` exists with `MSSQL_SA_PASSWORD` so the app can use the local connection string fallback (or set `ConnectionStrings__RecipeDb` in user secrets / environment).

## 6. Local Identity seed user

Auth is always on (ASP.NET Core Identity). For a Development-only test account, set user secrets or `.env`:

```powershell
dotnet user-secrets set "Identity:SeedUser:Email" "you@example.com" --project src/Web/RecipeLibrary.Web
dotnet user-secrets set "Identity:SeedUser:UserName" "yourname" --project src/Web/RecipeLibrary.Web
dotnet user-secrets set "Identity:SeedUser:Password" "ChangeMe!123" --project src/Web/RecipeLibrary.Web
```

Or in `.env` (gitignored):

```
Identity__SeedUser__Email=you@example.com
Identity__SeedUser__UserName=yourname
Identity__SeedUser__Password=ChangeMe!123
```

On startup in Development, the user is created if missing; if the password in config differs, it is updated via `UserManager` (no email confirmation).

The Identity migration clears existing recipes that have no owner (`OwnerUserId` empty) so private libraries stay fail-closed. Re-seed or recreate recipes after migrating a database that had pre-Identity data.
