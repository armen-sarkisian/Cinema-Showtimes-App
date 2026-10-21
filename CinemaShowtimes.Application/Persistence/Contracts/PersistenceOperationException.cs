using CinemaShowtimes.Application.Persistence.Enums;

namespace CinemaShowtimes.Application.Persistence.Contracts;

public sealed class PersistenceOperationException(
    PersistenceFailure failure,
    string message,
    Exception innerException) : Exception(message, innerException)
{
    public PersistenceFailure Failure { get; } = failure;
}
