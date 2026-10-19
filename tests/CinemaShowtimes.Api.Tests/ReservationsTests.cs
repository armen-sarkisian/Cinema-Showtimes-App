using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaShowtimes.Api.Tests;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Domain.Enums;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaShowtimes.Api.Tests;

public sealed class ReservationsTests
{
    [Fact]
    public async Task ReservesOneFreeSeatForTenMinutes()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveAsync(client, showtimeId, [1]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.NotEqual(Guid.Empty, reservation.Reference);
        Assert.Equal(1, reservation.SeatCount);
        Assert.Equal(1, reservation.Movie.Id);
        Assert.Equal("The Grand Adventure", reservation.Movie.Title);
        Assert.Equal(1, reservation.Auditorium.Id);
        Assert.Equal("Main Hall", reservation.Auditorium.Name);
        Assert.Equal(factory.Clock.GetUtcNow(), reservation.CreatedAt);
        Assert.Equal(factory.Clock.GetUtcNow().AddMinutes(10), reservation.ExpiresAt);
    }

    [Fact]
    public async Task ReservesMultipleSeatsInOneReservation()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveAsync(client, showtimeId, [1, 2, 3]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(reservation);
        Assert.Equal(3, reservation.SeatCount);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Equal(3, await dbContext.ReservationSeats.CountAsync());
    }

    [Fact]
    public async Task RejectsEmptySeatList()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveAsync(client, showtimeId, []);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task RejectsDuplicateSeatIds()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        using var response = await ReserveAsync(client, showtimeId, [1, 1]);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task RejectsSeatFromAnotherAuditorium()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        int otherHallSeatId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var otherAuditorium = new Auditorium { Name = "Other Hall" };
            otherAuditorium.Seats.Add(new Seat { Row = "A", Number = 1 });
            dbContext.Auditoriums.Add(otherAuditorium);
            await dbContext.SaveChangesAsync();
            otherHallSeatId = otherAuditorium.Seats.Single().Id;
        }

        using var response = await ReserveAsync(client, showtimeId, [otherHallSeatId]);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    public async Task RejectsReservationForMissingShowtime()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        using var response = await ReserveAsync(client, 999, [1]);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task RejectsSeatThatHasBeenSold()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        await AddExistingReservationAsync(factory, showtimeId, 1, ReservationStatus.Confirmed, TimeSpan.FromMinutes(10));

        using var response = await ReserveAsync(client, showtimeId, [1]);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task RejectsSeatHeldByAnUnexpiredReservation()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        await AddExistingReservationAsync(factory, showtimeId, 1, ReservationStatus.Reserved, TimeSpan.FromMinutes(10));

        using var response = await ReserveAsync(client, showtimeId, [1]);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "conflict");
    }

    [Fact]
    public async Task AllowsReservationAfterPreviousReservationHasExpiredWithoutChangingItsStatus()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);
        await AddExistingReservationAsync(factory, showtimeId, 1, ReservationStatus.Reserved, TimeSpan.Zero);

        using var response = await ReserveAsync(client, showtimeId, [1]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var reservations = await dbContext.Reservations.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(2, reservations.Count);
        Assert.Equal(ReservationStatus.Reserved, reservations[0].Status);
        Assert.Equal(ReservationStatus.Reserved, reservations[1].Status);
        Assert.Equal(2, await dbContext.ReservationSeats.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRequestsCannotReserveTheSameSeatTwice()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var showtimeId = await CreateShowtimeAsync(client);

        var responses = await Task.WhenAll(
            ReserveAsync(client, showtimeId, [1]),
            ReserveAsync(client, showtimeId, [1]));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
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

    private static Task<HttpResponseMessage> ReserveAsync(
        HttpClient client,
        int showtimeId,
        int[] seatIds) =>
        client.PostAsJsonAsync(
            $"/api/showtimes/{showtimeId}/reservations",
            new CreateReservationRequest { SeatIds = seatIds.ToList() });

    private static async Task AddExistingReservationAsync(
        CinemaApiFactory factory,
        int showtimeId,
        int seatId,
        ReservationStatus status,
        TimeSpan remainingDuration)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = factory.Clock.GetUtcNow();
        var reservation = new Reservation
        {
            Reference = Guid.NewGuid(),
            ShowtimeId = showtimeId,
            CreatedAt = now.Add(remainingDuration).AddMinutes(-10),
            ExpiresAt = now.Add(remainingDuration),
            Status = status
        };
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();
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
