using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Persistence;

public interface IShowtimeCreationStore
{
    Task<ShowtimeCreationResult> CreateAsync(Showtime showtime, CancellationToken cancellationToken);
}

public enum ShowtimeCreationStatus
{
    Created,
    MovieNotFound,
    AuditoriumNotFound
}

public sealed record ShowtimeCreationResult(ShowtimeCreationStatus Status, Showtime? Showtime);
