using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CinemaShowtimes.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoriums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoriums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                    table.CheckConstraint("CK_Movies_Year", "\"Year\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuditoriumId = table.Column<int>(type: "INTEGER", nullable: false),
                    Row = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.Id);
                    table.CheckConstraint("CK_Seats_Number", "\"Number\" > 0");
                    table.ForeignKey(
                        name: "FK_Seats_Auditoriums_AuditoriumId",
                        column: x => x.AuditoriumId,
                        principalTable: "Auditoriums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Showtimes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuditoriumId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showtimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Showtimes_Auditoriums_AuditoriumId",
                        column: x => x.AuditoriumId,
                        principalTable: "Auditoriums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Showtimes_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Reference = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShowtimeId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.UniqueConstraint("AK_Reservations_Id_ShowtimeId", x => new { x.Id, x.ShowtimeId });
                    table.CheckConstraint("CK_Reservations_ExpiresAt", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_Reservations_Showtimes_ShowtimeId",
                        column: x => x.ShowtimeId,
                        principalTable: "Showtimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShowtimeSeats",
                columns: table => new
                {
                    ShowtimeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeatId = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentReservationId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowtimeSeats", x => new { x.ShowtimeId, x.SeatId });
                    table.ForeignKey(
                        name: "FK_ShowtimeSeats_Reservations_CurrentReservationId_ShowtimeId",
                        columns: x => new { x.CurrentReservationId, x.ShowtimeId },
                        principalTable: "Reservations",
                        principalColumns: new[] { "Id", "ShowtimeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShowtimeSeats_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShowtimeSeats_Showtimes_ShowtimeId",
                        column: x => x.ShowtimeId,
                        principalTable: "Showtimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationSeats",
                columns: table => new
                {
                    ReservationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShowtimeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeatId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationSeats", x => new { x.ReservationId, x.ShowtimeId, x.SeatId });
                    table.ForeignKey(
                        name: "FK_ReservationSeats_Reservations_ReservationId_ShowtimeId",
                        columns: x => new { x.ReservationId, x.ShowtimeId },
                        principalTable: "Reservations",
                        principalColumns: new[] { "Id", "ShowtimeId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationSeats_ShowtimeSeats_ShowtimeId_SeatId",
                        columns: x => new { x.ShowtimeId, x.SeatId },
                        principalTable: "ShowtimeSeats",
                        principalColumns: new[] { "ShowtimeId", "SeatId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Auditoriums",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "Main Hall" });

            migrationBuilder.InsertData(
                table: "Movies",
                columns: new[] { "Id", "Category", "Title", "Year" },
                values: new object[,]
                {
                    { 1, "Adventure", "The Grand Adventure", 2024 },
                    { 2, "Thriller", "Midnight Mystery", 2023 },
                    { 3, "Drama", "Quiet Horizons", 2022 }
                });

            migrationBuilder.InsertData(
                table: "Seats",
                columns: new[] { "Id", "AuditoriumId", "Number", "Row" },
                values: new object[,]
                {
                    { 1, 1, 1, "A" },
                    { 2, 1, 2, "A" },
                    { 3, 1, 3, "A" },
                    { 4, 1, 4, "A" },
                    { 5, 1, 5, "A" },
                    { 6, 1, 6, "A" },
                    { 7, 1, 7, "A" },
                    { 8, 1, 8, "A" },
                    { 9, 1, 9, "A" },
                    { 10, 1, 10, "A" },
                    { 11, 1, 1, "B" },
                    { 12, 1, 2, "B" },
                    { 13, 1, 3, "B" },
                    { 14, 1, 4, "B" },
                    { 15, 1, 5, "B" },
                    { 16, 1, 6, "B" },
                    { 17, 1, 7, "B" },
                    { 18, 1, 8, "B" },
                    { 19, 1, 9, "B" },
                    { 20, 1, 10, "B" },
                    { 21, 1, 1, "C" },
                    { 22, 1, 2, "C" },
                    { 23, 1, 3, "C" },
                    { 24, 1, 4, "C" },
                    { 25, 1, 5, "C" },
                    { 26, 1, 6, "C" },
                    { 27, 1, 7, "C" },
                    { 28, 1, 8, "C" },
                    { 29, 1, 9, "C" },
                    { 30, 1, 10, "C" },
                    { 31, 1, 1, "D" },
                    { 32, 1, 2, "D" },
                    { 33, 1, 3, "D" },
                    { 34, 1, 4, "D" },
                    { 35, 1, 5, "D" },
                    { 36, 1, 6, "D" },
                    { 37, 1, 7, "D" },
                    { 38, 1, 8, "D" },
                    { 39, 1, 9, "D" },
                    { 40, 1, 10, "D" },
                    { 41, 1, 1, "E" },
                    { 42, 1, 2, "E" },
                    { 43, 1, 3, "E" },
                    { 44, 1, 4, "E" },
                    { 45, 1, 5, "E" },
                    { 46, 1, 6, "E" },
                    { 47, 1, 7, "E" },
                    { 48, 1, 8, "E" },
                    { 49, 1, 9, "E" },
                    { 50, 1, 10, "E" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Reference",
                table: "Reservations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ShowtimeId_Status_ExpiresAt",
                table: "Reservations",
                columns: new[] { "ShowtimeId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationSeats_ShowtimeId_SeatId",
                table: "ReservationSeats",
                columns: new[] { "ShowtimeId", "SeatId" });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_AuditoriumId_Row_Number",
                table: "Seats",
                columns: new[] { "AuditoriumId", "Row", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_AuditoriumId_StartTime",
                table: "Showtimes",
                columns: new[] { "AuditoriumId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Showtimes_MovieId_StartTime",
                table: "Showtimes",
                columns: new[] { "MovieId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ShowtimeSeats_CurrentReservationId_ShowtimeId",
                table: "ShowtimeSeats",
                columns: new[] { "CurrentReservationId", "ShowtimeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShowtimeSeats_SeatId",
                table: "ShowtimeSeats",
                column: "SeatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationSeats");

            migrationBuilder.DropTable(
                name: "ShowtimeSeats");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "Showtimes");

            migrationBuilder.DropTable(
                name: "Auditoriums");

            migrationBuilder.DropTable(
                name: "Movies");
        }
    }
}
