using Microsoft.Data.SqlClient;

namespace Sqlm.Core.Mssql;

/// <summary>
/// Opens a connection to the source SQL Server. The shipping path is always Windows SSO
/// (<c>Integrated Security=True</c>, PLAN.md §1.1) — there is no username/password overload here.
/// The SQL-auth path used to reach the Testcontainers SQL Server in CI (PLAN.md §11) lives only in
/// the test projects, as a separate <c>IMssqlConnectionFactory</c> implementation, so it never ships
/// inside <c>Sqlm.Core</c> or is reachable from <c>Sqlm.App</c>.
/// </summary>
public interface IMssqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(string server, string database, CancellationToken cancellationToken);
}

public sealed class WindowsSsoConnectionFactory : IMssqlConnectionFactory
{
    public async Task<SqlConnection> OpenAsync(string server, string database, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(BuildConnectionString(server, database));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    /// <summary>
    /// Split out from <see cref="OpenAsync"/> so the connection-string shape (PLAN.md §1.1, §8:
    /// integrated security, TLS on, no credential fields) is unit-testable without a live server.
    /// </summary>
    public static string BuildConnectionString(string server, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            IntegratedSecurity = true,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 15,
        };

        return builder.ConnectionString;
    }
}
