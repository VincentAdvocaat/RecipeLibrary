# Units model (E13)

How RecipeLibrary understands, displays, and optionally converts ingredient measures.

## Three orthogonal concepts

| Concept | Meaning | Examples |
| --- | --- | --- |
| **Unit** | Concrete unit on a recipe line | Gram, Cup, Tablespoon, Ounce |
| **Dimension** | Physical kind | Mass, Volume, Count |
| **Presentation** | How the user prefers to see measures | Metric / Imperial; NL / EN labels |

**Kitchen measures** (Teaspoon, Tablespoon, Cup) are standardized volume units but are treated as **culinary** units for storage and presentation. They are not silently normalized to milliliters or grams.

## Unit groups

| Group | Dimension | Units (current) | Auto-convert display |
| --- | --- | --- | --- |
| Mass | Mass | Gram, Ounce (weight), Pound | Yes, via metric/imperial preference |
| Volume (standard) | Volume | Milliliter | Later (liter, fluid ounce) |
| Kitchen measure | Volume (culinary treatment) | Teaspoon, Tablespoon, Cup | No; optional convert tool only |
| Count | Count | Piece, Clove, Handful, Slice, Sprig, Leaf, Bunch, Stalk, Can | No |

**Weight ounce ≠ fluid ounce.** `Ounce` is mass. Fluid ounce (volume) is not in the enum yet.

**Storage:** always keep the original quantity + unit on the recipe line. Never rewrite `1 cup` to milliliters or grams in place.

**Mixed recipes** are allowed (e.g. cups + teaspoons + grams on the same recipe).

## Presentation: metric vs imperial

Only **Mass** is reformatted for display (exact Mass → Mass). Kitchen measures and count units are left alone.

| Preference | Mass display | Volume (v1) |
| --- | --- | --- |
| Metric | grams | milliliters unchanged |
| Imperial | ounces / pounds | milliliters unchanged |

Preference is stored in cookie `RecipeLibrary.MeasureSystem` (1 year). When unset, the default is **Metric**.

UI language and measure preference are fully independent: switching English/Dutch does not change g vs oz/lb.

The measure switcher lives next to the ingredients list (recipe detail, create, edit/import review, shopping list). Create/edit unit dropdowns hide non-preferred mass units (oz/lb under Metric, g under Imperial), except when the current row already uses that unit.

## Ingredient unit conversions

There is no global `Cup → Gram` table. Conversions belong **conceptually** to a `CanonicalIngredient`.

### Direction (one-way)

Conversions are **one-way** from culinary measures toward standard physical measures. The inverse is not used for presentation (no `200 g flour → 1.67 cups`).

Allowed for the convert helper:

- Kitchen → Mass (v1)
- Kitchen → Volume (later)
- Mass ↔ Mass / Volume ↔ Volume via Presentation only (no conversion row needed)

### ConversionSource (trusted sources — not AI)

```text
ConversionSource
  Id
  Name    // King Arthur | USDA | Manual
```

AI is a **generator**, not a trusted source.

### Approved conversions (immutable)

```text
IngredientUnitConversion
  CanonicalIngredientId
  FromUnit, ToUnit
  AmountFrom, AmountTo
  ConversionSourceId
  Origin              // Curated | AiAccepted
  ExternalReference?
  Notes?
  CreatedAt
```

Rows are **immutable**: do not update `AmountTo` in place. A better or alternate source value is a **new row** (different source). Unique index: `(CanonicalIngredientId, FromUnit, ToUnit, ConversionSourceId)`.

### AI suggestions (not definitive)

```text
IngredientUnitConversionSuggestion
  CanonicalIngredientId?   // null if unmatched
  IngredientDisplayName
  FromUnit, ToUnit
  AmountFrom, AmountTo
  Status                   // Pending | Accepted | Rejected
  …
```

### Preferred conversion order

1. King Arthur  
2. USDA  
3. Manual + Origin Curated  
4. Manual + Origin AiAccepted  

Pending suggestions are reused before calling AI again; they are not preferred catalog truth.
Convert never auto-promotes an AI proposal to `IngredientUnitConversion` (`AiAccepted`); that origin is reserved for an explicit accept path later.

### Seed vs runtime

Curated rows are seeded from `data/ingredients/unit-conversions.json` (King Arthur for baking staples, USDA elsewhere). Runtime uses only stored conversions and suggestions — **no live USDA/King Arthur API**. AI uses the same `RecipeImport:Ai` API key with a different prompt.

### Convert tool UX

- Default: show the original kitchen measure.
- Show the convert action only when a curated/pending estimate exists or AI fallback is configured.
- Optional “convert” → approximate mass (then apply presentation); AI results stay as `Pending` suggestions.
- Label as an estimate; do not rewrite the recipe line.
- Pending suggestions are unique per ingredient + direction; display names are normalized (trim + lowercase).
- The uniqueness migration normalizes existing display names and keeps the newest Pending row when duplicates exist, then creates the filtered unique indexes.

### Import → editor

When an import draft is applied to the create form, **mass** units are rewritten to the current measure preference (cookie): oz/lb → g under Metric, g → oz/lb under Imperial. Kitchen measures and count units are left as parsed. The stored recipe keeps whatever the user saves from the editor.

## Related

- Ingredient catalog (names/aliases): [ingredient-catalog.md](ingredient-catalog.md)
- Quantity and unit remain on recipe lines (`RecipeIngredients`), not on the catalog identity itself; conversion factors hang off `CanonicalIngredient`.
