using CinemaShowtimes.Application;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.DTOs.Requests;
using CinemaShowtimes.Application.DTOs.Responses;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Application.Persistence.Enums;
using CinemaShowtimes.Application.Persistence.Interfaces;
using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Services;

public sealed class ShowtimeApplicationService(
    IShowtimeCreationStore showtimes,
    TimeProvider timeProvider)
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

        var result = await showtimes.CreateAsync(showtime, cancellationToken);
        showtime = result.Status switch
        {
            ShowtimeCreationStatus.Created => result.Showtime
                ?? throw new InvalidOperationException("The showtime store returned no created showtime."),
            ShowtimeCreationStatus.MovieNotFound =>
                throw CinemaApplicationException.NotFound($"Movie with ID {request.MovieId} was not found."),
            ShowtimeCreationStatus.AuditoriumNotFound =>
                throw CinemaApplicationException.NotFound(
                    $"Auditorium with ID {request.AuditoriumId} was not found."),
            _ => throw new InvalidOperationException(
                $"Unexpected showtime creation status: {result.Status}.")
        };

        return new ShowtimeResponse(
            showtime.Id,
            showtime.MovieId,
            showtime.AuditoriumId,
            TimeZoneInfo.ConvertTime(showtime.StartTime, CinemaTimeZone.Value).DateTime);
    }
}
