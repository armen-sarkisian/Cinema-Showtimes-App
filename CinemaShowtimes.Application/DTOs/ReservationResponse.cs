namespace CinemaShowtimes.Application.DTOs;

public sealed record MovieSummaryResponse(int Id, string Title, string Category, int Year);

public sealed record AuditoriumSummaryResponse(int Id, string Name);

public sealed record ReservationResponse(
    Guid Reference,
    int SeatCount,
    MovieSummaryResponse Movie,
    AuditoriumSummaryResponse Auditorium,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ReservationConfirmationResponse(Guid Reference, string Status);
