using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizerTicketPaymentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStanding",
                table: "TicketTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StandingCapacity",
                table: "TicketTypes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizerTicketPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingLineItemId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    TicketTypeId = table.Column<int>(type: "int", nullable: false),
                    CustomerFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CustomerMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SeatDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TicketPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsPaidToOrganizer = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizerTicketPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizerTicketPayments_BookingLineItems_BookingLineItemId",
                        column: x => x.BookingLineItemId,
                        principalTable: "BookingLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizerTicketPayments_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrganizerTicketPayments_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_BookingLineItemId",
                table: "OrganizerTicketPayments",
                column: "BookingLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_CustomerEmail",
                table: "OrganizerTicketPayments",
                column: "CustomerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_EventId",
                table: "OrganizerTicketPayments",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_EventId_IsPaidToOrganizer",
                table: "OrganizerTicketPayments",
                columns: new[] { "EventId", "IsPaidToOrganizer" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_IsPaidToOrganizer",
                table: "OrganizerTicketPayments",
                column: "IsPaidToOrganizer");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_TicketTypeId",
                table: "OrganizerTicketPayments",
                column: "TicketTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizerTicketPayments");

            migrationBuilder.DropColumn(
                name: "IsStanding",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "StandingCapacity",
                table: "TicketTypes");
        }
    }
}
