using Sqlm.Core.Mapping;
using Sqlm.Core.Mssql;
using Sqlm.Core.Project;

namespace Sqlm.Core.Migrate;

/// <summary>
/// PLAN.md §5.2 step 2 — applies the type map and workflow schema mutations, resolves name
/// collisions, topologically sorts tables by FK dependency, and computes a per-table copy plan.
/// The result is shown to the user for review/edit before anything is written.
/// </summary>
public interface IMigrationPlanner
{
    Task<MigrationPlan> PlanAsync(SourceDatabase source, SqlmProject project, CancellationToken cancellationToken);
}

public sealed record MigrationPlan(IReadOnlyList<TablePlan> Tables);

public sealed record TablePlan(
    SourceTable Source,
    TargetTable Target,
    string? OrderingKeyColumn,
    int BatchSize,
    string ProjectionSql);

/// <summary>
/// PLAN.md §5.2 steps 3–8 — drives a planned migration end to end and reports progress as a
/// <see cref="Contracts.JobEvent"/> stream.
/// </summary>
public interface IMigrationExecutor
{
    IAsyncEnumerable<Contracts.JobEvent> ExecuteAsync(MigrationPlan plan, string targetSqlitePath, CancellationToken cancellationToken);
}
