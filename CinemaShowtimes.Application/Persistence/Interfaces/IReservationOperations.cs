using CinemaShowtimes.Application.Persistence.Contracts;
using CinemaShowtimes.Application.Persistence.Enums;

namespace CinemaShowtimes.Application.Persistence.Interfaces;

public interface IReservationOperations
{
    Task<ReservationCreationResult> CreateAsync(
        int showtimeId,
        ReservationSeatSelection selection,
        TimeSpan reservationLifetime,
        CancellationToken cancellationToken);

    Task<ReservationConfirmationStatus> ConfirmAsync(
        Guid reference,
        CancellationToken cancellationToken);
}
