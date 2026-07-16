# Ingredient catalog seed data

Curated culinary ingredients (**English** + **Dutch**) used to bootstrap the RecipeLibrary ingredient catalog.

**Full documentation:** [docs/ingredient-catalog.md](../../docs/ingredient-catalog.md)

That doc covers:

- Why the list exists (canonical matching on a fresh DB)
- Sources (Open Food Facts + manual Dutch staples)
- How generation/filtering works
- JSON schema and language keys (`en`, `nl`, extensible via OFF prefixes)
- How this maps to `CanonicalIngredient` / aliases
- **Runtime seeding** after `Database.Migrate()` via `IngredientCatalogSeeder`

## Files in this folder

| File | Purpose |
|------|---------|
| `curated-ingredients.json` | Source of truth (schema + ingredients); also embedded in Infrastructure |
| `curated-ingredients.csv` | Spreadsheet-friendly en/nl review |

## Regenerate JSON/CSV

From the repository / worktree root:

```powershell
./scripts/generate-curated-ingredients.ps1
```

This overwrites the JSON and CSV only — not this README or `docs/ingredient-catalog.md`. After regenerating, restart the app; the seeder inserts only new normalized names/aliases.
