using CinemaShowtimes.Application.Persistence;
using CinemaShowtimes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Infrastructure;

public sealed class EfCoreShowtimeCreationStore(CinemaDbContext dbContext) : IShowtimeCreationStore
{
    public async Task<ShowtimeCreationResult> CreateAsync(
        Showtime showtime,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await dbContext.Movies.AnyAsync(
                    movie => movie.Id == showtime.MovieId,
                    cancellationToken))
            {
                return new ShowtimeCreationResult(ShowtimeCreationStatus.MovieNotFound, null);
            }

            if (!await dbContext.Auditoriums.AnyAsync(
                    auditorium => auditorium.Id == showtime.AuditoriumId,
                    cancellationToken))
            {
                return new ShowtimeCreationResult(ShowtimeCreationStatus.AuditoriumNotFound, null);
            }

            dbContext.Showtimes.Add(showtime);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ShowtimeCreationResult(ShowtimeCreationStatus.Created, showtime);
        }
        catch (Exception exception) when (SqliteExceptionTranslator.IsExpected(exception))
        {
            throw SqliteExceptionTranslator.Translate(exception);
        }
    }
}
