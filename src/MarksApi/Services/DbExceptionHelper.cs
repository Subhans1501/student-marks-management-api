using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Services;

public static class DbExceptionHelper
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("23505", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsForeignKeyRestrictViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException pgEx &&
            (pgEx.SqlState == PostgresErrorCodes.ForeignKeyViolation || pgEx.SqlState == PostgresErrorCodes.RestrictViolation))
        {
            return true;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("23503", StringComparison.OrdinalIgnoreCase)
            || message.Contains("23001", StringComparison.OrdinalIgnoreCase)
            || message.Contains("foreign key constraint", StringComparison.OrdinalIgnoreCase)
            || message.Contains("referential integrity", StringComparison.OrdinalIgnoreCase);
    }
}

