using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaShowtimes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateShowtimeStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Showtimes_AuditoriumId_StartTime",
                table: "Showtimes");

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_AuditoriumId_StartTime",
                table: "Showtimes",
                columns: new[] { "AuditoriumId", "StartTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Showtimes_AuditoriumId_StartTime",
                table: "Showtimes");

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_AuditoriumId_StartTime",
                table: "Showtimes",
                columns: new[] { "AuditoriumId", "StartTime" });
        }
    }
}
