using CinemaShowtimes.Application.Services;
using CinemaShowtimes.Application.DTOs;
using CinemaShowtimes.Application.DTOs.Requests;
using CinemaShowtimes.Application.DTOs.Responses;
using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(MovieApplicationService movies) : ControllerBase
{
    /// <summary>Creates a movie.</summary>
    /// <param name="request">The movie details.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The created movie.</returns>
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
