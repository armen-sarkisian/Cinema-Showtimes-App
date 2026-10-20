using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Application.Persistence;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Application;

namespace CinemaShowtimes.Application.Services;

public sealed class MovieApplicationService(IMovieCreationStore movies, TimeProvider timeProvider)
{
    public async Task<MovieResponse> CreateMovieAsync(
        CreateMovieRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var category = request.Category.Trim();
        if (title.Length == 0 || category.Length == 0)
        {
            throw CinemaApplicationException.Invalid(
                "Title and category must contain at least one non-whitespace character.");
        }

        var currentYear = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), CinemaTimeZone.Value).Year;
        if (request.Year < 1930 || request.Year > currentYear)
        {
            throw CinemaApplicationException.Invalid($"Year must be between 1930 and {currentYear}.");
        }

        var movie = new Movie
        {
            Title = title,
            Category = category,
            Year = request.Year
        };

        movie = await movies.CreateAsync(movie, cancellationToken);
        return new MovieResponse(movie.Id, movie.Title, movie.Category, movie.Year);
    }
}