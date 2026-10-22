using CinemaShowtimes.Application.Services;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.DTOs.Requests;
using CinemaShowtimes.Application.DTOs.Responses;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.Controllers;

[ApiController]
[Route("api/showtimes/{showtimeId:int}/reservations")]
public sealed class ReservationsController(ReservationApplicationService reservations) : ControllerBase
{
    /// <summary>Reserves the specified seats for ten minutes.</summary>
    /// <param name="showtimeId">The showtime to reserve seats for.</param>
    /// <param name="request">The seat IDs to reserve.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The created reservation and its expiration time.</returns>
    [HttpPost]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Create(
        int showtimeId,
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await reservations.CreateReservationAsync(showtimeId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, reservation);
    }

    /// <summary>Reserves the first available contiguous block of seats for ten minutes.</summary>
    /// <param name="showtimeId">The showtime to reserve seats for.</param>
    /// <param name="request">The number of consecutive seats to reserve.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The created reservation and its expiration time.</returns>
    [HttpPost("contiguous")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> CreateContiguous(
        int showtimeId,
        CreateContiguousReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await reservations.CreateContiguousReservationAsync(
            showtimeId,
            request.Count,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, reservation);
    }

    /// <summary>Confirms an active reservation before it expires.</summary>
    /// <param name="reference">The public reservation reference.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The reservation confirmation.</returns>
    [HttpPost("~/api/reservations/{reference:guid}/confirm")]
    [ProducesResponseType<ReservationConfirmationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationConfirmationResponse>> Confirm(
        Guid reference,
        CancellationToken cancellationToken)
    {
        var confirmation = await reservations.ConfirmReservationAsync(reference, cancellationToken);
        return Ok(confirmation);
    }
}
