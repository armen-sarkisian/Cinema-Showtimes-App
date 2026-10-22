using CinemaShowtimes.Application.Persistence.Contracts;

namespace CinemaShowtimes.Application.Persistence.Interfaces;

public interface IShowtimeSeatAvailabilityStore
{
    Task<IReadOnlyList<ShowtimeSeatAvailabilityItem>?> GetAsync(
        int showtimeId,
        CancellationToken cancellationToken);
}
