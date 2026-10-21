using CinemaShowtimes.Application.Persistence.Enums;
using CinemaShowtimes.Domain.Entities;

namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed record ShowtimeCreationResult(ShowtimeCreationStatus Status, Showtime? Showtime);
