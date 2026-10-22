using CinemaShowtimes.Application.Persistence.Contracts;
using CinemaShowtimes.Application.Persistence.Interfaces;
using CinemaShowtimes.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Infrastructure;

public sealed class EfCoreShowtimeSeatAvailabilityStore(CinemaDbContext dbContext)
    : IShowtimeSeatAvailabilityStore
{
    public async Task<IReadOnlyList<ShowtimeSeatAvailabilityItem>?> GetAsync(
        int showtimeId,
        CancellationToken cancellationToken)
    {
        var auditoriumId = await dbContext.Showtimes
            .AsNoTracking()
            .Where(showtime => showtime.Id == showtimeId)
            .Select(showtime => (int?)showtime.AuditoriumId)
            .SingleOrDefaultAsync(cancellationToken);
        if (auditoriumId is null)
        {
            return null;
        }

        var assignments = dbContext.ShowtimeSeats
            .AsNoTracking()
            .Where(assignment => assignment.ShowtimeId == showtimeId)
            .Select(assignment => new
            {
                assignment.SeatId,
                ReservationStatus = assignment.CurrentReservation == null
                    ? (ReservationStatus?)null
                    : assignment.CurrentReservation.Status,
                ReservationExpiresAt = assignment.CurrentReservation == null
                    ? (DateTimeOffset?)null
                    : assignment.CurrentReservation.ExpiresAt
            });

        return await (
            from seat in dbContext.Seats.AsNoTracking()
            where seat.AuditoriumId == auditoriumId.Value
            join assignment in assignments on seat.Id equals assignment.SeatId into seatAssignments
            from assignment in seatAssignments.DefaultIfEmpty()
            orderby seat.Row, seat.Number
            select new ShowtimeSeatAvailabilityItem(
                seat.Id,
                seat.Row,
                seat.Number,
                assignment == null ? null : assignment.ReservationStatus,
                assignment == null ? null : assignment.ReservationExpiresAt))
            .ToListAsync(cancellationToken);
    }
}
