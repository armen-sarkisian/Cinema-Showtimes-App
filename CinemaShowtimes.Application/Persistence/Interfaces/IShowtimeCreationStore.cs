using CinemaShowtimes.Application.Persistence.Contracts;
using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Persistence.Interfaces;

public interface IShowtimeCreationStore
{
    Task<ShowtimeCreationResult> CreateAsync(Showtime showtime, CancellationToken cancellationToken);
}
