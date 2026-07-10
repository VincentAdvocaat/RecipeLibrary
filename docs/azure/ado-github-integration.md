# Azure DevOps + GitHub integratie

RecipeLibrary gebruikt **GitHub als enige bron van waarheid** voor code. Azure DevOps
wordt gebruikt voor backlog/boards en CI/CD — zonder handmatige repo-sync.

## Overzicht

| Onderdeel | Bron | URL |
|-----------|------|-----|
| Code & pull requests | GitHub | https://github.com/VincentAdvocaat/RecipeLibrary |
| Backlog & boards | Azure DevOps | https://dev.azure.com/vadvocaat/RecipeLibrary |
| CI/CD pipeline | Azure Pipelines (bouwt vanaf GitHub) | https://dev.azure.com/vadvocaat/RecipeLibrary/_build |

De oude Azure DevOps Git-repo (`RecipeLibrary` onder Repos) is **uitgeschakeld** om
verwarring te voorkomen. Clone en push altijd naar GitHub.

## Lokaal werken

```powershell
git clone https://github.com/VincentAdvocaat/RecipeLibrary.git
cd RecipeLibrary
```

Gebruik het bestaande worktree-workflow (`scripts/start-development.ps1`, enz.);
zie `.cursor/rules/worktrees-and-branches.mdc`.

## Work items koppelen aan commits en PR's

Zet in commit-berichten of PR-titels/beschrijvingen een verwijzing naar het work item:

```text
Fix ingredient search pagination AB#42
```

Azure DevOps herkent `AB#<id>` en koppelt de commit of PR automatisch aan dat item.
Op het work item verschijnen dan de gerelateerde development-activiteiten.

Voorbeeld commit:

```powershell
git commit -m "Add shopping list export AB#15"
```

## CI/CD

De pipeline **VincentAdvocaat.RecipeLibrary** leest `azure-pipelines.yml` uit GitHub
(branch `main`). Triggers:

- **CI** bij pushes naar `main` en pull requests naar `main`
- **CD (test)** na succesvolle build op `main`: Bicep (`main.bicep`) + zip-deploy naar App Service in een bestaande resource group

Eenmalige Azure DevOps-configuratie (service connection, geheime variabelen,
environment `test`): zie **`pipeline-setup.md`**.

Recente builds draaien vanaf GitHub (geen Azure Repos nodig).

## Wat is al geconfigureerd

- GitHub service connection (`VincentAdvocaat`) voor pipelines
- GitHub Boards-koppeling met repo `VincentAdvocaat/RecipeLibrary`
- Pipeline gekoppeld aan GitHub (`main`, `azure-pipelines.yml`)
- Azure DevOps Git-repo uitgeschakeld (alleen GitHub gebruiken)

## Handige links

- [Backlog](https://dev.azure.com/vadvocaat/RecipeLibrary/_backlogs/backlog)
- [Boards](https://dev.azure.com/vadvocaat/RecipeLibrary/_boards/board)
- [Pipelines](https://dev.azure.com/vadvocaat/RecipeLibrary/_build)
- [GitHub repo](https://github.com/VincentAdvocaat/RecipeLibrary)
