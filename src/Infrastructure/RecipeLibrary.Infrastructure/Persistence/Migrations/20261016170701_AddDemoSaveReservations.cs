using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeLibrary.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoSaveReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoSaveAttempts",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CompletedRecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoSaveAttempts", x => x.OperationId);
                    table.ForeignKey(
                        name: "FK_DemoSaveAttempts_DemoSessions_UserId",
                        column: x => x.UserId,
                        principalTable: "DemoSessions",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemoSaveAttempts_UserId",
                table: "DemoSaveAttempts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoSaveAttempts");
        }
    }
}
