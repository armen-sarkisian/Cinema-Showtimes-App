namespace CinemaShowtimes.Application.DTOs.Responses;

public sealed record ReservationResponse(
    Guid Reference,
    int SeatCount,
    MovieSummaryResponse Movie,
    AuditoriumSummaryResponse Auditorium,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
