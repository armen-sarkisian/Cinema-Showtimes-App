using CinemaShowtimes.Application.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CinemaShowtimes.Infrastructure;

internal static class SqliteExceptionTranslator
{
    public static bool IsExpected(Exception exception) => FindSqliteException(exception) is not null;

    public static PersistenceOperationException Translate(Exception exception)
    {
        var sqliteException = FindSqliteException(exception)
            ?? throw new ArgumentException("The exception is not a mapped SQLite failure.", nameof(exception));
        var failure = sqliteException.SqliteErrorCode is 5 or 6
            ? PersistenceFailure.TemporarilyUnavailable
            : PersistenceFailure.Conflict;
        var message = failure == PersistenceFailure.TemporarilyUnavailable
            ? "The database is busy. Retry the request."
            : "The operation conflicts with the current database state.";
        return new PersistenceOperationException(failure, message, exception);
    }

    private static SqliteException? FindSqliteException(Exception exception)
    {
        var sqliteException = exception as SqliteException
            ?? (exception as DbUpdateException)?.InnerException as SqliteException;
        return sqliteException?.SqliteErrorCode is 5 or 6 or 19
            ? sqliteException
            : null;
    }
}
