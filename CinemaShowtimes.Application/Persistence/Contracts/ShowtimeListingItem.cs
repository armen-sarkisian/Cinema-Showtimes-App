namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed record ShowtimeListingItem(
    int Id,
    int MovieId,
    string MovieTitle,
    string Category,
    int Year,
    int AuditoriumId,
    string AuditoriumName,
    DateTimeOffset StartTime);
