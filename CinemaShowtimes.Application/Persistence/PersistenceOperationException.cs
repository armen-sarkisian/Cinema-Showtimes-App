namespace CinemaShowtimes.Application.Persistence;

public enum PersistenceFailure
{
    TemporarilyUnavailable,
    Conflict
}

public sealed class PersistenceOperationException(
    PersistenceFailure failure,
    string message,
    Exception innerException) : Exception(message, innerException)
{
    public PersistenceFailure Failure { get; } = failure;
}
