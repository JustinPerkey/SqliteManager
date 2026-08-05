using Microsoft.Data.Sqlite;

namespace Sqlm.Core.Sqlite;

/// <summary>
/// The load-time / durable pragma pairs from PLAN.md §5.3. Load pragmas trade durability for
/// throughput while <c>Sqlm.App</c> is building a file; durable pragmas restore safe defaults at
/// finalize. <b>Only ever applied to a file the app is building — never to a user-supplied
/// database opened for browsing.</b>
/// </summary>
public static class LoadPragmas
{
    public static async Task ApplyLoadPragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, "PRAGMA journal_mode = OFF;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA synchronous = OFF;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA temp_store = MEMORY;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA cache_size = -256000;", cancellationToken).ConfigureAwait(false);
    }

    public static async Task ApplyDurablePragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA synchronous = NORMAL;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
