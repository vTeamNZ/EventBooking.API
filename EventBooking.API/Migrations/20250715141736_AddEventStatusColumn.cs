using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEventStatusColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Seats_Sections_SectionId",
                table: "Seats");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Sections_SectionId",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Seats_SectionId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Seats");

            migrationBuilder.RenameColumn(
                name: "SectionId",
                table: "Tables",
                newName: "TicketTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Tables_SectionId",
                table: "Tables",
                newName: "IX_Tables_TicketTypeId");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "TicketTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "TicketTypes",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#007bff",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "TicketTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "SeatNumber",
                table: "Seats",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Row",
                table: "Seats",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "TicketTypeId",
                table: "Seats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EventBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeatNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentGUID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BuyerEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrganizerEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventID = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventBookings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_TicketTypeId",
                table: "Seats",
                column: "TicketTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Seats_TicketTypes_TicketTypeId",
                table: "Seats",
                column: "TicketTypeId",
                principalTable: "TicketTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_TicketTypes_TicketTypeId",
                table: "Tables",
                column: "TicketTypeId",
                principalTable: "TicketTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Seats_TicketTypes_TicketTypeId",
                table: "Seats");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_TicketTypes_TicketTypeId",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "EventBookings");

            migrationBuilder.DropIndex(
                name: "IX_Seats_TicketTypeId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "TicketTypes");

            migrationBuilder.DropColumn(
                name: "TicketTypeId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "TicketTypeId",
                table: "Tables",
                newName: "SectionId");

            migrationBuilder.RenameIndex(
                name: "IX_Tables_TicketTypeId",
                table: "Tables",
                newName: "IX_Tables_SectionId");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "TicketTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "TicketTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#007bff");

            migrationBuilder.AlterColumn<string>(
                name: "SeatNumber",
                table: "Seats",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Row",
                table: "Seats",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Seats",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SectionId",
                table: "Seats",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_VenueId",
                table: "Sections",
                column: "VenueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Seats_Sections_SectionId",
                table: "Seats",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Sections_SectionId",
                table: "Tables",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
