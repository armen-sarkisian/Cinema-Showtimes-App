namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed record ExplicitReservationSeatSelection(IReadOnlyList<int> SeatIds)
    : ReservationSeatSelection;
