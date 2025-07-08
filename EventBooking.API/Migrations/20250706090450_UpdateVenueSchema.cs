using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVenueSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Events_EventId",
                table: "Bookings");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasStaggeredSeating",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasWheelchairSpaces",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LayoutType",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NumberOfRows",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowSpacing",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeatSpacing",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeatsPerRow",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WheelchairSpaces",
                table: "Venues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Reservations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeatReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatReservations_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_EventId_IsConfirmed_ExpiresAt",
                table: "SeatReservations",
                columns: new[] { "EventId", "IsConfirmed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_EventId_Row_Number",
                table: "SeatReservations",
                columns: new[] { "EventId", "Row", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Events_EventId",
                table: "Bookings",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Events_EventId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "HasStaggeredSeating",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "HasWheelchairSpaces",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "LayoutType",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "NumberOfRows",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "RowSpacing",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "SeatSpacing",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "SeatsPerRow",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "WheelchairSpaces",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Reservations");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Events_EventId",
                table: "Bookings",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
