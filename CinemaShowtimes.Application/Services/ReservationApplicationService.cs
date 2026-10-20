using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Application.Persistence;
using CinemaShowtimes.Domain.Enums;

namespace CinemaShowtimes.Application.Services;

public sealed class ReservationApplicationService(
    IReservationOperations reservationOperations)
{
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(10);

    public Task<ReservationResponse> CreateReservationAsync(
        int showtimeId,
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SeatIds is null || request.SeatIds.Count == 0)
        {
            throw CinemaApplicationException.Invalid("At least one seat must be selected.");
        }

        if (request.SeatIds.Any(seatId => seatId <= 0))
        {
            throw CinemaApplicationException.Invalid("Seat IDs must be positive integers.");
        }

        if (request.SeatIds.Distinct().Count() != request.SeatIds.Count)
        {
            throw CinemaApplicationException.Invalid("Seat IDs must not contain duplicates.");
        }

        return CreateReservationAsync(
            showtimeId,
            new ReservationSeatSelection.Explicit(request.SeatIds),
            cancellationToken);
    }

    public Task<ReservationResponse> CreateContiguousReservationAsync(
        int showtimeId,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            throw CinemaApplicationException.Invalid("Seat count must be greater than zero.");
        }

        return CreateReservationAsync(
            showtimeId,
            new ReservationSeatSelection.Contiguous(count),
            cancellationToken);
    }

    private async Task<ReservationResponse> CreateReservationAsync(
        int showtimeId,
        ReservationSeatSelection selection,
        CancellationToken cancellationToken)
    {
        var result = await reservationOperations.CreateAsync(
            showtimeId,
            selection,
            ReservationLifetime,
            cancellationToken);

        switch (result.Status)
        {
            case ReservationCreationStatus.Created:
                if (result.Reservation is null || result.Movie is null || result.Auditorium is null)
                {
                    throw new InvalidOperationException(
                        "The reservation operation returned an incomplete created reservation.");
                }

                return new ReservationResponse(
                    result.Reservation.Reference,
                    result.SeatCount,
                    new MovieSummaryResponse(
                        result.Movie.Id,
                        result.Movie.Title,
                        result.Movie.Category,
                        result.Movie.Year),
                    new AuditoriumSummaryResponse(result.Auditorium.Id, result.Auditorium.Name),
                    result.Reservation.CreatedAt,
                    result.Reservation.ExpiresAt);

            case ReservationCreationStatus.ShowtimeNotFound:
                throw CinemaApplicationException.NotFound($"Showtime with ID {showtimeId} was not found.");

            case ReservationCreationStatus.InvalidSeats:
                throw CinemaApplicationException.Invalid(
                    "Every seat must exist and belong to the showtime's auditorium.");

            case ReservationCreationStatus.NoContiguousSeats:
                if (selection is not ReservationSeatSelection.Contiguous contiguous)
                {
                    throw new InvalidOperationException(
                        "An explicit seat selection cannot return a contiguous-seat failure.");
                }

                throw CinemaApplicationException.Conflict(
                    $"No contiguous block of {contiguous.Count} available seats exists.");

            case ReservationCreationStatus.SeatUnavailable:
                throw CinemaApplicationException.Conflict(
                    $"Seat {result.ConflictingSeatId} is currently reserved or has already been sold.");

            default:
                throw new InvalidOperationException(
                    $"Unexpected reservation creation status: {result.Status}.");
        }
    }

    public async Task<ReservationConfirmationResponse> ConfirmReservationAsync(
        Guid reference,
        CancellationToken cancellationToken)
    {
        var result = await reservationOperations.ConfirmAsync(
            reference,
            cancellationToken);

        switch (result)
        {
            case ReservationConfirmationStatus.Confirmed:
                return new ReservationConfirmationResponse(reference, ReservationStatus.Confirmed.ToString());
            case ReservationConfirmationStatus.NotFound:
                throw CinemaApplicationException.NotFound($"Reservation {reference} was not found.");
            case ReservationConfirmationStatus.NotReserved:
                throw CinemaApplicationException.Conflict(
                    "Only a reservation with Reserved status can be confirmed.");
            case ReservationConfirmationStatus.Expired:
                throw CinemaApplicationException.Conflict("The reservation has expired.");
            case ReservationConfirmationStatus.AssignmentMismatch:
                throw CinemaApplicationException.Conflict(
                    "The reservation expired or no longer owns all of its recorded seats.");
            default:
                throw new InvalidOperationException(
                    $"Unexpected reservation confirmation status: {result}.");
        }
    }
}
