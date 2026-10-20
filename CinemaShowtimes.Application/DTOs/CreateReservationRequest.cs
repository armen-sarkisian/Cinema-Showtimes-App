using System.ComponentModel.DataAnnotations;

namespace CinemaShowtimes.Application.DTOs;

public sealed class CreateReservationRequest
{
    [Required]
    [MinLength(1)]
    public List<int> SeatIds { get; init; } = [];
}

public sealed class CreateContiguousReservationRequest
{
    [Range(1, int.MaxValue)]
    public int Count { get; init; }
}
