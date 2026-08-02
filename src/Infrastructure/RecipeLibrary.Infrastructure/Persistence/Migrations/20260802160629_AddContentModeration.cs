using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModeratedAt",
                table: "Recipes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                table: "Recipes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModerationSummary",
                table: "Recipes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentModerationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CategoriesSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentModerationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Handled = table.Column<bool>(type: "bit", nullable: false),
                    HandledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ModerationStatus",
                table: "Recipes",
                column: "ModerationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ContentModerationEvents_CreatedAt",
                table: "ContentModerationEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentModerationEvents_RecipeId",
                table: "ContentModerationEvents",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_Handled_CreatedAt",
                table: "ContentReports",
                columns: new[] { "Handled", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_RecipeId",
                table: "ContentReports",
                column: "RecipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentModerationEvents");

            migrationBuilder.DropTable(
                name: "ContentReports");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_ModerationStatus",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ModerationSummary",
                table: "Recipes");
        }
    }
}
