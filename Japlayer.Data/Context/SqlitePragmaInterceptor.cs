using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Japlayer.Data.Context;

/// <summary>
/// Configures SQLite connections with WAL journaling and a busy timeout for safe concurrent access.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string PragmaSql = """
        PRAGMA journal_mode=WAL;
        PRAGMA busy_timeout=10000;
        PRAGMA synchronous=NORMAL;
        """;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmaSql;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PragmaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
