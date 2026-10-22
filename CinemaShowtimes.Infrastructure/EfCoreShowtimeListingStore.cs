using CinemaShowtimes.Application.Persistence.Contracts;
using CinemaShowtimes.Application.Persistence.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Infrastructure;

public sealed class EfCoreShowtimeListingStore(CinemaDbContext dbContext) : IShowtimeListingStore
{
    public async Task<IReadOnlyList<ShowtimeListingItem>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Showtimes
            .AsNoTracking()
            .OrderBy(showtime => showtime.StartTime)
            .ThenBy(showtime => showtime.Id)
            .Select(showtime => new ShowtimeListingItem(
                showtime.Id,
                showtime.MovieId,
                showtime.Movie.Title,
                showtime.Movie.Category,
                showtime.Movie.Year,
                showtime.AuditoriumId,
                showtime.Auditorium.Name,
                showtime.StartTime))
            .ToListAsync(cancellationToken);
    }
}
