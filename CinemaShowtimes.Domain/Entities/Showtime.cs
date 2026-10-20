namespace CinemaShowtimes.Domain.Entities;

public sealed class Showtime
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public int AuditoriumId { get; set; }
    public DateTimeOffset StartTime { get; set; }

    public Movie Movie { get; set; } = null!;
    public Auditorium Auditorium { get; set; } = null!;
    public ICollection<ShowtimeSeat> Seats { get; set; } = new List<ShowtimeSeat>();
}
