# Cinema Showtimes API

## Run locally

```powershell
dotnet run --project .\CinemaShowtimes.Api
```

In the Development environment, EF Core creates the SQLite schema on startup with
`EnsureCreated`. The relative connection string in `appsettings.json` creates
`cinema-showtimes-app.db` in the process working directory. Initial movies, the main
auditorium, and its 50 seats are seeded by the EF Core model.

Swagger UI is available at `/swagger` while the application is running.

This starter uses `EnsureCreated` and does not contain migrations. When schema evolution
is needed, add EF Core migrations and apply them with `dotnet ef database update`; do not
use `EnsureCreated` with a database managed by migrations.
