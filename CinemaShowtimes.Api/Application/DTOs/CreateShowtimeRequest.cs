using System.ComponentModel.DataAnnotations;

namespace CinemaShowtimes.Application.DTOs;

public sealed class CreateShowtimeRequest
{
    [Range(1, int.MaxValue)]
    public int MovieId { get; init; }

    [Range(1, int.MaxValue)]
    public int AuditoriumId { get; init; }

    public DateTime StartTime { get; init; }
}
