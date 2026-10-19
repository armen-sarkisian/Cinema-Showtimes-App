using CinemaShowtimes.Application.Errors;

namespace CinemaShowtimes.Api.Errors;

public sealed class CinemaExceptionMiddleware(RequestDelegate next)
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

            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(
                ApiProblemFactory.Create(
                    context,
                    exception.StatusCode,
                    exception.Title,
                    exception.Message,
                    exception.Code),
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }
}
