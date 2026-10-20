using CinemaShowtimes.Application.Persistence;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Infrastructure;

public sealed class SqliteReservationOperations(
    CinemaDbContext dbContext,
    TimeProvider timeProvider) : IReservationOperations
{
    public async Task<ReservationCreationResult> CreateAsync(
        int showtimeId,
        ReservationSeatSelection selection,
        TimeSpan reservationLifetime,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CreateCoreAsync(
                showtimeId,
                selection,
                reservationLifetime,
                cancellationToken);
        }
        catch (Exception exception) when (SqliteExceptionTranslator.IsExpected(exception))
        {
            throw SqliteExceptionTranslator.Translate(exception);
        }
    }

    private async Task<ReservationCreationResult> CreateCoreAsync(
        int showtimeId,
        ReservationSeatSelection selection,
        TimeSpan reservationLifetime,
        CancellationToken cancellationToken)
    {
        var requestedSeatIds = selection switch
        {
            ReservationSeatSelection.Explicit explicitSeats => explicitSeats.SeatIds,
            ReservationSeatSelection.Contiguous => null,
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };
        if (requestedSeatIds?.Any(seatId => seatId <= 0) == true
            || requestedSeatIds?.Distinct().Count() != requestedSeatIds?.Count)
        {
            return new ReservationCreationResult(ReservationCreationStatus.InvalidSeats);
        }

        var connection = dbContext.Database.GetDbConnection() as SqliteConnection
            ?? throw new InvalidOperationException("Reservation operations require SQLite.");
        await connection.OpenAsync(cancellationToken);
        await using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await dbContext.Database.UseTransactionAsync(
            sqliteTransaction,
            cancellationToken)
            ?? throw new InvalidOperationException("Could not enlist the SQLite reservation transaction.");
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var expiresAt = now.Add(reservationLifetime);

        var showtime = await dbContext.Showtimes
            .Include(item => item.Movie)
            .Include(item => item.Auditorium)
            .SingleOrDefaultAsync(item => item.Id == showtimeId, cancellationToken);
        if (showtime is null)
        {
            return new ReservationCreationResult(ReservationCreationStatus.ShowtimeNotFound);
        }

        if (selection is ReservationSeatSelection.Contiguous contiguous)
        {
            if (contiguous.Count <= 0)
            {
                return new ReservationCreationResult(ReservationCreationStatus.NoContiguousSeats);
            }

            var auditoriumSeats = await dbContext.Seats
                .AsNoTracking()
                .Where(seat => seat.AuditoriumId == showtime.AuditoriumId)
                .OrderBy(seat => seat.Row)
                .ThenBy(seat => seat.Number)
                .Select(seat => new SeatCandidate(seat.Id, seat.Row, seat.Number))
                .ToListAsync(cancellationToken);
            if (contiguous.Count > auditoriumSeats.Count)
            {
                return new ReservationCreationResult(ReservationCreationStatus.NoContiguousSeats);
            }

            var currentAssignments = await dbContext.ShowtimeSeats
                .AsNoTracking()
                .Include(seat => seat.CurrentReservation)
                .Where(seat => seat.ShowtimeId == showtimeId)
                .ToDictionaryAsync(seat => seat.SeatId, cancellationToken);
            requestedSeatIds = FindContiguousAvailableSeats(
                auditoriumSeats,
                currentAssignments,
                contiguous.Count,
                now);
            if (requestedSeatIds.Count != contiguous.Count)
            {
                return new ReservationCreationResult(ReservationCreationStatus.NoContiguousSeats);
            }
        }

        var requestedSeats = await dbContext.Seats
            .Where(seat => requestedSeatIds!.Contains(seat.Id)
                && seat.AuditoriumId == showtime.AuditoriumId)
            .ToListAsync(cancellationToken);
        if (requestedSeats.Count != requestedSeatIds!.Count)
        {
            return new ReservationCreationResult(ReservationCreationStatus.InvalidSeats);
        }

        var reservation = new Reservation
        {
            Reference = Guid.NewGuid(),
            ShowtimeId = showtimeId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
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
                return new ReservationCreationResult(
                    ReservationCreationStatus.SeatUnavailable,
                    ConflictingSeatId: seatId);
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

        return new ReservationCreationResult(
            ReservationCreationStatus.Created,
            reservation,
            showtime.Movie,
            showtime.Auditorium,
            requestedSeatIds.Count);
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

    public async Task<ReservationConfirmationStatus> ConfirmAsync(
        Guid reference,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ConfirmCoreAsync(reference, cancellationToken);
        }
        catch (Exception exception) when (SqliteExceptionTranslator.IsExpected(exception))
        {
            throw SqliteExceptionTranslator.Translate(exception);
        }
    }

    private async Task<ReservationConfirmationStatus> ConfirmCoreAsync(
        Guid reference,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection() as SqliteConnection
            ?? throw new InvalidOperationException("Reservation confirmation requires SQLite.");
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
            return ReservationConfirmationStatus.NotFound;
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return ReservationConfirmationStatus.NotReserved;
        }

        if (now >= reservation.ExpiresAt)
        {
            return ReservationConfirmationStatus.Expired;
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
            return ReservationConfirmationStatus.AssignmentMismatch;
        }

        await transaction.CommitAsync(cancellationToken);
        return ReservationConfirmationStatus.Confirmed;
    }
}
