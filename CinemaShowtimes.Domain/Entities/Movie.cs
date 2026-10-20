namespace CinemaShowtimes.Domain.Entities;

public sealed class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Year { get; set; }

    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
