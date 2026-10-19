using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Domain.Enums;
using CinemaShowtimes.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Application;

public sealed class CinemaApplicationService(CinemaDbContext dbContext, TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo CinemaTimeZone = ResolveCinemaTimeZone();

    public async Task<MovieResponse> CreateMovieAsync(
        CreateMovieRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var category = request.Category.Trim();
        if (title.Length == 0 || category.Length == 0)
        {
            throw CinemaApplicationException.Invalid(
                "Title and category must contain at least one non-whitespace character.");
        }

        var currentYear = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), CinemaTimeZone).Year;
        if (request.Year < 1930 || request.Year > currentYear)
        {
            throw CinemaApplicationException.Invalid($"Year must be between 1930 and {currentYear}.");
        }

        var movie = new Movie
        {
            Title = title,
            Category = category,
            Year = request.Year
        };

        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MovieResponse(movie.Id, movie.Title, movie.Category, movie.Year);
    }

    public async Task<ShowtimeResponse> CreateShowtimeAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartTime == default || request.StartTime.Kind != DateTimeKind.Unspecified)
        {
            throw CinemaApplicationException.Invalid(
                "StartTime must be a valid local date and time in the cinema time zone, without a UTC offset.");
        }

        if (!await dbContext.Movies.AnyAsync(movie => movie.Id == request.MovieId, cancellationToken))
        {
            throw CinemaApplicationException.NotFound($"Movie with ID {request.MovieId} was not found.");
        }

        if (!await dbContext.Auditoriums.AnyAsync(
                auditorium => auditorium.Id == request.AuditoriumId,
                cancellationToken))
        {
            throw CinemaApplicationException.NotFound(
                $"Auditorium with ID {request.AuditoriumId} was not found.");
        }

        var localStartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Unspecified);
        if (CinemaTimeZone.IsInvalidTime(localStartTime) || CinemaTimeZone.IsAmbiguousTime(localStartTime))
        {
            throw CinemaApplicationException.Invalid(
                "StartTime does not identify a unique valid local time in the cinema time zone.");
        }

        var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localStartTime, CinemaTimeZone);
        if (startTimeUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            throw CinemaApplicationException.Invalid("StartTime must be in the future.");
        }

        var showtime = new Showtime
        {
            MovieId = request.MovieId,
            AuditoriumId = request.AuditoriumId,
            StartTime = new DateTimeOffset(startTimeUtc, TimeSpan.Zero)
        };

        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ShowtimeResponse(
            showtime.Id,
            showtime.MovieId,
            showtime.AuditoriumId,
            TimeZoneInfo.ConvertTime(showtime.StartTime, CinemaTimeZone).DateTime);
    }

    public async Task<ReservationResponse> CreateReservationAsync(
        int showtimeId,
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SeatIds is null || request.SeatIds.Count == 0)
        {
            throw CinemaApplicationException.Invalid("At least one seat must be selected.");
        }

        if (request.SeatIds.Any(seatId => seatId <= 0))
        {
            throw CinemaApplicationException.Invalid("Seat IDs must be positive integers.");
        }

        if (request.SeatIds.Distinct().Count() != request.SeatIds.Count)
        {
            throw CinemaApplicationException.Invalid("Seat IDs must not contain duplicates.");
        }

        var connection = dbContext.Database.GetDbConnection() as SqliteConnection
            ?? throw new InvalidOperationException("Reservations require a SQLite database.");
        await connection.OpenAsync(cancellationToken);
        await using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await dbContext.Database.UseTransactionAsync(
            sqliteTransaction,
            cancellationToken)
            ?? throw new InvalidOperationException("Could not enlist the SQLite reservation transaction.");
        var now = timeProvider.GetUtcNow().ToUniversalTime();

        var showtime = await dbContext.Showtimes
            .Include(item => item.Movie)
            .Include(item => item.Auditorium)
            .SingleOrDefaultAsync(item => item.Id == showtimeId, cancellationToken);
        if (showtime is null)
        {
            throw CinemaApplicationException.NotFound($"Showtime with ID {showtimeId} was not found.");
        }

        var requestedSeats = await dbContext.Seats
            .Where(seat => request.SeatIds.Contains(seat.Id)
                && seat.AuditoriumId == showtime.AuditoriumId)
            .ToListAsync(cancellationToken);
        if (requestedSeats.Count != request.SeatIds.Count)
        {
            throw CinemaApplicationException.Invalid(
                "Every seat must exist and belong to the showtime's auditorium.");
        }

        var showtimeSeats = await dbContext.ShowtimeSeats
            .Include(item => item.CurrentReservation)
            .Where(item => item.ShowtimeId == showtimeId && request.SeatIds.Contains(item.SeatId))
            .ToListAsync(cancellationToken);

        var showtimeSeatIds = showtimeSeats.Select(item => item.SeatId).ToHashSet();
        var missingShowtimeSeats = request.SeatIds
            .Where(seatId => !showtimeSeatIds.Contains(seatId))
            .Select(seatId => new ShowtimeSeat { ShowtimeId = showtimeId, SeatId = seatId })
            .ToArray();
        if (missingShowtimeSeats.Length > 0)
        {
            dbContext.ShowtimeSeats.AddRange(missingShowtimeSeats);
            showtimeSeats.AddRange(missingShowtimeSeats);
        }

        foreach (var seat in showtimeSeats)
        {
            var currentReservation = seat.CurrentReservation;
            if (currentReservation?.Status == ReservationStatus.Confirmed)
            {
                throw CinemaApplicationException.Conflict(
                    $"Seat {seat.SeatId} has already been sold for this showtime.");
            }

            if (currentReservation?.Status == ReservationStatus.Reserved
                && now < currentReservation.ExpiresAt)
            {
                throw CinemaApplicationException.Conflict(
                    $"Seat {seat.SeatId} is currently reserved.");
            }
        }

        var reservation = new Reservation
        {
            Reference = Guid.NewGuid(),
            ShowtimeId = showtimeId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(10),
            Status = ReservationStatus.Reserved
        };
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var showtimeSeat in showtimeSeats)
        {
            showtimeSeat.CurrentReservation = reservation;
            dbContext.ReservationSeats.Add(new ReservationSeat
            {
                ReservationId = reservation.Id,
                ShowtimeId = showtimeId,
                SeatId = showtimeSeat.SeatId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReservationResponse(
            reservation.Reference,
            request.SeatIds.Count,
            new MovieSummaryResponse(
                showtime.Movie.Id,
                showtime.Movie.Title,
                showtime.Movie.Category,
                showtime.Movie.Year),
            new AuditoriumSummaryResponse(showtime.Auditorium.Id, showtime.Auditorium.Name),
            reservation.CreatedAt,
            reservation.ExpiresAt);
    }

    public async Task<ReservationConfirmationResponse> ConfirmReservationAsync(
        Guid reference,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection() as SqliteConnection
            ?? throw new InvalidOperationException("Reservation confirmation requires a SQLite database.");
        await connection.OpenAsync(cancellationToken);
        await using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await dbContext.Database.UseTransactionAsync(
            sqliteTransaction,
            cancellationToken)
            ?? throw new InvalidOperationException("Could not enlist the SQLite confirmation transaction.");
        var now = timeProvider.GetUtcNow().ToUniversalTime();

        var reservation = await dbContext.Reservations
            .Include(item => item.Seats)
            .SingleOrDefaultAsync(item => item.Reference == reference, cancellationToken);
        if (reservation is null)
        {
            throw CinemaApplicationException.NotFound($"Reservation {reference} was not found.");
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            throw CinemaApplicationException.Conflict(
                "Only a reservation with Reserved status can be confirmed.");
        }

        if (now >= reservation.ExpiresAt)
        {
            throw CinemaApplicationException.Conflict("The reservation has expired.");
        }

        var currentSeats = await dbContext.ShowtimeSeats
            .Where(item => item.ShowtimeId == reservation.ShowtimeId
                && item.CurrentReservationId == reservation.Id)
            .ToListAsync(cancellationToken);
        var historicalSeatIds = reservation.Seats.Select(item => item.SeatId).ToHashSet();
        if (currentSeats.Count == 0
            || currentSeats.Count != historicalSeatIds.Count
            || currentSeats.Any(item => !historicalSeatIds.Contains(item.SeatId)))
        {
            throw CinemaApplicationException.Conflict(
                "The reservation does not own all of its recorded seats.");
        }

        reservation.Status = ReservationStatus.Confirmed;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReservationConfirmationResponse(reservation.Reference, reservation.Status.ToString());
    }

    private static TimeZoneInfo ResolveCinemaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }
    }
}
