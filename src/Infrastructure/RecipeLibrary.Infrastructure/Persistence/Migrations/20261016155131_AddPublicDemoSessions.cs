using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicDemoSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DemoOwnerUserId",
                table: "ContentModerationEvents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DemoSessions",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SeedVersion = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ResetCount = table.Column<int>(type: "int", nullable: false),
                    SaveAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoSessions", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_DemoSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemoStartQuotas",
                columns: table => new
                {
                    AddressHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WindowStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Starts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoStartQuotas", x => x.AddressHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentModerationEvents_DemoOwnerUserId",
                table: "ContentModerationEvents",
                column: "DemoOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DemoSessions_ExpiresAt",
                table: "DemoSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DemoStartQuotas_WindowStart",
                table: "DemoStartQuotas",
                column: "WindowStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoSessions");

            migrationBuilder.DropTable(
                name: "DemoStartQuotas");

            migrationBuilder.DropIndex(
                name: "IX_ContentModerationEvents_DemoOwnerUserId",
                table: "ContentModerationEvents");

            migrationBuilder.DropColumn(
                name: "DemoOwnerUserId",
                table: "ContentModerationEvents");
        }
    }
}
