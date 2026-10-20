namespace CinemaShowtimes.Application.DTOs;

public sealed record ShowtimeResponse(
    int Id,
    int MovieId,
    int AuditoriumId,
    DateTime StartTime);
