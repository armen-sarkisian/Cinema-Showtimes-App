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

public sealed class ReservationConfirmationTests
{
    [Fact]
    public async Task ConfirmsActiveReservationAndMarksEverySeatSold()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var (showtimeId, reservation) = await CreateReservationAsync(client, [1, 2, 3]);

        using var response = await ConfirmAsync(client, reservation.Reference);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmation = await response.Content.ReadFromJsonAsync<ReservationConfirmationResponse>();
        Assert.NotNull(confirmation);
        Assert.Equal(reservation.Reference, confirmation.Reference);
        Assert.Equal(nameof(ReservationStatus.Confirmed), confirmation.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var storedReservation = await dbContext.Reservations
            .SingleAsync(item => item.Reference == reservation.Reference);
        Assert.Equal(ReservationStatus.Confirmed, storedReservation.Status);

        var seats = await dbContext.ShowtimeSeats
            .Where(item => item.ShowtimeId == showtimeId)
            .OrderBy(item => item.SeatId)
            .ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, seats.Select(item => item.SeatId));
        Assert.All(seats, seat => Assert.Equal(storedReservation.Id, seat.CurrentReservationId));
    }

    [Fact]
    public async Task RejectsConfirmationExactlyAtExpiration()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var (_, reservation) = await CreateReservationAsync(client, [1]);
        factory.Clock.Advance(reservation.ExpiresAt - factory.Clock.GetUtcNow());

        using var response = await ConfirmAsync(client, reservation.Reference);

        await AssertConflictAsync(response);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Equal(
            ReservationStatus.Reserved,
            (await dbContext.Reservations.SingleAsync(item => item.Reference == reservation.Reference)).Status);
    }

    [Fact]
    public async Task RejectsConfirmationAfterExpiration()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var (_, reservation) = await CreateReservationAsync(client, [1]);
        factory.Clock.Advance(reservation.ExpiresAt - factory.Clock.GetUtcNow() + TimeSpan.FromTicks(1));

        using var response = await ConfirmAsync(client, reservation.Reference);

        await AssertConflictAsync(response);
    }

    [Fact]
    public async Task UnknownReferenceReturnsNotFound()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        using var response = await ConfirmAsync(client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RepeatedConfirmationDoesNotRepeatStateTransition()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var (_, reservation) = await CreateReservationAsync(client, [1]);

        using var first = await ConfirmAsync(client, reservation.Reference);
        using var second = await ConfirmAsync(client, reservation.Reference);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertConflictAsync(second);
    }

    [Theory]
    [InlineData(ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Confirmed)]
    public async Task RejectsReservationInAnotherTerminalStatus(ReservationStatus status)
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var (_, reservation) = await CreateReservationAsync(client, [1]);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var storedReservation = await dbContext.Reservations
                .SingleAsync(item => item.Reference == reservation.Reference);
            storedReservation.Status = status;
            await dbContext.SaveChangesAsync();
        }

        using var response = await ConfirmAsync(client, reservation.Reference);

        await AssertConflictAsync(response);
    }

    [Fact]
    public async Task ConcurrentConfirmationsOnlyPerformOneStateTransition()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var (_, reservation) = await CreateReservationAsync(client, [1, 2]);

        var responses = await Task.WhenAll(
            ConfirmAsync(client, reservation.Reference),
            ConfirmAsync(client, reservation.Reference));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response =>
                response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static async Task<(int ShowtimeId, ReservationResponse Reservation)> CreateReservationAsync(
        HttpClient client,
        int[] seatIds)
    {
        using var showtimeResponse = await client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId = 1,
            auditoriumId = 1,
            startTime = "2026-10-25T14:00:00"
        });
        showtimeResponse.EnsureSuccessStatusCode();
        var showtime = await showtimeResponse.Content.ReadFromJsonAsync<ShowtimeResponse>();
        Assert.NotNull(showtime);

        using var reservationResponse = await client.PostAsJsonAsync(
            $"/api/showtimes/{showtime.Id}/reservations",
            new CreateReservationRequest { SeatIds = seatIds.ToList() });
        reservationResponse.EnsureSuccessStatusCode();
        var reservation = await reservationResponse.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        return (showtime.Id, reservation);
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, Guid reference) =>
        client.PostAsync($"/api/reservations/{reference}/confirm", content: null);

    private static async Task AssertConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", problem.RootElement.GetProperty("code").GetString());
    }
}
