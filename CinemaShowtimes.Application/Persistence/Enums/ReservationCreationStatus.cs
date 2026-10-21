namespace CinemaShowtimes.Application.Persistence.Enums;

public enum ReservationCreationStatus
{
    Created,
    ShowtimeNotFound,
    InvalidSeats,
    NoContiguousSeats,
    SeatUnavailable
}
