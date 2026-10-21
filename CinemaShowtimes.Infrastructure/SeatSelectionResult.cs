using CinemaShowtimes.Infrastructure.Enums;

namespace CinemaShowtimes.Infrastructure;

internal sealed record SeatSelectionResult(
    SeatSelectionStatus Status,
    IReadOnlyList<int>? SeatIds = null);
