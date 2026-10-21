using System.ComponentModel.DataAnnotations;

namespace CinemaShowtimes.Application.DTOs;

public sealed class CreateReservationRequest
{
    [Required]
    [MinLength(1)]
    public List<int> SeatIds { get; init; } = [];
}
