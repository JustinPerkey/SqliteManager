using Testcontainers.MsSql;

namespace Sqlm.Fidelity.Tests;

/// <summary>
/// PLAN.md §11 type fidelity suite: migrate boundary values and assert exact round-trip against
/// docs/type-mapping.md. Real SQL Server 2022 via <c>Testcontainers.MsSql</c>, SQL auth only —
/// the test-only path from §11/§13, never the shipping Windows-SSO path.
///
/// Body intentionally not yet written: it depends on <c>Sqlm.Core.Migrate</c> (not implemented
/// until Phase 2) and on docs/type-mapping.md (not yet written, PLAN.md §14.2). The container
/// fixture below is real so the suite is ready to receive cases as soon as both exist.
/// </summary>
public class DecimalRoundTripTests : IAsyncLifetime
{
    // Same image as the devcontainer's compose `mssql` service (.devcontainer/docker-compose.yml).
    private readonly MsSqlContainer _mssql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _mssql.StartAsync();

    public Task DisposeAsync() => _mssql.DisposeAsync().AsTask();

    [Fact(Skip = "Sqlm.Core.Migrate and docs/type-mapping.md don't exist yet — PLAN.md §14.2, Phase 2/3.")]
    public Task Decimal_38_10_round_trips_at_max_precision()
    {
        _ = _mssql.GetConnectionString();
        return Task.CompletedTask;
    }
}
