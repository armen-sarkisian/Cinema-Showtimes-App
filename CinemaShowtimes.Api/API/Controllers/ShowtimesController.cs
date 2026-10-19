using CinemaShowtimes.Application;
using CinemaShowtimes.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.Controllers;

[ApiController]
[Route("api/showtimes")]
public sealed class ShowtimesController(CinemaApplicationService cinema) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ShowtimeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShowtimeResponse>> Create(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        var showtime = await cinema.CreateShowtimeAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, showtime);
    }
}
