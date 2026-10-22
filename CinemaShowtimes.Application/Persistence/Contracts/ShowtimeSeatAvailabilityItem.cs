using CinemaShowtimes.Domain.Enums;

namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed record ShowtimeSeatAvailabilityItem(
    int SeatId,
    string Row,
    int Number,
    ReservationStatus? ReservationStatus,
    DateTimeOffset? ReservationExpiresAt);
