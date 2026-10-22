using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaShowtimes.Api.Tests;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.DTOs.Responses;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaShowtimes.Api.Tests;

public sealed class MoviesAndShowtimesTests
{
    [Fact]
    public async Task CreateMovieReturnsCreatedMovieAndPersistsIt()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/movies", new
        {
            title = "New Release",
            category = "Drama",
            year = 2025
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movie = await response.Content.ReadFromJsonAsync<MovieResponse>();
        Assert.NotNull(movie);
        Assert.True(movie.Id > 0);
        Assert.Equal("New Release", movie.Title);
        Assert.Equal("Drama", movie.Category);
        Assert.Equal(2025, movie.Year);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.True(await dbContext.Movies.AnyAsync(item => item.Id == movie.Id));
    }

    [Theory]
    [InlineData("{\"title\":\" \",\"category\":\"Drama\",\"year\":2025}")]
    [InlineData("{\"title\":\"Valid\",\"category\":\"Drama\",\"year\":1800}")]
    [InlineData("{\"title\":\"Valid\",\"category\":\"Drama\",\"year\":2099}")]
    [InlineData("{\"title\":\"Valid\",\"category\":\"\",\"year\":2025}")]
    public async Task CreateMovieRejectsInvalidData(string json)
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/movies",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(
            problem.RootElement.GetProperty("code").GetString(),
            new[] { "validation_error", "invalid_request" });
    }

    [Fact]
    public async Task CreateMovieRejectsYearAfterCurrentYear()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/movies", new
        {
            title = "Future Release",
            category = "Drama",
            year = DateTime.UtcNow.Year + 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateMovieRejectsStringsBeyondAllowedLengths()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/movies", new
        {
            title = new string('T', 201),
            category = new string('C', 101),
            year = 2025
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateShowtimeWithExistingMovieAndAuditoriumReturnsCreatedShowtime()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();
        var startTime = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd'T'HH:mm:ss");

        var response = await client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId = 1,
            auditoriumId = 1,
            startTime
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var showtime = await response.Content.ReadFromJsonAsync<ShowtimeResponse>();
        Assert.NotNull(showtime);
        Assert.True(showtime.Id > 0);
        Assert.Equal(1, showtime.MovieId);
        Assert.Equal(1, showtime.AuditoriumId);
        Assert.Equal(DateTime.Parse(startTime), showtime.StartTime);
    }

    [Fact]
    public async Task GetShowtimesReturnsAllShowtimesInStartTimeOrderWithBookingDetails()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        var laterShowtimeResponse = await client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId = 1,
            auditoriumId = 1,
            startTime = "2026-10-25T20:00:00"
        });
        var earlierShowtimeResponse = await client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId = 2,
            auditoriumId = 1,
            startTime = "2026-10-25T18:00:00"
        });
        Assert.Equal(HttpStatusCode.Created, laterShowtimeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, earlierShowtimeResponse.StatusCode);

        var response = await client.GetAsync("/api/showtimes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var showtimes = await response.Content.ReadFromJsonAsync<ShowtimeListItemResponse[]>();
        Assert.NotNull(showtimes);
        Assert.Equal(2, showtimes.Length);
        Assert.Equal(2, showtimes[0].MovieId);
        Assert.Equal("Midnight Mystery", showtimes[0].MovieTitle);
        Assert.Equal(1, showtimes[0].AuditoriumId);
        Assert.Equal("Main Hall", showtimes[0].AuditoriumName);
        Assert.Equal(new DateTime(2026, 10, 25, 18, 0, 0), showtimes[0].StartTime);
        Assert.Equal(1, showtimes[1].MovieId);
        Assert.Equal(new DateTime(2026, 10, 25, 20, 0, 0), showtimes[1].StartTime);
    }

    [Fact]
    public async Task CreateShowtimeWithMissingMovieReturnsNotFoundProblem()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        using var response = await CreateShowtimeAsync(client, movieId: 999, auditoriumId: 1);

        await AssertNotFoundProblemAsync(response, "Movie with ID 999 was not found.");
    }

    [Fact]
    public async Task CreateShowtimeWithMissingAuditoriumReturnsNotFoundProblem()
    {
        using var factory = new CinemaApiFactory();
        using var client = factory.CreateClient();

        using var response = await CreateShowtimeAsync(client, movieId: 1, auditoriumId: 999);

        await AssertNotFoundProblemAsync(response, "Auditorium with ID 999 was not found.");
    }

    private static Task<HttpResponseMessage> CreateShowtimeAsync(
        HttpClient client,
        int movieId,
        int auditoriumId) =>
        client.PostAsJsonAsync("/api/showtimes", new
        {
            movieId,
            auditoriumId,
            startTime = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd'T'HH:mm:ss")
        });

    private static async Task AssertNotFoundProblemAsync(HttpResponseMessage response, string detail)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(detail, problem.RootElement.GetProperty("detail").GetString());
    }
}
