using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaturalStoneImpex.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVisualizationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisualizationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisualizationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisualizationRequests_CreatedAt",
                table: "VisualizationRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VisualizationRequests_IpHash_CreatedAt",
                table: "VisualizationRequests",
                columns: new[] { "IpHash", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisualizationRequests");
        }
    }
}
