namespace CinemaShowtimes.Application.Errors;

public sealed class CinemaApplicationException(
    CinemaApplicationErrorKind kind,
    string code,
    string title,
    string message) : Exception(message)
{
    public CinemaApplicationErrorKind Kind { get; } = kind;
    public string Code { get; } = code;
    public string Title { get; } = title;

    public static CinemaApplicationException Invalid(string message) =>
        new(CinemaApplicationErrorKind.InvalidRequest, "invalid_request", "Invalid request", message);

    public static CinemaApplicationException NotFound(string message) =>
        new(CinemaApplicationErrorKind.NotFound, "not_found", "Resource not found", message);

    public static CinemaApplicationException Conflict(string message) =>
        new(CinemaApplicationErrorKind.Conflict, "conflict", "Reservation conflict", message);
}

public enum CinemaApplicationErrorKind
{
    InvalidRequest,
    NotFound,
    Conflict
}
