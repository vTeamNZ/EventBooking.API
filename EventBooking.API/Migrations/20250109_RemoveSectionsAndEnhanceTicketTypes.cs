using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSectionsAndEnhanceTicketTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new columns to TicketTypes table
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "TicketTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Step 2: Migrate existing data - Copy Type to Name
            migrationBuilder.Sql("UPDATE TicketTypes SET Name = Type");

            // Step 3: Add TicketTypeId to Seats table
            migrationBuilder.AddColumn<int>(
                name: "TicketTypeId",
                table: "Seats",
                type: "int",
                nullable: true);

            // Step 4: Migrate Section data to TicketTypes
            // Create a TicketType for each Section and assign seats accordingly
            migrationBuilder.Sql(@"
                INSERT INTO TicketTypes (Type, Name, Price, Color, EventId, Description)
                SELECT 
                    s.Name,
                    s.Name,
                    s.BasePrice,
                    s.Color,
                    e.Id as EventId,
                    'Migrated from Section: ' + s.Name
                FROM Sections s
                CROSS JOIN Events e
                WHERE EXISTS (
                    SELECT 1 FROM Seats seat 
                    WHERE seat.SectionId = s.Id AND seat.EventId = e.Id
                )
            ");

            // Step 5: Update Seats to reference TicketTypes instead of Sections
            migrationBuilder.Sql(@"
                UPDATE seat
                SET seat.TicketTypeId = tt.Id
                FROM Seats seat
                INNER JOIN Sections s ON seat.SectionId = s.Id
                INNER JOIN TicketTypes tt ON tt.Name = s.Name AND tt.EventId = seat.EventId
                WHERE seat.SectionId IS NOT NULL
            ");

            // Step 6: Drop foreign key constraints for Sections
            migrationBuilder.DropForeignKey(
                name: "FK_Seats_Sections_SectionId",
                table: "Seats");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Sections_SectionId",
                table: "Tables");

            // Step 7: Drop Section columns
            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Tables");

            // Step 8: Drop Sections table
            migrationBuilder.DropTable(
                name: "Sections");

            // Step 9: Add foreign key for TicketTypeId
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
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse migration - recreate Sections
            migrationBuilder.DropForeignKey(
                name: "FK_Seats_TicketTypes_TicketTypeId",
                table: "Seats");

            migrationBuilder.DropIndex(
                name: "IX_Seats_TicketTypeId",
                table: "Seats");

            // Recreate Sections table
            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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

            // Add back Section columns
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Seats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "Tables",
                type: "int",
                nullable: true);

            // Remove TicketTypeId and Name columns
            migrationBuilder.DropColumn(
                name: "TicketTypeId",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "TicketTypes");

            // Recreate foreign keys
            migrationBuilder.CreateIndex(
                name: "IX_Seats_SectionId",
                table: "Seats",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_SectionId",
                table: "Tables",
                column: "SectionId");

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
