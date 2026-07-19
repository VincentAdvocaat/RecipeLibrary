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
