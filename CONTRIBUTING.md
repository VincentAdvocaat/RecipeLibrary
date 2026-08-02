# Contributing

Thanks for helping with Recipe Library. Start here so local setup and PRs stay consistent.

## First run

Follow **[docs/getting-started.md](docs/getting-started.md)** (clone → Docker SQL → run within ~30 minutes).

## Branch & worktree workflow (required)

Do **not** commit on `main`. Every workstream uses its own branch and git worktree:

```powershell
./scripts/start-development.ps1 -Branch feature/<topic>
# work in .worktrees/feature/<topic>

./scripts/new-pr.ps1 -Message "AB#123 Short imperative summary"
./scripts/stop-development.ps1 -Branch feature/<topic>   # when done
```

- Branch prefixes: `feature/<topic>` or `bugfix/<topic>` (lowercase, hyphens).
- Policy: `.cursor/rules/worktrees-and-branches.mdc`

## Pull requests

- Reference Azure DevOps work items in the commit/PR (`AB#123`).
- Prefer small, reviewable PRs.
- CI runs on GitHub; boards live in Azure DevOps — see `docs/azure/ado-github-integration.md`.

## Coding conventions (short)

| Area | Rule |
|------|------|
| Code (C#, JS identifiers, comments, exceptions) | **English** |
| User-visible Blazor UI | **Dutch via resources** (`SharedResources.*.resx`), English keys |
| EF migrations | Always `dotnet ef migrations add` — never hand-edit migration files |

Details: `.cursor/rules/english-code-dutch-ui.mdc`, `.cursor/rules/ef-core-migrations.mdc`.

## Useful commands

```powershell
./scripts/start-local.ps1          # SQL + web app
dotnet test RecipeLibrary.slnx     # unit + integration (see docs/testing.md for E2E)
docker compose up -d sql --wait    # SQL only
```

## Questions

Open an issue or link the related Azure DevOps work item on the PR.
