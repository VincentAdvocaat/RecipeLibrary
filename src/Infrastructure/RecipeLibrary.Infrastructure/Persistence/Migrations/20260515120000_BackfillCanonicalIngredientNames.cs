using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class BackfillCanonicalIngredientNames : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE Ingredients
            SET CanonicalName = NormalizedName
            WHERE CanonicalName IS NULL OR LTRIM(RTRIM(CanonicalName)) = '';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data backfill is not reversed.
    }
}
