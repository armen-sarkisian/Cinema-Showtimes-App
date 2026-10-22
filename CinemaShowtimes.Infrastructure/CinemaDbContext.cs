using CinemaShowtimes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CinemaShowtimes.Infrastructure;

public sealed class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, long> UtcDateTimeOffsetConverter = new(
        value => value.UtcDateTime.Ticks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Auditorium> Auditoriums => Set<Auditorium>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationSeat> ReservationSeats => Set<ReservationSeat>();
    public DbSet<ShowtimeSeat> ShowtimeSeats => Set<ShowtimeSeat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.Property(movie => movie.Title).HasMaxLength(200).IsRequired();
            entity.Property(movie => movie.Category).HasMaxLength(100).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_Movies_Year", "\"Year\" > 0"));
        });

        modelBuilder.Entity<Auditorium>(entity =>
        {
            entity.Property(auditorium => auditorium.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.Property(seat => seat.Row).HasMaxLength(10).IsRequired();
            entity.HasIndex(seat => new { seat.AuditoriumId, seat.Row, seat.Number }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("CK_Seats_Number", "\"Number\" > 0"));
            entity.HasOne(seat => seat.Auditorium)
                .WithMany(auditorium => auditorium.Seats)
                .HasForeignKey(seat => seat.AuditoriumId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Showtime>(entity =>
        {
            entity.Property(showtime => showtime.StartTime).HasConversion(UtcDateTimeOffsetConverter);
            entity.HasIndex(showtime => new { showtime.MovieId, showtime.StartTime });
            entity.HasIndex(showtime => new { showtime.AuditoriumId, showtime.StartTime }).IsUnique();
            entity.HasOne(showtime => showtime.Movie)
                .WithMany(movie => movie.Showtimes)
                .HasForeignKey(showtime => showtime.MovieId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(showtime => showtime.Auditorium)
                .WithMany(auditorium => auditorium.Showtimes)
                .HasForeignKey(showtime => showtime.AuditoriumId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasAlternateKey(reservation => new { reservation.Id, reservation.ShowtimeId });
            entity.HasIndex(reservation => reservation.Reference).IsUnique();
            entity.HasIndex(reservation => new { reservation.ShowtimeId, reservation.Status, reservation.ExpiresAt });
            entity.Property(reservation => reservation.CreatedAt).HasConversion(UtcDateTimeOffsetConverter);
            entity.Property(reservation => reservation.ExpiresAt).HasConversion(UtcDateTimeOffsetConverter);
            entity.Property(reservation => reservation.Status).HasConversion<int>();
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Reservations_ExpiresAt",
                "\"ExpiresAt\" > \"CreatedAt\""));
            entity.HasOne(reservation => reservation.Showtime)
                .WithMany()
                .HasForeignKey(reservation => reservation.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShowtimeSeat>(entity =>
        {
            entity.HasKey(showtimeSeat => new { showtimeSeat.ShowtimeId, showtimeSeat.SeatId });
            entity.HasOne(showtimeSeat => showtimeSeat.Showtime)
                .WithMany(showtime => showtime.Seats)
                .HasForeignKey(showtimeSeat => showtimeSeat.ShowtimeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(showtimeSeat => showtimeSeat.Seat)
                .WithMany(seat => seat.ShowtimeSeats)
                .HasForeignKey(showtimeSeat => showtimeSeat.SeatId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(showtimeSeat => showtimeSeat.CurrentReservation)
                .WithMany(reservation => reservation.CurrentSeats)
                .HasForeignKey(showtimeSeat => new
                {
                    showtimeSeat.CurrentReservationId,
                    showtimeSeat.ShowtimeId
                })
                .HasPrincipalKey(reservation => new { reservation.Id, reservation.ShowtimeId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationSeat>(entity =>
        {
            entity.HasKey(reservationSeat => new
            {
                reservationSeat.ReservationId,
                reservationSeat.ShowtimeId,
                reservationSeat.SeatId
            });
            entity.HasOne(reservationSeat => reservationSeat.Reservation)
                .WithMany(reservation => reservation.Seats)
                .HasForeignKey(reservationSeat => new
                {
                    reservationSeat.ReservationId,
                    reservationSeat.ShowtimeId
                })
                .HasPrincipalKey(reservation => new { reservation.Id, reservation.ShowtimeId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(reservationSeat => reservationSeat.ShowtimeSeat)
                .WithMany(showtimeSeat => showtimeSeat.ReservationHistory)
                .HasForeignKey(reservationSeat => new
                {
                    reservationSeat.ShowtimeId,
                    reservationSeat.SeatId
                })
                .OnDelete(DeleteBehavior.Restrict);
        });

        SeedInitialData(modelBuilder);
    }

    private static void SeedInitialData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>().HasData(
            new { Id = 1, Title = "The Grand Adventure", Category = "Adventure", Year = 2024 },
            new { Id = 2, Title = "Midnight Mystery", Category = "Thriller", Year = 2023 },
            new { Id = 3, Title = "Quiet Horizons", Category = "Drama", Year = 2022 });

        modelBuilder.Entity<Auditorium>().HasData(new { Id = 1, Name = "Main Hall" });

        var rows = new[] { "A", "B", "C", "D", "E" };
        var seats = rows
            .SelectMany((row, rowIndex) => Enumerable.Range(1, 10)
                .Select(number => new
                {
                    Id = rowIndex * 10 + number,
                    AuditoriumId = 1,
                    Row = row,
                    Number = number
                }))
            .ToArray();

        modelBuilder.Entity<Seat>().HasData(seats);
    }
}
