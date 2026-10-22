using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaShowtimes.Api.Tests;
using CinemaShowtimes.Application.DTOs.Requests;
using CinemaShowtimes.Application.DTOs.Responses;
using CinemaShowtimes.Domain.Enums;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaShowtimes.Api.Tests;

public sealed class ShowtimeSeatsTests
{
    [Fact]
    public async Task ReturnsEverySeatAsAvailableWhenShowtimeHasNoReservations()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await client.GetAsync($"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seats = await response.Content.ReadFromJsonAsync<SeatAvailabilityResponse[]>();
        Assert.NotNull(seats);
        Assert.Equal(50, seats.Length);
        Assert.Equal(Enumerable.Range(1, 50), seats.Select(seat => seat.SeatId));
        Assert.All(seats, seat => Assert.Equal("Available", seat.Status));
        Assert.Equal("A", seats[0].Row);
        Assert.Equal(1, seats[0].Number);
        Assert.Equal("E", seats[^1].Row);
        Assert.Equal(10, seats[^1].Number);
    }

    [Fact]
    public async Task ReturnsIndependentStatusesForAvailableReservedSoldAndExpiredSeats()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        var activeReservation = await ReserveAsync(client, showtimeId, 1);
        var soldReservation = await ReserveAsync(client, showtimeId, 2);
        var expiredReservation = await ReserveAsync(client, showtimeId, 3);

        using (var confirmation = await client.PostAsync(
                   $"/api/reservations/{soldReservation.Reference}/confirm",
                   content: null))
        {
            Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var expired = await dbContext.Reservations
                .SingleAsync(reservation => reservation.Reference == expiredReservation.Reference);
            expired.CreatedAt = factory.Clock.GetUtcNow().AddTicks(-1);
            expired.ExpiresAt = factory.Clock.GetUtcNow();
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seats = await response.Content.ReadFromJsonAsync<SeatAvailabilityResponse[]>();
        Assert.NotNull(seats);
        Assert.Equal(50, seats.Length);
        Assert.Equal("Reserved", Assert.Single(seats, seat => seat.SeatId == 1).Status);
        Assert.Equal("Sold", Assert.Single(seats, seat => seat.SeatId == 2).Status);
        Assert.Equal("Available", Assert.Single(seats, seat => seat.SeatId == 3).Status);
        Assert.Equal("Available", Assert.Single(seats, seat => seat.SeatId == 4).Status);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Equal(
            ReservationStatus.Reserved,
            (await verificationDb.Reservations.SingleAsync(item =>
                item.Reference == activeReservation.Reference)).Status);
    }

    [Fact]
    public async Task ReturnsNotFoundForMissingShowtime()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/showtimes/999/seats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ReturnsEmptyListWhenShowtimeAuditoriumHasNoSeats()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            await dbContext.Seats.ExecuteDeleteAsync();
        }

        using var response = await client.GetAsync($"/api/showtimes/{showtimeId}/seats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seats = await response.Content.ReadFromJsonAsync<SeatAvailabilityResponse[]>();
        Assert.NotNull(seats);
        Assert.Empty(seats);
    }

    private static async Task<int> CreateShowtimeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId = 1,
            auditoriumId = 1,
            startTime = "2026-10-25T18:00:00"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var showtime = await response.Content.ReadFromJsonAsync<ShowtimeResponse>();
        Assert.NotNull(showtime);
        return showtime.Id;
    }

    private static async Task<ReservationResponse> ReserveAsync(
        HttpClient client,
        int showtimeId,
        int seatId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/showtimes/{showtimeId}/reservations",
            new CreateReservationRequest { SeatIds = [seatId] });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        return reservation;
    }
}
