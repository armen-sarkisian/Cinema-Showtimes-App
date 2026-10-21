namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed record ContiguousReservationSeatSelection(int Count)
    : ReservationSeatSelection;
