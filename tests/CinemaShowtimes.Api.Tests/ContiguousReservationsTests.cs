using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Domain.Enums;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaShowtimes.Api.Tests;

public sealed class ContiguousReservationsTests
{
    [Fact]
    public async Task ReservesFirstBlockOfTwoAdjacentFreeSeats()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveContiguousAsync(client, showtimeId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(2, reservation.SeatCount);
        Assert.Equal(new[] { 1, 2 }, await GetReservedSeatIdsAsync(factory, reservation.Reference));
    }

    [Fact]
    public async Task ReservesBlockAtStartOfRow()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveContiguousAsync(client, showtimeId, 3);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(new[] { 1, 2, 3 }, await GetReservedSeatIdsAsync(factory, reservation.Reference));
    }

    [Fact]
    public async Task ReservesBlockAtEndOfRowWhenEarlierSeatsAreSold()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        await MarkSeatsSoldAsync(factory, showtimeId, Enumerable.Range(1, 8));

        using var response = await ReserveContiguousAsync(client, showtimeId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(new[] { 9, 10 }, await GetReservedSeatIdsAsync(factory, reservation.Reference));
    }

    [Fact]
    public async Task RejectsRequestWhenNoBlockCanFitInAnyRow()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveContiguousAsync(client, showtimeId, 11);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task RejectsWhenFreeSeatCountIsSufficientButNoRowCanFitTheBlock()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            Assert.True(await dbContext.Seats.CountAsync(seat => seat.AuditoriumId == 1) >= 11);
        }

        using var response = await ReserveContiguousAsync(client, showtimeId, 11);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task SkipsOccupiedSeatInsideEarlierPotentialBlock()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        await MarkSeatsSoldAsync(factory, showtimeId, [2]);

        using var response = await ReserveContiguousAsync(client, showtimeId, 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(new[] { 3, 4 }, await GetReservedSeatIdsAsync(factory, reservation.Reference));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RejectsNonPositiveSeatCount(int count)
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveContiguousAsync(client, showtimeId, count);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    private static async Task<int> CreateShowtimeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId = 1,
            auditoriumId = 1,
            startTime = "2026-10-25T14:00:00"
        });
        response.EnsureSuccessStatusCode();
        var showtime = await response.Content.ReadFromJsonAsync<ShowtimeResponse>();
        Assert.NotNull(showtime);
        return showtime.Id;
    }

    private static Task<HttpResponseMessage> ReserveContiguousAsync(
        HttpClient client,
        int showtimeId,
        int count) =>
        client.PostAsJsonAsync(
            $"/api/showtimes/{showtimeId}/reservations/contiguous",
            new CreateContiguousReservationRequest { Count = count });

    private static async Task<int[]> GetReservedSeatIdsAsync(CinemaApiFactory factory, Guid reference)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var reservationId = await dbContext.Reservations
            .Where(item => item.Reference == reference)
            .Select(item => item.Id)
            .SingleAsync();
        return await dbContext.ShowtimeSeats
            .Where(item => item.CurrentReservationId == reservationId)
            .Select(item => item.SeatId)
            .OrderBy(seatId => seatId)
            .ToArrayAsync();
    }

    private static async Task MarkSeatsSoldAsync(
        CinemaApiFactory factory,
        int showtimeId,
        IEnumerable<int> seatIds)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = factory.Clock.GetUtcNow();
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

        foreach (var seatId in seatIds)
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

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, problem.RootElement.GetProperty("code").GetString());
    }
}
