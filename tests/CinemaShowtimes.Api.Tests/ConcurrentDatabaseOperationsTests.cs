using CinemaShowtimes.Application;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Domain.Enums;
using CinemaShowtimes.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaShowtimes.Api.Tests;

public sealed class ConcurrentDatabaseOperationsTests
{
    [Fact]
    public async Task IndependentContextsCannotCreateDuplicateActiveAssignments()
    {
        using var factory = new CinemaApiFactory();
        var showtimeId = await CreateShowtimeAsync(factory);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.NotSame(firstDbContext, secondDbContext);
        var firstService = firstScope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<OperationResult> ReserveAsync(CinemaApplicationService service)
        {
            await start.Task;
            try
            {
                var reservation = await service.CreateReservationAsync(
                    showtimeId,
                    new CreateReservationRequest { SeatIds = [1] },
                    CancellationToken.None);
                return OperationResult.Success(reservation.Reference);
            }
            catch (CinemaApplicationException exception) when (exception.StatusCode == 409)
            {
                return OperationResult.Failure(exception.StatusCode);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                return OperationResult.Failure(503);
            }
        }

        var first = ReserveAsync(firstService);
        var second = ReserveAsync(secondService);
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Reference.HasValue);
        Assert.Single(results, result => result.StatusCode is 409 or 503);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var assignments = await verificationDb.ShowtimeSeats
            .Where(item => item.ShowtimeId == showtimeId && item.SeatId == 1)
            .ToListAsync();
        Assert.Single(assignments);
        Assert.NotNull(assignments[0].CurrentReservationId);

        var activeHistoryClaims = await (
            from history in verificationDb.ReservationSeats
            join reservation in verificationDb.Reservations
                on new { history.ReservationId, history.ShowtimeId }
                equals new { ReservationId = reservation.Id, reservation.ShowtimeId }
            where history.ShowtimeId == showtimeId
                && history.SeatId == 1
                && reservation.Status == ReservationStatus.Reserved
                && reservation.ExpiresAt > factory.Clock.GetUtcNow()
            select reservation.Id).Distinct().CountAsync();
        Assert.Equal(1, activeHistoryClaims);
    }

    [Fact]
    public async Task IndependentContextsSerializeConcurrentConfirmation()
    {
        using var factory = new CinemaApiFactory();
        var showtimeId = await CreateShowtimeAsync(factory);
        var reservation = await CreateReservationAsync(factory, showtimeId, [1, 2, 3]);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.NotSame(firstDbContext, secondDbContext);
        var firstService = firstScope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<OperationResult> ConfirmAsync(CinemaApplicationService service)
        {
            await start.Task;
            try
            {
                await service.ConfirmReservationAsync(reservation.Reference, CancellationToken.None);
                return OperationResult.Success(reservation.Reference);
            }
            catch (CinemaApplicationException exception) when (exception.StatusCode == 409)
            {
                return OperationResult.Failure(exception.StatusCode);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                return OperationResult.Failure(503);
            }
        }

        var first = ConfirmAsync(firstService);
        var second = ConfirmAsync(secondService);
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Reference.HasValue);
        Assert.Single(results, result => result.StatusCode is 409 or 503);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var storedReservation = await verificationDb.Reservations
            .SingleAsync(item => item.Reference == reservation.Reference);
        Assert.Equal(ReservationStatus.Confirmed, storedReservation.Status);
        var ownedSeats = await verificationDb.ShowtimeSeats
            .Where(item => item.ShowtimeId == showtimeId
                && item.CurrentReservationId == storedReservation.Id)
            .ToListAsync();
        Assert.Equal(3, ownedSeats.Count);
        Assert.Equal(new[] { 1, 2, 3 }, ownedSeats.Select(item => item.SeatId).Order());
    }

    [Fact]
    public async Task IndependentContextsCannotReserveOverlappingContiguousBlocks()
    {
        using var factory = new CinemaApiFactory();
        var showtimeId = await CreateShowtimeAsync(factory);
        await MarkAllExceptPairAsSoldAsync(factory, showtimeId, [1, 2]);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.NotSame(firstDbContext, secondDbContext);
        var firstService = firstScope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<OperationResult> ReserveBlockAsync(CinemaApplicationService service)
        {
            await start.Task;
            try
            {
                var reservation = await service.CreateContiguousReservationAsync(
                    showtimeId,
                    2,
                    CancellationToken.None);
                return OperationResult.Success(reservation.Reference);
            }
            catch (CinemaApplicationException exception) when (exception.StatusCode == 409)
            {
                return OperationResult.Failure(exception.StatusCode);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
            {
                return OperationResult.Failure(503);
            }
        }

        var first = ReserveBlockAsync(firstService);
        var second = ReserveBlockAsync(secondService);
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Reference.HasValue);
        Assert.Single(results, result => result.StatusCode is 409 or 503);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var assignments = await verificationDb.ShowtimeSeats
            .Where(item => item.ShowtimeId == showtimeId)
            .ToListAsync();
        Assert.Equal(50, assignments.Count);
        var activeReservations = await verificationDb.Reservations
            .Where(item => item.ShowtimeId == showtimeId
                && item.Status == ReservationStatus.Reserved
                && item.ExpiresAt > factory.Clock.GetUtcNow())
            .ToListAsync();
        Assert.Single(activeReservations);
        Assert.Equal(2, assignments.Count(item => item.CurrentReservationId == activeReservations[0].Id));
        Assert.Equal(1, await verificationDb.Reservations.CountAsync(
            item => item.ShowtimeId == showtimeId && item.Status == ReservationStatus.Confirmed));
    }

    private static async Task<int> CreateShowtimeAsync(CinemaApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var showtime = new Showtime
        {
            MovieId = 1,
            AuditoriumId = 1,
            StartTime = factory.Clock.GetUtcNow().AddDays(1)
        };
        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync();
        return showtime.Id;
    }

    private static async Task<ReservationResponse> CreateReservationAsync(
        CinemaApiFactory factory,
        int showtimeId,
        List<int> seatIds)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CinemaApplicationService>();
        return await service.CreateReservationAsync(
            showtimeId,
            new CreateReservationRequest { SeatIds = seatIds },
            CancellationToken.None);
    }

    private static async Task MarkAllExceptPairAsSoldAsync(
        CinemaApiFactory factory,
        int showtimeId,
        IReadOnlyCollection<int> availableSeatIds)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = factory.Clock.GetUtcNow();
        var soldSeatIds = await dbContext.Seats
            .Where(seat => seat.AuditoriumId == 1 && !availableSeatIds.Contains(seat.Id))
            .Select(seat => seat.Id)
            .ToListAsync();
        var reservation = new Reservation
        {
            Reference = Guid.NewGuid(),
            ShowtimeId = showtimeId,
            CreatedAt = now.AddMinutes(-10),
            ExpiresAt = now,
            Status = ReservationStatus.Confirmed
        };
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        foreach (var seatId in soldSeatIds)
        {
            dbContext.ShowtimeSeats.Add(new ShowtimeSeat
            {
                ShowtimeId = showtimeId,
                SeatId = seatId,
                CurrentReservationId = reservation.Id
            });
            dbContext.ReservationSeats.Add(new ReservationSeat
            {
                ReservationId = reservation.Id,
                ShowtimeId = showtimeId,
                SeatId = seatId
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed record OperationResult(Guid? Reference, int? StatusCode)
    {
        public static OperationResult Success(Guid reference) => new(reference, null);
        public static OperationResult Failure(int statusCode) => new(null, statusCode);
    }
}
