using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaturalStoneImpex.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVisualizerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVisualizerEnabled",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TextureImagePath",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TextureWidthMeters",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 1.00m);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsVisualizerEnabled",
                table: "Products",
                column: "IsVisualizerEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsVisualizerEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsVisualizerEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TextureImagePath",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TextureWidthMeters",
                table: "Products");
        }
    }
}
