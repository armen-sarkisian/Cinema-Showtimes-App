namespace CinemaShowtimes.Domain.Entities;

public sealed class ShowtimeSeat
{
    public int ShowtimeId { get; set; }
    public int SeatId { get; set; }
    public int? CurrentReservationId { get; set; }

    public Showtime Showtime { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
    public Reservation? CurrentReservation { get; set; }
    public ICollection<ReservationSeat> ReservationHistory { get; set; } = new List<ReservationSeat>();
}
