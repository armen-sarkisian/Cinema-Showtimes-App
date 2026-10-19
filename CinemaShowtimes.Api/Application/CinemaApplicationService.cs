using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Application;

public sealed class CinemaApplicationService(CinemaDbContext dbContext, TimeProvider timeProvider)
{
    private static readonly TimeZoneInfo CinemaTimeZone = ResolveCinemaTimeZone();

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

        var currentYear = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), CinemaTimeZone).Year;
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

        dbContext.Movies.Add(movie);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MovieResponse(movie.Id, movie.Title, movie.Category, movie.Year);
    }

    public async Task<ShowtimeResponse> CreateShowtimeAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartTime == default || request.StartTime.Kind != DateTimeKind.Unspecified)
        {
            throw CinemaApplicationException.Invalid(
                "StartTime must be a valid local date and time in the cinema time zone, without a UTC offset.");
        }

        if (!await dbContext.Movies.AnyAsync(movie => movie.Id == request.MovieId, cancellationToken))
        {
            throw CinemaApplicationException.NotFound($"Movie with ID {request.MovieId} was not found.");
        }

        if (!await dbContext.Auditoriums.AnyAsync(
                auditorium => auditorium.Id == request.AuditoriumId,
                cancellationToken))
        {
            throw CinemaApplicationException.NotFound(
                $"Auditorium with ID {request.AuditoriumId} was not found.");
        }

        var localStartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Unspecified);
        if (CinemaTimeZone.IsInvalidTime(localStartTime) || CinemaTimeZone.IsAmbiguousTime(localStartTime))
        {
            throw CinemaApplicationException.Invalid(
                "StartTime does not identify a unique valid local time in the cinema time zone.");
        }

        var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localStartTime, CinemaTimeZone);
        if (startTimeUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            throw CinemaApplicationException.Invalid("StartTime must be in the future.");
        }

        var showtime = new Showtime
        {
            MovieId = request.MovieId,
            AuditoriumId = request.AuditoriumId,
            StartTime = new DateTimeOffset(startTimeUtc, TimeSpan.Zero)
        };

        dbContext.Showtimes.Add(showtime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ShowtimeResponse(
            showtime.Id,
            showtime.MovieId,
            showtime.AuditoriumId,
            TimeZoneInfo.ConvertTime(showtime.StartTime, CinemaTimeZone).DateTime);
    }

    private static TimeZoneInfo ResolveCinemaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
        }
    }
}
