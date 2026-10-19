using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CinemaShowtimes.Api.Tests;

public sealed class CinemaApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"cinema-showtimes-tests-{Guid.NewGuid():N}";
    private readonly SqliteConnection _keepAliveConnection;

    public FixedTimeProvider Clock { get; } = new(
        new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));

    public CinemaApiFactory()
    {
        _keepAliveConnection = new SqliteConnection(
            $"Data Source={_databaseName};Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Cinema"] = $"Data Source={_databaseName};Mode=Memory;Cache=Shared"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAliveConnection.Dispose();
        }
    }

    public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
