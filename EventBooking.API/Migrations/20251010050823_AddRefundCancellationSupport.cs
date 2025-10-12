using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundCancellationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "OrganizerTicketPayments");

            migrationBuilder.RenameColumn(
                name: "UpdatedByUserId",
                table: "OrganizerTicketPayments",
                newName: "RefundedBy");

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "OrganizerTicketPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundedBy",
                table: "Bookings",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "BookingLineItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundedBy",
                table: "BookingLineItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_BookingLineItemId_Status",
                table: "OrganizerTicketPayments",
                columns: new[] { "BookingLineItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_RefundedAt",
                table: "OrganizerTicketPayments",
                column: "RefundedAt",
                filter: "RefundedAt IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_RefundedBy",
                table: "OrganizerTicketPayments",
                column: "RefundedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerTicketPayments_Status_EventId",
                table: "OrganizerTicketPayments",
                columns: new[] { "Status", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RefundedAt",
                table: "Bookings",
                column: "RefundedAt",
                filter: "RefundedAt IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RefundedBy",
                table: "Bookings",
                column: "RefundedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_EventId",
                table: "Bookings",
                columns: new[] { "Status", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingLineItems_RefundedAt",
                table: "BookingLineItems",
                column: "RefundedAt",
                filter: "RefundedAt IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BookingLineItems_RefundedBy",
                table: "BookingLineItems",
                column: "RefundedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BookingLineItems_Status_ItemType",
                table: "BookingLineItems",
                columns: new[] { "Status", "ItemType" });

            migrationBuilder.AddForeignKey(
                name: "FK_BookingLineItems_AspNetUsers_RefundedBy",
                table: "BookingLineItems",
                column: "RefundedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_AspNetUsers_RefundedBy",
                table: "Bookings",
                column: "RefundedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizerTicketPayments_AspNetUsers_RefundedBy",
                table: "OrganizerTicketPayments",
                column: "RefundedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingLineItems_AspNetUsers_RefundedBy",
                table: "BookingLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_AspNetUsers_RefundedBy",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizerTicketPayments_AspNetUsers_RefundedBy",
                table: "OrganizerTicketPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrganizerTicketPayments_BookingLineItemId_Status",
                table: "OrganizerTicketPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrganizerTicketPayments_RefundedAt",
                table: "OrganizerTicketPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrganizerTicketPayments_RefundedBy",
                table: "OrganizerTicketPayments");

            migrationBuilder.DropIndex(
                name: "IX_OrganizerTicketPayments_Status_EventId",
                table: "OrganizerTicketPayments");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RefundedAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RefundedBy",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status_EventId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_BookingLineItems_RefundedAt",
                table: "BookingLineItems");

            migrationBuilder.DropIndex(
                name: "IX_BookingLineItems_RefundedBy",
                table: "BookingLineItems");

            migrationBuilder.DropIndex(
                name: "IX_BookingLineItems_Status_ItemType",
                table: "BookingLineItems");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "OrganizerTicketPayments");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RefundedBy",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "BookingLineItems");

            migrationBuilder.DropColumn(
                name: "RefundedBy",
                table: "BookingLineItems");

            migrationBuilder.RenameColumn(
                name: "RefundedBy",
                table: "OrganizerTicketPayments",
                newName: "UpdatedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "OrganizerTicketPayments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }
    }
}
