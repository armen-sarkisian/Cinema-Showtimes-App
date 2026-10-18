namespace CinemaShowtimes.Domain.Entities;

public sealed class Seat
{
    public int Id { get; set; }
    public int AuditoriumId { get; set; }
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }

    public Auditorium Auditorium { get; set; } = null!;
    public ICollection<ShowtimeSeat> ShowtimeSeats { get; set; } = new List<ShowtimeSeat>();
}
