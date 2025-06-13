using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class FixReservationModelAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Seats_SeatId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_SeatId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "SeatId",
                table: "Reservations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Reservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeatId",
                table: "Reservations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_SeatId",
                table: "Reservations",
                column: "SeatId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Seats_SeatId",
                table: "Reservations",
                column: "SeatId",
                principalTable: "Seats",
                principalColumn: "Id");
        }
    }
}
