using CinemaShowtimes.Application.Persistence;
using CinemaShowtimes.Application.Persistence.Contracts;
using CinemaShowtimes.Application.Persistence.Enums;
using CinemaShowtimes.Application.Persistence.Interfaces;
using CinemaShowtimes.Domain.Entities;
using Microsoft.Data.Sqlite;
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

            if (await HasShowtimeAtSameTimeAsync(showtime, cancellationToken))
            {
                return new ShowtimeCreationResult(
                    ShowtimeCreationStatus.AuditoriumAlreadyHasShowtime,
                    null);
            }

            dbContext.Showtimes.Add(showtime);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqliteException
                {
                    SqliteExtendedErrorCode: 2067
                })
            {
                dbContext.Entry(showtime).State = EntityState.Detached;
                if (await HasShowtimeAtSameTimeAsync(showtime, cancellationToken))
                {
                    return new ShowtimeCreationResult(
                        ShowtimeCreationStatus.AuditoriumAlreadyHasShowtime,
                        null);
                }

                throw;
            }

            return new ShowtimeCreationResult(ShowtimeCreationStatus.Created, showtime);
        }
        catch (Exception exception) when (SqliteExceptionTranslator.IsExpected(exception))
        {
            throw SqliteExceptionTranslator.Translate(exception);
        }
    }

    private Task<bool> HasShowtimeAtSameTimeAsync(
        Showtime showtime,
        CancellationToken cancellationToken) =>
        dbContext.Showtimes.AnyAsync(
            existing => existing.AuditoriumId == showtime.AuditoriumId
                && existing.StartTime == showtime.StartTime,
            cancellationToken);
}
