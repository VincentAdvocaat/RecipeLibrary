using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalIngredientUserGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UserGenerated",
                table: "Ingredients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing FindOrCreate rows have no CatalogKey; treat them as user-generated.
            migrationBuilder.Sql(
                """
                UPDATE Ingredients
                SET UserGenerated = 1
                WHERE CatalogKey IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserGenerated",
                table: "Ingredients");
        }
    }
}
