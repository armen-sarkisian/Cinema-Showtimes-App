using CinemaShowtimes.Application.DTOs.Requests;
using CinemaShowtimes.Application.DTOs.Responses;
using CinemaShowtimes.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.API.Controllers;

[ApiController]
[Route("api/showtimes")]
public sealed class ShowtimesController(ShowtimeApplicationService showtimes) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ShowtimeListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShowtimeListItemResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await showtimes.GetAllShowtimesAsync(cancellationToken);
        return Ok(result);
    }

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