using CinemaShowtimes.Domain.Enums;

namespace CinemaShowtimes.Domain.Entities;

public sealed class Reservation
{
    public int Id { get; set; }
    public Guid Reference { get; set; }
    public int ShowtimeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public ReservationStatus Status { get; set; }

    public Showtime Showtime { get; set; } = null!;
    public ICollection<ReservationSeat> Seats { get; set; } = new List<ReservationSeat>();
    public ICollection<ShowtimeSeat> CurrentSeats { get; set; } = new List<ShowtimeSeat>();
}
