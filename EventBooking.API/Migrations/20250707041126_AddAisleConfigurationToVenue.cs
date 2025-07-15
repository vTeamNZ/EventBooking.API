using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAisleConfigurationToVenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AisleWidth",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasHorizontalAisles",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasVerticalAisles",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HorizontalAisleRows",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VerticalAisleSeats",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AisleWidth",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "HasHorizontalAisles",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "HasVerticalAisles",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "HorizontalAisleRows",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "VerticalAisleSeats",
                table: "Venues");
        }
    }
}
