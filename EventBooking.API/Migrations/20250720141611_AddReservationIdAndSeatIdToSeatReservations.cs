using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationIdAndSeatIdToSeatReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingFoods");

            migrationBuilder.DropTable(
                name: "BookingTickets");

            migrationBuilder.DropTable(
                name: "EventBookings");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_EventId_IsConfirmed_ExpiresAt",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_EventId_Row_Number",
                table: "SeatReservations");

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "SeatReservations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ReservationId",
                table: "SeatReservations",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatId",
                table: "SeatReservations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_EventId_SessionId",
                table: "SeatReservations",
                columns: new[] { "EventId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ExpiresAt_IsConfirmed",
                table: "SeatReservations",
                columns: new[] { "ExpiresAt", "IsConfirmed" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ReservationId",
                table: "SeatReservations",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SeatId",
                table: "SeatReservations",
                column: "SeatId");

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Seats_SeatId",
                table: "SeatReservations",
                column: "SeatId",
                principalTable: "Seats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Seats_SeatId",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_EventId_SessionId",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_ExpiresAt_IsConfirmed",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_ReservationId",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_SeatId",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "SeatId",
                table: "SeatReservations");

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "SeatReservations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "BookingFoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    FoodItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingFoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingFoods_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingFoods_FoodItems_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookingTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    TicketTypeId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    TicketTypeId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingTickets_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingTickets_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingTickets_TicketTypes_TicketTypeId1",
                        column: x => x.TicketTypeId1,
                        principalTable: "TicketTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: true),
                    BuyerEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EventID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrganizerEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PaymentGUID = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SeatNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TicketPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventBookings_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateIndex(
                name: "IX_BookingFoods_BookingId",
                table: "BookingFoods",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingFoods_FoodItemId",
                table: "BookingFoods",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTickets_BookingId",
                table: "BookingTickets",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTickets_TicketTypeId",
                table: "BookingTickets",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingTickets_TicketTypeId1",
                table: "BookingTickets",
                column: "TicketTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_ETicketBookings_PaymentGUID_SeatNo",
                table: "EventBookings",
                columns: new[] { "PaymentGUID", "SeatNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventBookings_BookingId",
                table: "EventBookings",
                column: "BookingId");
        }
    }
}
