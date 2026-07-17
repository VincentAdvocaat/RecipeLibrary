using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultilingualIngredientTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientAliases");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_NormalizedName",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CanonicalName",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Ingredients");

            migrationBuilder.AddColumn<string>(
                name: "CatalogKey",
                table: "Ingredients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IngredientTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientTranslations_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientTranslationAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientTranslationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedAlias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientTranslationAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientTranslationAliases_IngredientTranslations_IngredientTranslationId",
                        column: x => x.IngredientTranslationId,
                        principalTable: "IngredientTranslations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_CatalogKey",
                table: "Ingredients",
                column: "CatalogKey",
                unique: true,
                filter: "[CatalogKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientTranslationAliases_IngredientTranslationId",
                table: "IngredientTranslationAliases",
                column: "IngredientTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientTranslationAliases_NormalizedAlias",
                table: "IngredientTranslationAliases",
                column: "NormalizedAlias");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientTranslations_IngredientId_LanguageCode",
                table: "IngredientTranslations",
                columns: new[] { "IngredientId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientTranslations_LanguageCode_NormalizedDisplayName",
                table: "IngredientTranslations",
                columns: new[] { "LanguageCode", "NormalizedDisplayName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientTranslationAliases");

            migrationBuilder.DropTable(
                name: "IngredientTranslations");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_CatalogKey",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CatalogKey",
                table: "Ingredients");

            migrationBuilder.AddColumn<string>(
                name: "CanonicalName",
                table: "Ingredients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Ingredients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "IngredientAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedAlias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientAliases_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_NormalizedName",
                table: "Ingredients",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientAliases_IngredientId",
                table: "IngredientAliases",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientAliases_NormalizedAlias",
                table: "IngredientAliases",
                column: "NormalizedAlias",
                unique: true);
        }
    }
}
