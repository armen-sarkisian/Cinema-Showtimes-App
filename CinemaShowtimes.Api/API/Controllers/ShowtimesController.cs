using CinemaShowtimes.Application.DTOs.Requests;
using CinemaShowtimes.Application.DTOs.Responses;
using CinemaShowtimes.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.API.Controllers;

[ApiController]
[Route("api/showtimes")]
public sealed class ShowtimesController(ShowtimeApplicationService showtimes) : ControllerBase
{
    /// <summary>Gets all showtimes ordered by their local cinema start time.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>Showtimes with their movie and auditorium details.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShowtimeListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShowtimeListItemResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await showtimes.GetAllShowtimesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a showtime.</summary>
    /// <param name="request">The movie, auditorium, and local cinema start time.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The created showtime.</returns>
    [HttpPost]
    [ProducesResponseType<ShowtimeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShowtimeResponse>> Create(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var showtime = await showtimes.CreateShowtimeAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, showtime);
    }
}