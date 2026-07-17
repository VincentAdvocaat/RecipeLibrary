# Ingredient catalog (curated seed data)

This document explains the **purpose**, **sources**, and **mechanics** of the curated ingredient list under `data/ingredients/`.

## Why this exists

RecipeLibrary matches recipe lines, pantry items, and shopping-list rows against a shared **canonical ingredient** catalog (`CanonicalIngredient`: display name + normalized name + aliases).

Without seed data the catalog starts nearly empty and only grows when users type ingredients. That makes search/matching weak on a fresh database.

The curated list is meant to:

1. **Bootstrap** the catalog with a solid set of everyday cooking ingredients.
2. Keep names **culinary** (what you put in a recipe), not industrial additives or nutrition-table variants.
3. Store **both Dutch and English** up front, with a language-key scheme that can grow later.
4. Stay **reviewable** in git (JSON + CSV), separate from EF migrations and runtime code.

At application startup, after EF Core `Database.Migrate()`, `IngredientCatalogSeeder` loads the curated JSON (embedded in Infrastructure) and **idempotently** upserts canonical ingredients with NL/EN translations and aliases. Regenerating the JSON does not require a new EF migration.


## Design idea in one sentence

Take Open Food Facts’ multilingual ingredient taxonomy, keep only kitchen-useful entries that already have Dutch + English labels, fill obvious Dutch gaps by hand, and save the result as a versioned catalog with OFF-compatible language keys.

## What we deliberately do *not* do

| Approach | Why not (for now) |
|----------|-------------------|
| Dump all ~7k OFF taxonomy entries | Heavy on additives, E-numbers, processing aids; hurts autocomplete and matching |
| Seed NEVO wholesale | NEVO is a nutrition table (`Aardappelen rauw`, prepared dishes, brand cereals); great for nutrients, noisy for recipe names |
| Rely on runtime EN↔NL translation | The app normalizes text and matches aliases; it does not translate languages. Prefer storing both `en` and `nl` in the catalog |
| Put the catalog only in a migration `HasData` | Harder to review/diff; regenerating from OFF would fight EF snapshots |

NEVO remains useful as a **gap checker** (Dutch staples missing from OFF), not as the primary dump source.

## Where the data comes from

### Primary: Open Food Facts ingredients taxonomy

- File: [`taxonomies/food/ingredients.txt`](https://github.com/openfoodfacts/openfoodfacts-server/blob/main/taxonomies/food/ingredients.txt)
- Each ingredient is a block of lines. Language lines look like:

  ```text
  en: tomato, tomatoes
  nl: tomaat, tomaten
  fr: tomate, tomates
  < en: fruit vegetable
  ```

- The first name after the language key is the preferred form; further comma-separated values are synonyms (aliases).
- Parent links (`< en: …`) describe taxonomy hierarchy (vegetable, dairy, …).

**License:** OFF database contributions are generally under open terms (often ODbL for the database). Review [Open Food Facts terms](https://world.openfoodfacts.org/terms-of-use) before redistributing derived datasets widely. The JSON carries a short `source.licenseNote`.

### Secondary: manual Dutch kitchen staples

OFF coverage for Dutch is incomplete (~30% of taxonomy entries have an `nl:` line). Some everyday NL cooking terms are missing or awkward (`gehakt`, `andijvie`, `ketjap`, `bouillonblokje`, …).

Those are added in `scripts/generate-curated-ingredients.ps1` as an explicit manual list (English + Dutch), merged into the curated output.

## How generation works

```text
OFF ingredients.txt
        │
        ▼
  Keep blocks with both en: and nl:
        │
        ▼
  Drop additives / E-numbers / processing aids
  (name + parent heuristics)
        │
        ▼
  Score & prefer short culinary names
  (vegetables, dairy, spices, …)
        │
        ▼
  Merge manual staples / Dutch gaps
        │
        ▼
  data/ingredients/curated-ingredients.json
  data/ingredients/curated-ingredients.csv
```

Regenerate from the worktree root:

```powershell
./scripts/generate-curated-ingredients.ps1
```

The script downloads the OFF taxonomy to `%TEMP%\off-ingredients.txt` if needed, then overwrites the JSON and CSV. It does **not** overwrite this documentation or `data/ingredients/README.md`.

### Filters and scoring (summary)

- **Require** both `en` and `nl` labels (unless the row is purely manual).
- **Deny** names/parents that look like additives, emulsifiers, sweeteners, fibres/extracts used as industrial ingredients, etc.
- **Prefer** entries whose OFF parents look culinary (vegetable, fruit, herb, spice, meat, dairy, …) and short primary names (1–3 words).
- Cap the auto-selected set, then **force-include** a staple query list and the manual NL list.

Exact regexes live in the generator script; treat them as heuristics, not a food-science ontology.

## File layout

| Path | Role |
|------|------|
| `data/ingredients/curated-ingredients.json` | Source of truth for the curated catalog (schema + ingredients) |
| `data/ingredients/curated-ingredients.csv` | Flat `id,en,nl,aliases` view for spreadsheet review |
| `data/ingredients/README.md` | Short pointer into this doc |
| `scripts/generate-curated-ingredients.ps1` | Regenerates JSON/CSV from OFF + manual list |
| `src/Infrastructure/.../Persistence/Seed/IngredientCatalogSeeder.cs` | Loads embedded JSON and upserts into SQL |
| `docs/ingredient-catalog.md` | This document |

The JSON is linked into Infrastructure as an **embedded resource** (`RecipeLibrary.Infrastructure.SeedData.curated-ingredients.json`) so Docker/Azure do not need a loose file on disk.


## JSON schema (conceptual)

```json
{
  "schemaVersion": 1,
  "source": { "name": "...", "url": "...", "licenseNote": "..." },
  "languageKeys": {
    "included": ["en", "nl"],
    "howToExtend": "...",
    "availableInSource": ["en", "fr", "nl", "..."]
  },
  "count": 675,
  "ingredients": [
    {
      "id": "tomato",
      "names": {
        "en": ["tomato", "tomatoes"],
        "nl": ["tomaat", "tomaten"]
      },
      "offParents": ["en: fruit vegetable"]
    }
  ]
}
```

| Field | Meaning |
|-------|---------|
| `id` | Stable slug (usually from the English primary name). Use this as the join key when seeding or extending languages. |
| `names.<lang>[0]` | Preferred display name for that language |
| `names.<lang>[1…]` | Aliases / synonyms for matching |
| `offParents` | OFF parent links when the row came from OFF; empty for purely manual rows |

### Mapping to the domain (runtime seed)

| Catalog JSON | Domain / DB |
|--------------|-------------|
| `id` | `CanonicalIngredient.CatalogKey` |
| `names.nl[0]` | `IngredientTranslation` (`LanguageCode = nl`, `DisplayName`) |
| `names.nl[1…]` | `IngredientTranslationAlias` under the NL translation |
| `names.en[0]` | `IngredientTranslation` (`LanguageCode = en`, `DisplayName`) |
| `names.en[1…]` | `IngredientTranslationAlias` under the EN translation |

Quantity and unit stay on **recipe lines** (`RecipeIngredients`), not on the catalog entity.

## Runtime seeding (after Migrate)

```text
App start
  → EnsurePersistenceMigrated()
      → Database.Migrate()
      → IngredientCatalogSeeder.SeedAsync()
```

Behaviour:

- **Idempotent**: existing `CatalogKey` / `(IngredientId, LanguageCode)` / aliases are skipped; safe on every startup.
- **Language-aware matching**: search and match use a BCP-47 fallback chain (exact culture → parent → `en`).
- **Not an EF `HasData` migration**: catalog updates stay in JSON; no Designer/snapshot churn for hundreds of rows.
- Hook: `PersistenceServiceRegistration.EnsurePersistenceMigrated` in Infrastructure (called from `Program.cs`).

### Generator uniqueness and culinary overrides

After OFF selection + manual staples, `generate-curated-ingredients.ps1`:

1. Puts **manual names first** when merging onto an OFF row (so Dutch kitchen preferred forms win).
2. Strips known-bad aliases (e.g. `ketjap` off soy sauce; `witlof` off andijvie; outdated labels; scientific binomials).
3. Drops an explicit non-culinary id list (industrial sugars, processing fats, prepared sauces like marinade/vinaigrette, etc.).
4. **`Resolve-UniqueCatalog`**: merges entries that share an NL primary; drops aliases already claimed elsewhere; **keeps EN display names even when they normalize equal to the NL primary** (fixes empty `en: []`); **throws** if any normalized name would still be owned by two ids.

Examples kept intentionally separate: `andijvie` vs `witlof`, `ketjap` vs `sojasaus`, `clementine` vs `mandarijn`.

## Language keys (multi-language later)

Keys match OFF taxonomy prefixes:

| Key shape | Example | Meaning |
|-----------|---------|---------|
| ISO 639-1 | `en`, `nl`, `fr`, `de` | Language |
| Regional | `pt_br`, `zh_cn` | Underscore variant |
| Special | `xx` | Language-independent label |

The curated file currently includes **`en` and `nl` only**. Upstream OFF has many more languages; the full key list is stored in `languageKeys.availableInSource` inside the JSON so you can discover what exists without re-parsing the taxonomy.

**To add another language** (e.g. French):

1. Find the OFF block for the ingredient (by English primary name / `id`).
2. Copy the `fr: …` line into `names.fr` as a string array (split on commas).
3. Keep `names.nl[0]` as the canonical Dutch display name for the Dutch UI.

There is no in-app translator for ingredient names; extending `names` is the intended path.

## Relation to app behaviour today

| Feature | Role |
|---------|------|
| `IngredientTextNormalizer` | Lowercase, strip diacritics, light cleanup — **not** translation |
| `IngredientAlias` / matcher | Synonym → same canonical ingredient |
| `UnitAliasMap` | NL/EN **unit** words only (`gram`, `el`, …) |
| UI `.resx` | Labels/buttons, not catalog food names |

This curated list feeds the **catalog content**; it does not replace those mechanisms.

## Maintenance checklist

1. Change filters or manual staples in `scripts/generate-curated-ingredients.ps1`.
2. Run the script; review the CSV diff (and spot-check JSON).
3. Restart the app (or run tests): `EnsurePersistenceMigrated` picks up new rows idempotently.
4. Update this doc if the *idea* or pipeline changes.

## Related code

- Domain: `CanonicalIngredient`, `IngredientTranslation`, `IngredientTranslationAlias`
- Localization: `IngredientLanguageFallback`, `IngredientDisplayResolver`
- Matching: `IngredientMatcher`, `IngredientTextNormalizer`
- Seeder: `IngredientCatalogSeeder`
- Existing recipe demo seed (separate): `scripts/seed-lasagna-recipe.sql`
- Tests: `IngredientCatalogSeederTests`, `IngredientCatalogSeedStartupTests`, `IngredientLanguageFallbackTests`
