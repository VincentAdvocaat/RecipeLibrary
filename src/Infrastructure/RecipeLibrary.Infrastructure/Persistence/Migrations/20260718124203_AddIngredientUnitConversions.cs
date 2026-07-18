using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientUnitConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversionSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversionSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientUnitConversionSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IngredientDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AmountFrom = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AmountTo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientUnitConversionSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversionSuggestions_Ingredients_CanonicalIngredientId",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IngredientUnitConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalIngredientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToUnit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AmountFrom = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AmountTo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ConversionSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientUnitConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversions_ConversionSources_ConversionSourceId",
                        column: x => x.ConversionSourceId,
                        principalTable: "ConversionSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientUnitConversions_Ingredients_CanonicalIngredientId",
                        column: x => x.CanonicalIngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversionSources_Name",
                table: "ConversionSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversions_CanonicalIngredientId_FromUnit_ToUnit_ConversionSourceId",
                table: "IngredientUnitConversions",
                columns: new[] { "CanonicalIngredientId", "FromUnit", "ToUnit", "ConversionSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversions_ConversionSourceId",
                table: "IngredientUnitConversions",
                column: "ConversionSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientUnitConversionSuggestions_CanonicalIngredientId_FromUnit_ToUnit_Status",
                table: "IngredientUnitConversionSuggestions",
                columns: new[] { "CanonicalIngredientId", "FromUnit", "ToUnit", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientUnitConversions");

            migrationBuilder.DropTable(
                name: "IngredientUnitConversionSuggestions");

            migrationBuilder.DropTable(
                name: "ConversionSources");
        }
    }
}
