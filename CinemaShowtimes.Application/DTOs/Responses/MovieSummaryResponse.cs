namespace CinemaShowtimes.Application.DTOs.Responses;

public sealed record MovieSummaryResponse(int Id, string Title, string Category, int Year);
