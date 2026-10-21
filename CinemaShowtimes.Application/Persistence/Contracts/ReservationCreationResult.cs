using CinemaShowtimes.Application.Persistence.Enums;
using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed record ReservationCreationResult(
    ReservationCreationStatus Status,
    Reservation? Reservation = null,
    Movie? Movie = null,
    Auditorium? Auditorium = null,
    int SeatCount = 0,
    int? ConflictingSeatId = null);
