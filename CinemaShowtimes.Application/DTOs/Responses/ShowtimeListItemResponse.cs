namespace CinemaShowtimes.Application.DTOs.Responses;

public sealed record ShowtimeListItemResponse(
    int Id,
    int MovieId,
    string MovieTitle,
    string Category,
    int Year,
    int AuditoriumId,
    string AuditoriumName,
    DateTime StartTime);
