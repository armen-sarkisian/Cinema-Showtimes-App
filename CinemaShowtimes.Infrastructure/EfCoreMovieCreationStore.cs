using CinemaShowtimes.Application.Persistence;
using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Infrastructure;

public sealed class EfCoreMovieCreationStore(CinemaDbContext dbContext) : IMovieCreationStore
{
    public async Task<Movie> CreateAsync(Movie movie, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.Movies.Add(movie);
            await dbContext.SaveChangesAsync(cancellationToken);
            return movie;
        }
        catch (Exception exception) when (SqliteExceptionTranslator.IsExpected(exception))
        {
            throw SqliteExceptionTranslator.Translate(exception);
        }
    }
}
