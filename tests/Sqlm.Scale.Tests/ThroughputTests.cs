namespace Sqlm.Scale.Tests;

/// <summary>
/// PLAN.md §11 scale suite: a 10M-row synthetic table, asserting a throughput floor, a flat
/// memory profile, and that cancel returns within one batch. Depends on
/// <c>Sqlm.Core.Migrate.IMigrationExecutor</c>, which is Phase 2/7 work (PLAN.md §10) — not
/// implemented yet, so there is nothing to drive through the pipeline.
/// </summary>
public class ThroughputTests
{
    [Fact(Skip = "Sqlm.Core.Migrate.IMigrationExecutor isn't implemented yet — PLAN.md §10 Phase 2/7.")]
    public void Ten_million_row_copy_meets_the_throughput_floor()
    {
    }

    [Fact(Skip = "Sqlm.Core.Migrate.IMigrationExecutor isn't implemented yet — PLAN.md §10 Phase 2/7.")]
    public void Cancellation_lands_within_one_batch()
    {
    }
}
