using System.ComponentModel.DataAnnotations;

namespace CinemaShowtimes.Application.DTOs.Requests;

public sealed class CreateContiguousReservationRequest
{
    [Range(1, int.MaxValue)]
    public int Count { get; init; }
}
