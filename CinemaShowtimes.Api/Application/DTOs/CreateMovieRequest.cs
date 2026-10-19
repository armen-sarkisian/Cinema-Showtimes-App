using System.ComponentModel.DataAnnotations;

namespace CinemaShowtimes.Application.DTOs;

public sealed class CreateMovieRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Category { get; init; } = string.Empty;

    public int Year { get; init; }
}
