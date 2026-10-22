using CinemaShowtimes.Application.Persistence.Contracts;

namespace CinemaShowtimes.Application.Persistence.Interfaces;

public interface IShowtimeListingStore
{
    Task<IReadOnlyList<ShowtimeListingItem>> GetAllAsync(CancellationToken cancellationToken);
}
