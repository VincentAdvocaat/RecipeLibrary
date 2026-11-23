using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicShareKey",
                table: "Recipes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OwnerUserId_SharedFromRecipeId",
                table: "Recipes",
                columns: new[] { "OwnerUserId", "SharedFromRecipeId" },
                unique: true,
                filter: "[HouseholdId] IS NULL AND [SharedFromRecipeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_PublicShareKey",
                table: "Recipes",
                column: "PublicShareKey",
                unique: true,
                filter: "[PublicShareKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_OwnerUserId_SharedFromRecipeId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_PublicShareKey",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PublicShareKey",
                table: "Recipes");
        }
    }
}
