using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientNormalizationAndTagging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Recipes_RecipeId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_RecipeId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "RecipeId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Ingredients");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Ingredients",
                newName: "NormalizedName");

            migrationBuilder.AddColumn<string>(
                name: "CanonicalName",
                table: "Ingredients",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Ingredients",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

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

            migrationBuilder.CreateTable(
                name: "IngredientMatchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Input = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedInput = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MatchedIngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientMatchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientMatchLogs_Ingredients_MatchedIngredientId",
                        column: x => x.MatchedIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Preparation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientTags",
                columns: table => new
                {
                    IngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientTags", x => new { x.IngredientId, x.TagId });
                    table.ForeignKey(
                        name: "FK_IngredientTags_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
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

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMatchLogs_MatchedIngredientId",
                table: "IngredientMatchLogs",
                column: "MatchedIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientTags_TagId",
                table: "IngredientTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_IngredientId",
                table: "RecipeIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientAliases");

            migrationBuilder.DropTable(
                name: "IngredientMatchLogs");

            migrationBuilder.DropTable(
                name: "IngredientTags");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_NormalizedName",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CanonicalName",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Ingredients");

            migrationBuilder.RenameColumn(
                name: "NormalizedName",
                table: "Ingredients",
                newName: "Name");

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "Ingredients",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipeId",
                table: "Ingredients",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Ingredients",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_RecipeId",
                table: "Ingredients",
                column: "RecipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Recipes_RecipeId",
                table: "Ingredients",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
