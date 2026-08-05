using FluentAssertions;
using Microsoft.Data.SqlClient;
using Sqlm.Core.Mssql;

namespace Sqlm.Core.Tests;

/// <summary>
/// PLAN.md §1.1: Windows authentication only, passwordless — never a username, password, or
/// token in the connection string.
/// </summary>
public class WindowsSsoConnectionFactoryTests
{
    [Fact]
    public void BuildConnectionString_uses_integrated_security_and_encrypts()
    {
        var builder = new SqlConnectionStringBuilder(
            WindowsSsoConnectionFactory.BuildConnectionString(@"SQL01\PROD", "Northwind"));

        builder.IntegratedSecurity.Should().Be(true);
        builder.Encrypt.Should().Be(SqlConnectionEncryptOption.Mandatory);
        builder.TrustServerCertificate.Should().Be(false);
        builder.DataSource.Should().Be(@"SQL01\PROD");
        builder.InitialCatalog.Should().Be("Northwind");
    }

    [Fact]
    public void BuildConnectionString_never_contains_a_credential_field()
    {
        var connectionString = WindowsSsoConnectionFactory.BuildConnectionString(@"SQL01\PROD", "Northwind");

        connectionString.Should().NotContain("Password", "the app stores no credentials anywhere (PLAN.md §8)");
        connectionString.Should().NotContain("User ID");
    }
}
