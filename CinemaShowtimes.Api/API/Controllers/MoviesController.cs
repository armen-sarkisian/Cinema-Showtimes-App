using CinemaShowtimes.Application.Services;
using CinemaShowtimes.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(MovieApplicationService movies) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MovieResponse>> Create(
        CreateMovieRequest request,
        CancellationToken cancellationToken)
    {
        var movie = await movies.CreateMovieAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, movie);
    }
}
