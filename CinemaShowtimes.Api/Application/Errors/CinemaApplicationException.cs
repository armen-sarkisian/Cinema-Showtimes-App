namespace CinemaShowtimes.Application.Errors;

public sealed class CinemaApplicationException(
    int statusCode,
    string code,
    string title,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public string Title { get; } = title;

    public static CinemaApplicationException Invalid(string message) =>
        new(StatusCodes.Status400BadRequest, "invalid_request", "Invalid request", message);

    public static CinemaApplicationException NotFound(string message) =>
        new(StatusCodes.Status404NotFound, "not_found", "Resource not found", message);
}
