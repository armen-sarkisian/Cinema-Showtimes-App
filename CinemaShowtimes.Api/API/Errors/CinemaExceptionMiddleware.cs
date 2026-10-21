using CinemaShowtimes.Application.Errors;
using CinemaShowtimes.Application.Enums;
using CinemaShowtimes.Application.Persistence.Contracts;
using CinemaShowtimes.Application.Persistence.Enums;

namespace CinemaShowtimes.Api.Errors;

public sealed class CinemaExceptionMiddleware(RequestDelegate next, ILogger<CinemaExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (CinemaApplicationException exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var statusCode = exception.Kind switch
            {
                CinemaApplicationErrorKind.InvalidRequest => StatusCodes.Status400BadRequest,
                CinemaApplicationErrorKind.NotFound => StatusCodes.Status404NotFound,
                CinemaApplicationErrorKind.Conflict => StatusCodes.Status409Conflict,
                _ => throw new InvalidOperationException(
                    $"Unexpected application error kind: {exception.Kind}.")
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(
                ApiProblemFactory.Create(
                    context,
                    statusCode,
                    exception.Title,
                    exception.Message,
                    exception.Code),
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
        catch (PersistenceOperationException exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var isBusy = exception.Failure == PersistenceFailure.TemporarilyUnavailable;
            var statusCode = isBusy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status409Conflict;
            var code = isBusy ? "database_busy" : "database_constraint_conflict";
            var title = isBusy ? "Database temporarily unavailable" : "Database constraint conflict";
            var detail = isBusy
                ? "The database is busy. Retry the request."
                : "The operation conflicts with the current database state.";

            logger.LogWarning(
                exception,
                "Persistence operation failed with classification {Failure}.",
                exception.Failure);
            context.Response.StatusCode = statusCode;
            if (isBusy)
            {
                context.Response.Headers.RetryAfter = "1";
            }

            await context.Response.WriteAsJsonAsync(
                ApiProblemFactory.Create(context, statusCode, title, detail, code),
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }

}
