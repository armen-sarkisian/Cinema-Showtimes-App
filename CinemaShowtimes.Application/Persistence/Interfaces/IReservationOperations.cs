using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Persistence;

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

public abstract record ReservationSeatSelection
{
    public sealed record Explicit(IReadOnlyList<int> SeatIds) : ReservationSeatSelection;
    public sealed record Contiguous(int Count) : ReservationSeatSelection;
}

public enum ReservationCreationStatus
{
    Created,
    ShowtimeNotFound,
    InvalidSeats,
    NoContiguousSeats,
    SeatUnavailable
}

public sealed record ReservationCreationResult(
    ReservationCreationStatus Status,
    Reservation? Reservation = null,
    Movie? Movie = null,
    Auditorium? Auditorium = null,
    int SeatCount = 0,
    int? ConflictingSeatId = null);

public enum ReservationConfirmationStatus
{
    Confirmed,
    NotFound,
    NotReserved,
    Expired,
    AssignmentMismatch
}
