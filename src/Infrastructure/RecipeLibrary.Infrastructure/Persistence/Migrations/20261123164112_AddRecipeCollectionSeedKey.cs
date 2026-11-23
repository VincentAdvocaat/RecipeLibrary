using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeCollectionSeedKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionSeedKey",
                table: "Recipes",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OwnerUserId_CollectionSeedKey",
                table: "Recipes",
                columns: new[] { "OwnerUserId", "CollectionSeedKey" },
                unique: true,
                filter: "[HouseholdId] IS NULL AND [CollectionSeedKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_OwnerUserId_CollectionSeedKey",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "CollectionSeedKey",
                table: "Recipes");
        }
    }
}
