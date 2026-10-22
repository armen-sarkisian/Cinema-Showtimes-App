using CinemaShowtimes.Domain.Enums;

namespace CinemaShowtimes.Domain.Rules;

public static class ReservationAvailabilityRules
{
    public static bool IsActive(
        ReservationStatus status,
        DateTimeOffset expiresAt,
        DateTimeOffset now) =>
        status == ReservationStatus.Reserved && now < expiresAt;

    public static bool HasReleasedSeat(
        ReservationStatus status,
        DateTimeOffset expiresAt,
        DateTimeOffset now) =>
        status == ReservationStatus.Expired
        || status == ReservationStatus.Reserved && now >= expiresAt;
}
