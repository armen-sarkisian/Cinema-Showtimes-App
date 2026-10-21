namespace CinemaShowtimes.Application.DTOs.Responses;

public sealed record ShowtimeResponse(
    int Id,
    int MovieId,
    int AuditoriumId,
    DateTime StartTime);
