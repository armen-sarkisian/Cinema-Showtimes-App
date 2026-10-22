# Cinema Showtimes API

A small REST API for creating movies and showtimes, temporarily reserving seats, and confirming reservations. The API uses separate request and response DTOs; EF Core entities are not returned to clients.

## Technology stack

- .NET 9 / ASP.NET Core Web API
- Entity Framework Core 9
- SQLite
- Swagger UI
- xUnit integration tests using SQLite

The modular monolith is split into projects with explicit dependency direction:

```text
CinemaShowtimes.Domain
        ↑                 ↑
CinemaShowtimes.Application ← CinemaShowtimes.Infrastructure
        ↑                          ↑
        └──── CinemaShowtimes.Api ─┘
```

`CinemaShowtimes.Application` owns the creation and reservation-operation contracts; `CinemaShowtimes.Infrastructure` implements them using EF Core and SQLite. The reservation contract represents each complete atomic create/confirm operation, so application code cannot separate availability checks from seat assignment. SQLite transactions and SQL are contained in Infrastructure; provider errors are translated to provider-independent persistence failures before reaching API middleware. The application layer has dedicated movie, showtime, and reservation services; ordinary and contiguous reservations share the same booking flow. Automated tests are in a separate project.

## Project structure

```text
CinemaShowtimes.Api/
  API/
    Controllers/
    Errors/
CinemaShowtimes.Application/
  DTOs/
    Requests/       # API request models
    Responses/      # API response models
  Enums/
  Errors/
  Persistence/
    Contracts/      # Persistence operation results and inputs
    Enums/
    Interfaces/     # Application-owned persistence contracts
  Services/
CinemaShowtimes.Domain/
  Entities/
  Enums/
CinemaShowtimes.Infrastructure/
  Enums/
tests/
  CinemaShowtimes.Api.Tests/
```

Request and response DTOs are kept separate under `CinemaShowtimes.Application/DTOs/Requests` and `CinemaShowtimes.Application/DTOs/Responses`. Persistence contracts and enums are likewise separated from their interfaces.

## Requirements

- .NET 9 SDK
- Windows, Linux, or macOS

The project was built with .NET SDK `9.0.203`. No database server or credentials are required.

## Run locally

From the repository root:

```powershell
dotnet run --project .\CinemaShowtimes.Api\CinemaShowtimes.Api.csproj
```

The HTTP launch profile listens on `http://localhost:5206`; Swagger UI is at `http://localhost:5206/swagger`.
The launch profile sets the `Development` environment. In that environment, startup applies pending EF Core migrations and seeds three movies, one auditorium, and 50 seats (rows A-E, seats 1-10).

## SQLite configuration and persistence

The default connection string is in `CinemaShowtimes.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Cinema": "Data Source=cinema-showtimes-app.db"
  }
}
```

The relative database filename is resolved from the application's working directory. With the run command above, the database is created as `CinemaShowtimes.Api/cinema-showtimes-app.db`. The file is ignored by Git and retains movies, showtimes, reservations, and seat assignments after the application restarts.

Override the connection string without changing source code by setting the standard ASP.NET Core environment variable before running the app:

```powershell
$env:ConnectionStrings__Cinema = "Data Source=$PWD\cinema-showtimes-app.db"
dotnet run --project .\CinemaShowtimes.Api\CinemaShowtimes.Api.csproj
```

The initial schema and seed data are managed by EF Core migrations. Development startup applies pending migrations automatically; for other environments, apply migrations as a deployment step before starting the API.

### Create and apply migrations

EF Core tooling version `9.0.21` matches the EF Core packages used by the project. Install or update the global tool:

```powershell
dotnet tool update --global dotnet-ef --version 9.0.21 --allow-downgrade
```

Create a migration from the repository root:

```powershell
dotnet ef migrations add MigrationName `
  --project .\CinemaShowtimes.Infrastructure\CinemaShowtimes.Infrastructure.csproj `
  --startup-project .\CinemaShowtimes.Api\CinemaShowtimes.Api.csproj `
  --context CinemaDbContext `
  --output-dir Migrations
```

Apply it to the configured SQLite database:

```powershell
dotnet ef database update `
  --project .\CinemaShowtimes.Infrastructure\CinemaShowtimes.Infrastructure.csproj `
  --startup-project .\CinemaShowtimes.Api\CinemaShowtimes.Api.csproj `
  --context CinemaDbContext
```

## API endpoints

All request and response bodies use JSON. Showtimes are submitted as local Kyiv wall-clock time without a UTC offset. Invalid or ambiguous daylight-saving transition times are rejected. Stored timestamps and reservation timestamps use UTC.

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/showtimes` | List all showtimes, ordered by start time |
| `GET` | `/api/showtimes/{showtimeId}/seats` | Get the seat map and current availability for a showtime |
| `POST` | `/api/movies` | Create a movie |
| `POST` | `/api/showtimes` | Create a showtime |
| `POST` | `/api/showtimes/{showtimeId}/reservations` | Reserve explicit seat IDs |
| `POST` | `/api/showtimes/{showtimeId}/reservations/contiguous` | Reserve the first available contiguous block |
| `POST` | `/api/reservations/{reference}/confirm` | Confirm a reservation |

Successful movie, showtime, and reservation creation returns `201 Created`; confirmation returns `200 OK`. Errors use the `application/problem+json` format with `code` and `traceId` extensions.

### List showtimes

```http
GET /api/showtimes
```

Returns `200 OK` and a JSON array containing each show's ID, movie ID and title, category and year, auditorium ID and name, and local cinema start time. Results are ordered by start time, then showtime ID. The endpoint includes all showtimes; clients can use the returned showtime `id` when creating a reservation.

```json
[
  {
    "id": 1,
    "movieId": 1,
    "movieTitle": "The Grand Adventure",
    "category": "Adventure",
    "year": 2024,
    "auditoriumId": 1,
    "auditoriumName": "Main Hall",
    "startTime": "2026-11-02T19:30:00"
  }
]
```

### Get showtime seats

```http
GET /api/showtimes/1/seats
```

Returns every seat in the selected show's auditorium. `Reserved` means the current reservation is still active; once its expiry time is reached, the seat is reported as `Available`. `Sold` means the current reservation is confirmed. A showtime that does not exist returns `404 Not Found`.

```json
[
  {
    "seatId": 1,
    "row": "A",
    "number": 1,
    "status": "Available"
  },
  {
    "seatId": 2,
    "row": "A",
    "number": 2,
    "status": "Reserved"
  }
]
```

### Create a movie

```http
POST /api/movies
Content-Type: application/json

{
  "title": "Example Film",
  "category": "Drama",
  "year": 2025
}
```

```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "id": 4,
  "title": "Example Film",
  "category": "Drama",
  "year": 2025
}
```

Title and category are required (maximum lengths 200 and 100 characters). The year must be between 1930 and the current year.

### Create a showtime

```http
POST /api/showtimes
Content-Type: application/json

{
  "movieId": 1,
  "auditoriumId": 1,
  "startTime": "2026-11-02T19:30:00"
}
```

```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "id": 1,
  "movieId": 1,
  "auditoriumId": 1,
  "startTime": "2026-11-02T19:30:00"
}
```

The movie and auditorium must exist, and the showtime must be in the future. Within a single auditorium, two showtimes cannot have the same start time, even when they are for different movies; simultaneous showtimes in different auditoriums are allowed. A duplicate returns `409 Conflict`. A unique database index enforces this rule even for concurrent creation requests.

### Reserve explicit seats

```http
POST /api/showtimes/1/reservations
Content-Type: application/json

{
  "seatIds": [1, 2]
}
```

```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "reference": "8c77169e-ae11-4e92-b812-9a1d95a78001",
  "seatCount": 2,
  "movie": {
    "id": 1,
    "title": "The Grand Adventure",
    "category": "Adventure",
    "year": 2024
  },
  "auditorium": {
    "id": 1,
    "name": "Main Hall"
  },
  "createdAt": "2026-10-19T12:00:00+00:00",
  "expiresAt": "2026-10-19T12:10:00+00:00"
}
```

Seat IDs must be distinct, exist, and belong to the auditorium used by the showtime.

### Reserve contiguous seats

```http
POST /api/showtimes/1/reservations/contiguous
Content-Type: application/json

{
  "count": 2
}
```

The response has the same shape as an explicit seat reservation. The API chooses the first available consecutive seats in row/seat order; it returns `409 Conflict` if no block fits.

### Confirm a reservation

```http
POST /api/reservations/8c77169e-ae11-4e92-b812-9a1d95a78001/confirm
```

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "reference": "8c77169e-ae11-4e92-b812-9a1d95a78001",
  "status": "Confirmed"
}
```

No payment is performed. Unknown references return `404`; expired, already confirmed, or otherwise ineligible reservations return `409`.

An error response has a consistent Problem Details shape:

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "Reservation conflict",
  "status": 409,
  "detail": "Seat 1 is currently reserved or has already been sold.",
  "instance": "/api/showtimes/1/reservations",
  "code": "conflict",
  "traceId": "0H..."
}
```

## Reservation lifecycle

1. A reservation is created with status `Reserved`, a public `Guid` reference, and an expiry exactly 10 minutes after its creation time.
2. It is active only while its status is `Reserved` and `now < ExpiresAt`. At `now == ExpiresAt`, it is expired for booking and confirmation purposes even if the stored status remains `Reserved`.
3. Confirmation is accepted only before expiry. A successful confirmation changes the reservation status to `Confirmed`; every current seat assignment points to that reservation, so the assigned seats are treated as sold.
4. A later reservation can atomically replace a seat assignment belonging to an expired reservation. The old reservation and its `ReservationSeat` history remain in the database. No background cleanup service is used.

## Duplicate-booking protection

`ShowtimeSeat` is the current-assignment table and has a database primary key on `(ShowtimeId, SeatId)`. `ReservationSeat` records the historical seats associated with each reservation. Both normal and contiguous reservations use the same SQLite transaction and conditional upsert.

Reservation creation and confirmation start a SQLite `IMMEDIATE` transaction before reading availability or reservation state. SQLite serializes writers; the conditional upsert can claim a seat only when it has no current owner or its previous `Reserved` owner has expired. All requested assignments and history rows are committed together; a failed seat claim rolls back the whole multi-seat reservation. Confirmation uses a conditional database update requiring the reservation to remain `Reserved`, unexpired, and to own all recorded assignments.

SQLite allows only one writer at a time. A busy/locked database is returned as `503 Service Unavailable` with `Retry-After: 1`; the application does not automatically retry that request. The unique key prevents duplicate current-assignment rows, while the transaction and conditional updates enforce the booking rules for API operations.

## Build and tests

Run from the repository root:

```powershell
dotnet build .\cinema-showtimes-app.sln
dotnet test .\cinema-showtimes-app.sln
```

The integration tests use SQLite, including two independent `DbContext` instances for concurrent reservation and confirmation cases. They also cover validation, expiration boundaries, unavailable seats, contiguous blocks, and database lock responses.

## Limitations and trade-offs

- The schema is created automatically only in `Development`; migrations are not included. `EnsureCreated` is suitable for this local starter, not incremental schema upgrades.
- The seeded auditorium is fixed; there is no endpoint for managing halls or their seats.
- The API validates that a reserved seat belongs to the showtime's auditorium. The relational schema has separate foreign keys to showtime and seat, and does not itself enforce that cross-entity auditorium match for data written outside the API.
- The contiguous algorithm returns the first fitting block and does not optimize seat allocation.
- SQLite serializes all database writes, which is appropriate for this small assessment but limits write throughput.
- The code handles expected SQLite busy/constraint errors but does not automatically retry failed requests.
- No list/read APIs, authentication, payment integration, or reservation-expiration background worker are included.
