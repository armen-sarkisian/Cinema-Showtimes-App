using CinemaShowtimes.Application;
using CinemaShowtimes.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.Controllers;

[ApiController]
[Route("api/showtimes/{showtimeId:int}/reservations")]
public sealed class ReservationsController(ReservationApplicationService reservations) : ControllerBase
{
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
