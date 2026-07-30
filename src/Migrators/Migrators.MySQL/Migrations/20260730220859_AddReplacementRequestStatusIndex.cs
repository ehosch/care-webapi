using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Care.WebApi.Migrators.MySQL.Migrations
{
    /// <inheritdoc />
    public partial class AddReplacementRequestStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ReplacementRequests_Status",
                table: "ReplacementRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReplacementRequests_Status",
                table: "ReplacementRequests");
        }
    }
}
