using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingConversionSuggestionUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align stored names with runtime NormalizeDisplayName (trim + lowercase)
            // so unique indexes and lookups stay consistent under any collation.
            migrationBuilder.Sql(
                """
                UPDATE [IngredientUnitConversionSuggestions]
                SET [IngredientDisplayName] = LOWER(LTRIM(RTRIM([IngredientDisplayName])));
                """);

            // Keep the newest Pending row per matched ingredient + direction.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [CanonicalIngredientId], [FromUnit], [ToUnit]
                               ORDER BY [CreatedAt] DESC
                           ) AS [rn]
                    FROM [IngredientUnitConversionSuggestions]
                    WHERE [Status] = N'Pending'
                      AND [CanonicalIngredientId] IS NOT NULL
                )
                DELETE FROM [IngredientUnitConversionSuggestions]
                WHERE [Id] IN (SELECT [Id] FROM ranked WHERE [rn] > 1);
                """);

            // Keep the newest Pending row per unmatched display name + direction.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [IngredientDisplayName], [FromUnit], [ToUnit]
                               ORDER BY [CreatedAt] DESC
                           ) AS [rn]
                    FROM [IngredientUnitConversionSuggestions]
                    WHERE [Status] = N'Pending'
                      AND [CanonicalIngredientId] IS NULL
                )
                DELETE FROM [IngredientUnitConversionSuggestions]
                WHERE [Id] IN (SELECT [Id] FROM ranked WHERE [rn] > 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversionSuggestions_CanonicalIngredientId_FromUnit_ToUnit",
                table: "IngredientUnitConversionSuggestions",
                columns: new[] { "CanonicalIngredientId", "FromUnit", "ToUnit" },
                unique: true,
                filter: "[Status] = N'Pending' AND [CanonicalIngredientId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversionSuggestions_IngredientDisplayName_FromUnit_ToUnit",
                table: "IngredientUnitConversionSuggestions",
                columns: new[] { "IngredientDisplayName", "FromUnit", "ToUnit" },
                unique: true,
                filter: "[Status] = N'Pending' AND [CanonicalIngredientId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IngredientUnitConversionSuggestions_CanonicalIngredientId_FromUnit_ToUnit",
                table: "IngredientUnitConversionSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_IngredientUnitConversionSuggestions_IngredientDisplayName_FromUnit_ToUnit",
                table: "IngredientUnitConversionSuggestions");
        }
    }
}
