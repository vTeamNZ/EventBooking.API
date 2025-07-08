using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportedSeatSelectionModeToVenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupportedSeatSelectionMode",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportedSeatSelectionMode",
                table: "Venues");
        }
    }
}
