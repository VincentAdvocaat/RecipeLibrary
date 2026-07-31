using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShoppingListOwnerUniqueAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListGroups_OwnerUserId",
                table: "ShoppingListGroups",
                column: "OwnerUserId",
                unique: true,
                filter: "[OwnerUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShoppingListGroups_OwnerUserId",
                table: "ShoppingListGroups");
        }
    }
}
