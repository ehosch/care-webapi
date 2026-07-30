using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Care.WebApi.Migrators.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Date_ShiftType",
                table: "Shifts",
                columns: new[] { "Date", "ShiftType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shifts_Date_ShiftType",
                table: "Shifts");
        }
    }
}
