using System.Net;
using System.Net.Http.Json;
using CinemaShowtimes.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaShowtimes.Api.Tests;

public sealed class DatabasePersistenceTests
{
    [Fact]
    public async Task MovieDataSurvivesApplicationRestart()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"cinema-showtimes-persistence-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            DefaultTimeout = 1,
            Pooling = false
        }.ToString();

        try
        {
            using (var firstFactory = new CinemaApiFactory(connectionString))
            using (var client = firstFactory.CreateClient())
            using (var response = await client.PostAsJsonAsync("/api/movies", new
            {
                title = "Persistent Test Film",
                category = "Drama",
                year = 2024
            }))
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            using (var restartedFactory = new CinemaApiFactory(connectionString))
            using (var scope = restartedFactory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
                Assert.True(await dbContext.Movies.AnyAsync(movie => movie.Title == "Persistent Test Film"));
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
