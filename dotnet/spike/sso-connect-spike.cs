#:package Microsoft.Data.SqlClient@7.0.2
#:package Microsoft.Data.Sqlite@10.0.10
#:package SQLitePCLRaw.bundle_e_sqlite3@3.0.5

using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

// ---------------------------------------------------------------------------
// Throwaway spike — PLAN.md §14.4.
//
// Proves the riskiest path in the architecture end to end: connect to MSSQL,
// read one table with CommandBehavior.SequentialAccess (streaming the large
// value, not buffering it), push rows through a bounded Channel, and batch
// -insert into SQLite under the load-time pragmas from §5.3. Not shipped
// code — delete this directory once the assumptions below are confirmed.
//
// Windows auth (the real, only, shipping auth mode — §1.1) has no token to
// present from this Linux container, so the connection step falls back to
// the test-only SQL-auth path from §11/§13 here. Everything downstream of
// OpenAsync() — SequentialAccess, streaming, the channel, SQLite writes — is
// identical to the production path; only BuildMssqlConnectionString differs.
// The SSPI handshake itself still needs sqllocaldb on a Windows host or the
// Windows CI job (§13) before this risk is fully retired.
// ---------------------------------------------------------------------------

const string SourceTable = "dbo.SqlmSpikeSource";

var sw = Stopwatch.StartNew();
var mssqlConnectionString = BuildMssqlConnectionString();
var sqlitePath = Path.Combine(Path.GetTempPath(), $"sqlm-spike-{Guid.NewGuid():N}.db");

try
{
    await using var mssql = new SqlConnection(mssqlConnectionString);
    Console.WriteLine($"[mssql] connecting ({(OperatingSystem.IsWindows() ? "Windows SSO" : "test-only SQL auth")})...");
    await mssql.OpenAsync();
    Console.WriteLine($"[mssql] connected. server={mssql.DataSource} database={mssql.Database}");

    await SeedSourceTableAsync(mssql, SourceTable);

    var sourceRows = await CopyTableThroughChannelAsync(mssql, SourceTable, sqlitePath);
    var targetRows = await CountSqliteRowsAsync(sqlitePath, "spike_source");

    Console.WriteLine();
    if (sourceRows == targetRows)
    {
        Console.WriteLine($"PASS  {sourceRows} rows round-tripped in {sw.ElapsedMilliseconds} ms");
    }
    else
    {
        Console.WriteLine($"FAIL  source={sourceRows} target={targetRows}");
        Environment.ExitCode = 1;
    }
}
finally
{
    await DropSourceTableAsync(mssqlConnectionString, SourceTable);
    if (File.Exists(sqlitePath)) File.Delete(sqlitePath);
}

static string BuildMssqlConnectionString()
{
    if (OperatingSystem.IsWindows())
    {
        // Real path: PLAN.md §1.1 — Windows auth only, passwordless, never prompts.
        var server = Environment.GetEnvironmentVariable("SQLM_SPIKE_SERVER") ?? @"(localdb)\MSSQLLocalDB";
        return $"Server={server};Database=master;Integrated Security=True;Encrypt=True;" +
               "TrustServerCertificate=False;Connect Timeout=15";
    }

    // Linux dev container: no Windows logon token exists to hand SSPI (§11/§13).
    var testConnectionString = Environment.GetEnvironmentVariable("SQLM_TEST_MSSQL");
    if (string.IsNullOrEmpty(testConnectionString))
    {
        throw new InvalidOperationException(
            "SQLM_TEST_MSSQL is not set — devcontainer.json should provide it automatically.");
    }

    Console.WriteLine("[mssql] Linux container: using test-only SQL auth, NOT the real SSO path.");
    return testConnectionString;
}

static async Task SeedSourceTableAsync(SqlConnection mssql, string table)
{
    await using (var drop = mssql.CreateCommand())
    {
        drop.CommandText = $"IF OBJECT_ID('{table}', 'U') IS NOT NULL DROP TABLE {table};";
        await drop.ExecuteNonQueryAsync();
    }

    await using (var create = mssql.CreateCommand())
    {
        create.CommandText = $"""
            CREATE TABLE {table} (
                Id INT NOT NULL PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Amount DECIMAL(18,4) NOT NULL,
                Payload VARBINARY(MAX) NULL,
                CreatedAt DATETIME2(7) NOT NULL
            );
            """;
        await create.ExecuteNonQueryAsync();
    }

    const int RowCount = 500;
    const int LargePayloadRow = 250; // proves GetStream() never buffers a whole large value

    await using var insert = mssql.CreateCommand();
    insert.CommandText = $"""
        INSERT INTO {table} (Id, Name, Amount, Payload, CreatedAt)
        VALUES (@id, @name, @amount, @payload, @createdAt);
        """;
    var idParam = insert.Parameters.Add("@id", SqlDbType.Int);
    var nameParam = insert.Parameters.Add("@name", SqlDbType.NVarChar, 100);
    var amountParam = insert.Parameters.Add("@amount", SqlDbType.Decimal);
    amountParam.Precision = 18;
    amountParam.Scale = 4;
    var payloadParam = insert.Parameters.Add("@payload", SqlDbType.VarBinary, -1);
    var createdAtParam = insert.Parameters.Add("@createdAt", SqlDbType.DateTime2);

    for (var i = 1; i <= RowCount; i++)
    {
        idParam.Value = i;
        nameParam.Value = $"spike-row-{i}";
        amountParam.Value = Math.Round(i * 1.3037m, 4);
        createdAtParam.Value = DateTime.UtcNow.AddSeconds(-i);

        // Every 10th row exercises NULL; row 250 carries an 8 MB blob.
        if (i % 10 == 0)
        {
            payloadParam.Value = DBNull.Value;
        }
        else
        {
            var payload = new byte[i == LargePayloadRow ? 8 * 1024 * 1024 : 16];
            Random.Shared.NextBytes(payload);
            payloadParam.Value = payload;
        }

        await insert.ExecuteNonQueryAsync();
    }

    Console.WriteLine($"[mssql] seeded {RowCount} rows into {table}");
}

static async Task<long> CopyTableThroughChannelAsync(SqlConnection mssql, string sourceTable, string sqlitePath)
{
    const int BatchSize = 50;
    var channel = Channel.CreateBounded<object?[][]>(new BoundedChannelOptions(4)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    var readTask = ReadSourceAsync(mssql, sourceTable, BatchSize, channel.Writer);
    var writeTask = WriteSqliteAsync(sqlitePath, channel.Reader);

    var rowsRead = await readTask;
    var rowsWritten = await writeTask;

    if (rowsRead != rowsWritten)
        throw new InvalidOperationException($"pipeline dropped rows: read={rowsRead} written={rowsWritten}");

    return rowsRead;
}

static async Task<long> ReadSourceAsync(SqlConnection mssql, string table, int batchSize, ChannelWriter<object?[][]> writer)
{
    long total = 0;
    Exception? failure = null;
    try
    {
        await using var cmd = mssql.CreateCommand();
        cmd.CommandText = $"SELECT Id, Name, Amount, Payload, CreatedAt FROM {table} ORDER BY Id;";

        // The point of the spike: SequentialAccess + a streamed GetStream() read means
        // the 8 MB row never gets fully materialized inside the reader.
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

        var batch = new List<object?[]>(batchSize);
        while (await reader.ReadAsync())
        {
            // Columns must be read in ordinal order under SequentialAccess — no going back.
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var amount = (decimal)reader.GetSqlDecimal(2); // exact accessor, PLAN.md §5.1
            var payload = await ReadPayloadStreamedAsync(reader, ordinal: 3);
            var createdAt = reader.GetDateTime(4);

            batch.Add([id, name, amount, payload, createdAt]);
            total++;

            if (batch.Count == batchSize)
            {
                await writer.WriteAsync(batch.ToArray());
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await writer.WriteAsync(batch.ToArray());
    }
    catch (Exception ex)
    {
        failure = ex;
        throw;
    }
    finally
    {
        writer.Complete(failure);
    }

    return total;
}

static async Task<byte[]?> ReadPayloadStreamedAsync(SqlDataReader reader, int ordinal)
{
    if (await reader.IsDBNullAsync(ordinal)) return null;

    // Chunked copy — the reader-side buffer stays bounded regardless of value size.
    // (Re-materializing into a byte[] here is for the SQLite blob *parameter*, which
    // Microsoft.Data.Sqlite requires; true zero-copy end-to-end would use SQLite's
    // incremental blob I/O and is a §7 perf-phase concern, not this spike's risk.)
    await using var source = reader.GetStream(ordinal);
    var buffer = new MemoryStream();
    await source.CopyToAsync(buffer, bufferSize: 8192);
    return buffer.ToArray();
}

static async Task<long> WriteSqliteAsync(string sqlitePath, ChannelReader<object?[][]> reader)
{
    await using var sqlite = new SqliteConnection($"Data Source={sqlitePath}");
    await sqlite.OpenAsync();

    // Load-time pragmas, PLAN.md §5.3 — only ever applied to a file this app is building.
    await ExecuteAsync(sqlite, "PRAGMA journal_mode = OFF;");
    await ExecuteAsync(sqlite, "PRAGMA synchronous = OFF;");
    await ExecuteAsync(sqlite, "PRAGMA temp_store = MEMORY;");

    await ExecuteAsync(sqlite, """
        CREATE TABLE spike_source (
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            amount TEXT NOT NULL,
            payload BLOB,
            created_at TEXT NOT NULL
        );
        """);

    // One command, prepared once; parameters rebound per row (§5.3).
    await using var insert = sqlite.CreateCommand();
    insert.CommandText = """
        INSERT INTO spike_source (id, name, amount, payload, created_at)
        VALUES ($id, $name, $amount, $payload, $created_at);
        """;
    var idP = insert.Parameters.Add("$id", SqliteType.Integer);
    var nameP = insert.Parameters.Add("$name", SqliteType.Text);
    var amountP = insert.Parameters.Add("$amount", SqliteType.Text);
    var payloadP = insert.Parameters.Add("$payload", SqliteType.Blob);
    var createdAtP = insert.Parameters.Add("$created_at", SqliteType.Text);
    insert.Prepare();

    long total = 0;
    await foreach (var batch in reader.ReadAllAsync())
    {
        await using var tx = sqlite.BeginTransaction();
        insert.Transaction = tx;

        foreach (var row in batch)
        {
            idP.Value = row[0]!;
            nameP.Value = row[1]!;
            amountP.Value = ((decimal)row[2]!).ToString(CultureInfo.InvariantCulture); // TEXT default, §5.1
            payloadP.Value = row[3] ?? (object)DBNull.Value;
            createdAtP.Value = ((DateTime)row[4]!).ToString("O");
            await insert.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        total += batch.Length;
    }

    await ExecuteAsync(sqlite, "PRAGMA synchronous = NORMAL;"); // finalize step, §5.2 step 8

    return total;
}

static async Task ExecuteAsync(SqliteConnection conn, string sql)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    await cmd.ExecuteNonQueryAsync();
}

static async Task<long> CountSqliteRowsAsync(string path, string table)
{
    await using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
    return (long)(await cmd.ExecuteScalarAsync())!;
}

static async Task DropSourceTableAsync(string connectionString, string table)
{
    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"IF OBJECT_ID('{table}', 'U') IS NOT NULL DROP TABLE {table};";
        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[cleanup] warning: failed to drop {table}: {ex.Message}");
    }
}
