# RecipeLibrary

A personal recipe library built with **.NET** and **Blazor Server**, designed to run on Azure using **free-tier friendly** services and **password-less** authentication (Microsoft Entra ID).

## Architecture at a glance

### Azure runtime + local development connectivity

```mermaid
flowchart TD
  DevLaptop[DevLaptop] -->|az_login_EntraID| Azure[Azure]
  UserBrowser[UserBrowser] -->|HTTPS| WebApp[AppService_WebApp]
  WebApp -->|ManagedIdentity_EntraID| AzureSQL[AzureSQL_Serverless_FreeOffer]
  DevLaptop -->|EntraID_SQLClient| AzureSQL
```

### Application architecture (Clean + CQRS/UseCases)

```mermaid
flowchart LR
  subgraph WebLayer[Web_Layer]
    BlazorUI[Blazor_Pages_Components]
  end

  subgraph Contracts[Application_Contracts]
    CreateRecipeCmd[CreateRecipeCommand]
    CreateRecipeResult[CreateRecipeResult]
    CommandBus[ICommandBus]
  end

  subgraph AppLayer[Application_Layer]
    CreateRecipeHandler[CreateRecipeCommandHandler]
    Validators[Command_Validators]
    InProcBus[InProcessBus]
  end

  subgraph DomainLayer[Domain_Layer]
    DomainModel[Entities_ValueObjects]
  end

  subgraph Abstractions[Application_Abstractions]
    IRecipeRepository[IRecipeRepository]
  end

  subgraph InfraLayer[Infrastructure_Layer]
    RecipeRepo[EfRecipeRepository]
    DbCtx[RecipeDbContext]
  end

  subgraph DataLayer[Data]
    AzureSQL[AzureSQL]
  end

  BlazorUI -->|creates| CreateRecipeCmd
  BlazorUI -->|calls| CommandBus
  CommandBus -->|dispatches| InProcBus
  InProcBus -->|resolves| CreateRecipeHandler
  Validators -->|validates| CreateRecipeCmd
  CreateRecipeHandler -->|maps_to| DomainModel
  CreateRecipeHandler -->|depends_on| IRecipeRepository
  IRecipeRepository -->|implemented_by| RecipeRepo
  RecipeRepo -->|uses| DbCtx
  DbCtx -->|connects| AzureSQL
```

This repo uses a layered structure (`Domain`/`Application`/`Infrastructure`/`Web`) and exposes use cases via Commands/Handlers dispatched through an in-process command bus.

## Repository structure

- **`src/Domain`**: domain model (entities, value objects, domain events).
- **`src/Application`**:
  - **`RecipeLibrary.Application.Contracts`**: commands/results + bus interfaces (UI-facing, no infrastructure).
  - **`RecipeLibrary.Application.Abstractions`**: ports (e.g. repositories) implemented by Infrastructure.
  - **`RecipeLibrary.Application`**: handlers, validators, in-process bus implementation, and DI registration.
- **`src/Infrastructure`**: EF Core persistence, repository implementations, external services.
- **`src/Web`**: Blazor Server UI and dependency injection wiring.

## Infrastructure (Azure, Bicep)

This repo provisions a **test** environment in Azure (region: `westeurope`) using Bicep:

- **App Service Plan**: Free tier (F1)
- **Web App**: System Assigned Managed Identity enabled
- **Azure SQL**: General Purpose **serverless** database using the **Azure SQL Database Free Offer**
- **Firewall rules**: allow Azure services + optional laptop public IP for debugging

See: `infra/README.md`

## Database & migrations (EF Core)

- EF Core migrations are used for schema management (no `EnsureCreated`).
- The app is intended to apply migrations in dev/test, with the same schema moving into Azure SQL.

See: `docs/azure/test-runbook.md`

## Local development (against Azure SQL, password-less)

High level:

- `az login`
- Set `ConnectionStrings__RecipeDb` to a password-less Azure SQL connection string (Entra auth)
- Run the web project

Full instructions: `docs/azure/test-runbook.md`

## Worktree workflow (required)

This repo uses a strict worktree workflow (agents and humans):

- Start a workstream: `scripts/start-development.ps1`
- Commit + create PR: `scripts/new-pr.ps1`
- Stop/cleanup: `scripts/stop-development.ps1`

Policy reference: `.cursor/rules/worktrees-and-branches.mdc`

## Cost guards

- This project is designed to stay **free-tier friendly**; always verify your Azure subscription’s free offerings and quotas.
- Azure SQL Free Offer is configured to **auto-pause until next month** when the free limit is exhausted.
- Set a budget/alert on the resource group/subscription to avoid surprises.

## Docs index

- `infra/README.md`
- `docs/azure/test-runbook.md`
- `docs/azure/sql-grants.sql`
