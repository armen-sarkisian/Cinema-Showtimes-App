namespace CinemaShowtimes.Domain.Entities;

public sealed class ReservationSeat
{
    public int ReservationId { get; set; }
    public int ShowtimeId { get; set; }
    public int SeatId { get; set; }

    public Reservation Reservation { get; set; } = null!;
    public ShowtimeSeat ShowtimeSeat { get; set; } = null!;
}
