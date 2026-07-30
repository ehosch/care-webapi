using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Care.WebApi.Migrators.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftNoteShiftIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ShiftNotes_ShiftId",
                table: "ShiftNotes",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftNotes_ShiftId",
                table: "ShiftNotes");
        }
    }
}
