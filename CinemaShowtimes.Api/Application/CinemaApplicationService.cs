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

    public Task<ReservationResponse> CreateReservationAsync(
        int showtimeId,
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SeatIds is null || request.SeatIds.Count == 0)
        {
            throw CinemaApplicationException.Invalid("At least one seat must be selected.");
        }

        return CreateReservationCoreAsync(showtimeId, request.SeatIds, null, cancellationToken);
    }

    public Task<ReservationResponse> CreateContiguousReservationAsync(
        int showtimeId,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            throw CinemaApplicationException.Invalid("Seat count must be greater than zero.");
        }

        return CreateReservationCoreAsync(showtimeId, null, count, cancellationToken);
    }

    private async Task<ReservationResponse> CreateReservationCoreAsync(
        int showtimeId,
        IReadOnlyList<int>? requestedSeatIds,
        int? contiguousSeatCount,
        CancellationToken cancellationToken)
    {
        if (requestedSeatIds is not null && requestedSeatIds.Count == 0)
        {
            throw CinemaApplicationException.Invalid("At least one seat must be selected.");
        }

        if (requestedSeatIds?.Any(seatId => seatId <= 0) == true)
        {
            throw CinemaApplicationException.Invalid("Seat IDs must be positive integers.");
        }

        if (requestedSeatIds is not null
            && requestedSeatIds.Distinct().Count() != requestedSeatIds.Count)
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

        if (requestedSeatIds is null)
        {
            var auditoriumSeats = await dbContext.Seats
                .AsNoTracking()
                .Where(seat => seat.AuditoriumId == showtime.AuditoriumId)
                .OrderBy(seat => seat.Row)
                .ThenBy(seat => seat.Number)
                .Select(seat => new SeatCandidate(seat.Id, seat.Row, seat.Number))
                .ToListAsync(cancellationToken);
            if (contiguousSeatCount > auditoriumSeats.Count)
            {
                throw CinemaApplicationException.Conflict(
                    $"No contiguous block of {contiguousSeatCount} available seats exists.");
            }

            var currentAssignments = await dbContext.ShowtimeSeats
                .AsNoTracking()
                .Include(seat => seat.CurrentReservation)
                .Where(seat => seat.ShowtimeId == showtimeId)
                .ToDictionaryAsync(seat => seat.SeatId, cancellationToken);

            requestedSeatIds = FindContiguousAvailableSeats(
                auditoriumSeats,
                currentAssignments,
                contiguousSeatCount!.Value,
                now);
            if (requestedSeatIds.Count != contiguousSeatCount)
            {
                throw CinemaApplicationException.Conflict(
                    $"No contiguous block of {contiguousSeatCount} available seats exists.");
            }
        }

        var requestedSeats = await dbContext.Seats
            .Where(seat => requestedSeatIds.Contains(seat.Id)
                && seat.AuditoriumId == showtime.AuditoriumId)
            .ToListAsync(cancellationToken);
        if (requestedSeats.Count != requestedSeatIds.Count)
        {
            throw CinemaApplicationException.Invalid(
                "Every seat must exist and belong to the showtime's auditorium.");
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

        foreach (var seatId in requestedSeatIds)
        {
            var rowsAffected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ShowtimeSeats" ("ShowtimeId", "SeatId", "CurrentReservationId")
                VALUES ({showtimeId}, {seatId}, {reservation.Id})
                ON CONFLICT ("ShowtimeId", "SeatId") DO UPDATE
                SET "CurrentReservationId" = excluded."CurrentReservationId"
                WHERE "ShowtimeSeats"."CurrentReservationId" IS NULL
                   OR EXISTS (
                       SELECT 1
                       FROM "Reservations" AS currentReservation
                       WHERE currentReservation."Id" = "ShowtimeSeats"."CurrentReservationId"
                         AND currentReservation."ShowtimeId" = "ShowtimeSeats"."ShowtimeId"
                         AND (
                             currentReservation."Status" = {(int)ReservationStatus.Expired}
                             OR (
                                 currentReservation."Status" = {(int)ReservationStatus.Reserved}
                                 AND currentReservation."ExpiresAt" <= {now.UtcDateTime.Ticks}
                             )
                         )
                   )
                """, cancellationToken);

            if (rowsAffected != 1)
            {
                throw CinemaApplicationException.Conflict(
                    $"Seat {seatId} is currently reserved or has already been sold.");
            }

            dbContext.ReservationSeats.Add(new ReservationSeat
            {
                ReservationId = reservation.Id,
                ShowtimeId = showtimeId,
                SeatId = seatId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReservationResponse(
            reservation.Reference,
            requestedSeatIds.Count,
            new MovieSummaryResponse(
                showtime.Movie.Id,
                showtime.Movie.Title,
                showtime.Movie.Category,
                showtime.Movie.Year),
            new AuditoriumSummaryResponse(showtime.Auditorium.Id, showtime.Auditorium.Name),
            reservation.CreatedAt,
            reservation.ExpiresAt);
    }

    private static IReadOnlyList<int> FindContiguousAvailableSeats(
        IReadOnlyList<SeatCandidate> seats,
        IReadOnlyDictionary<int, ShowtimeSeat> assignments,
        int count,
        DateTimeOffset now)
    {
        var orderedSeats = seats
            .OrderBy(seat => seat.Row, StringComparer.Ordinal)
            .ThenBy(seat => seat.Number)
            .ToArray();
        var block = new List<SeatCandidate>(count);
        SeatCandidate? previous = null;

        foreach (var seat in orderedSeats)
        {
            var isAvailable = !assignments.TryGetValue(seat.Id, out var assignment)
                || assignment.CurrentReservationId is null
                || assignment.CurrentReservation is { } reservation
                && (reservation.Status == ReservationStatus.Expired
                    || reservation.Status == ReservationStatus.Reserved && now >= reservation.ExpiresAt);
            var followsPrevious = previous is not null
                && string.Equals(previous.Row, seat.Row, StringComparison.Ordinal)
                && seat.Number == previous.Number + 1;

            if (!isAvailable || (block.Count > 0 && !followsPrevious))
            {
                block.Clear();
            }

            if (isAvailable)
            {
                block.Add(seat);
                if (block.Count == count)
                {
                    return block.Select(item => item.Id).ToArray();
                }
            }

            previous = seat;
        }

        return [];
    }

    private sealed record SeatCandidate(int Id, string Row, int Number);

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
            .AsNoTracking()
            .Where(item => item.Reference == reference)
            .Select(item => new { item.Id, item.ShowtimeId, item.Status, item.ExpiresAt })
            .SingleOrDefaultAsync(cancellationToken);
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

        var rowsAffected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Reservations"
            SET "Status" = {(int)ReservationStatus.Confirmed}
            WHERE "Id" = {reservation.Id}
              AND "Status" = {(int)ReservationStatus.Reserved}
              AND "ExpiresAt" > {now.UtcDateTime.Ticks}
              AND EXISTS (
                  SELECT 1
                  FROM "ReservationSeats" AS history
                  WHERE history."ReservationId" = "Reservations"."Id"
                    AND history."ShowtimeId" = "Reservations"."ShowtimeId"
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM "ReservationSeats" AS history
                  LEFT JOIN "ShowtimeSeats" AS currentSeat
                    ON currentSeat."ShowtimeId" = history."ShowtimeId"
                   AND currentSeat."SeatId" = history."SeatId"
                  WHERE history."ReservationId" = "Reservations"."Id"
                    AND (
                        currentSeat."CurrentReservationId" IS NULL
                        OR currentSeat."CurrentReservationId" <> "Reservations"."Id"
                    )
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM "ShowtimeSeats" AS currentSeat
                  LEFT JOIN "ReservationSeats" AS history
                    ON history."ShowtimeId" = currentSeat."ShowtimeId"
                   AND history."SeatId" = currentSeat."SeatId"
                   AND history."ReservationId" = "Reservations"."Id"
                  WHERE currentSeat."ShowtimeId" = "Reservations"."ShowtimeId"
                    AND currentSeat."CurrentReservationId" = "Reservations"."Id"
                    AND history."ReservationId" IS NULL
              )
            """, cancellationToken);
        if (rowsAffected != 1)
        {
            throw CinemaApplicationException.Conflict(
                "The reservation expired or no longer owns all of its recorded seats.");
        }

        await transaction.CommitAsync(cancellationToken);

        return new ReservationConfirmationResponse(reference, ReservationStatus.Confirmed.ToString());
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
