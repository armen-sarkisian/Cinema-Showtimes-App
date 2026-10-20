using CinemaShowtimes.Application;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Domain.Entities;
using CinemaShowtimes.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Api.Application.Services;

public sealed class ShowtimeApplicationService(CinemaDbContext dbContext, TimeProvider timeProvider)
{
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
        if (CinemaTimeZone.Value.IsInvalidTime(localStartTime)
            || CinemaTimeZone.Value.IsAmbiguousTime(localStartTime))
        {
            throw CinemaApplicationException.Invalid(
                "StartTime does not identify a unique valid local time in the cinema time zone.");
        }

        var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(localStartTime, CinemaTimeZone.Value);
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
            TimeZoneInfo.ConvertTime(showtime.StartTime, CinemaTimeZone.Value).DateTime);
    }
}
