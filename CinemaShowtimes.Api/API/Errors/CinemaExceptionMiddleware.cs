using CinemaShowtimes.Application.Errors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        catch (Exception exception) when (GetExpectedSqliteException(exception) is not null)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var sqliteException = GetExpectedSqliteException(exception)!;
            var isBusy = sqliteException.SqliteErrorCode is 5 or 6;
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
                "SQLite operation failed with error code {SqliteErrorCode}.",
                sqliteException.SqliteErrorCode);
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

    private static SqliteException? GetExpectedSqliteException(Exception exception)
    {
        var sqliteException = exception as SqliteException
            ?? (exception as DbUpdateException)?.InnerException as SqliteException;
        return sqliteException?.SqliteErrorCode is 5 or 6 or 19
            ? sqliteException
            : null;
    }
}
