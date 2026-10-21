using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Persistence;

public interface IMovieCreationStore
{
    Task<Movie> CreateAsync(Movie movie, CancellationToken cancellationToken);
}
