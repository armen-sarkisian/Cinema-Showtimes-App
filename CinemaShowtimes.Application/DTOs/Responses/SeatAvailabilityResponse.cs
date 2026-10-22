namespace CinemaShowtimes.Application.DTOs.Responses;

public sealed record SeatAvailabilityResponse(
    int SeatId,
    string Row,
    int Number,
    string Status);
