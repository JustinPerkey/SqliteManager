namespace Sqlm.Core.Workflow;

/// <summary>
/// Lifecycle hooks a workflow step attaches to. PLAN.md §7.1.
/// </summary>
public enum WorkflowHook
{
    BeforeSchema,
    AfterSchema,
    BeforeTable,
    RowTransform,
    AfterTable,
    AfterData,
    AfterIndexes,
    Finalize,
}

public enum StepFailurePolicy
{
    Abort,
    Continue,
    Retry,
}

/// <summary>
/// A single step as stored in the project file, PLAN.md §7.1 / §7.4 ("plain JSON, diff cleanly").
/// </summary>
public sealed record WorkflowStepDefinition(
    string Id,
    string Type,
    WorkflowHook Hook,
    bool Enabled,
    string Name,
    string? Description,
    IReadOnlyDictionary<string, object?> Params,
    IReadOnlyList<string> DependsOn,
    StepFailurePolicy FailurePolicy = StepFailurePolicy.Abort);

/// <summary>
/// Implemented by each of the v1 step types (PLAN.md §7.1: addTable, addColumn, dropObject,
/// rename, filterRows, transformColumn, deriveTable, lookupEnrich, runSql, seedData, assert).
/// Adding a step type is a new class registered in <see cref="IWorkflowStepRegistry"/> plus a
/// schema entry — not a change to the runner (§7.1).
/// </summary>
public interface IWorkflowStep
{
    string Type { get; }

    Task ExecuteAsync(WorkflowStepDefinition definition, WorkflowExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves a step type name to its <see cref="IWorkflowStep"/> implementation.
/// </summary>
public interface IWorkflowStepRegistry
{
    IWorkflowStep Resolve(string type);
}

/// <summary>
/// Ambient state available to a step while it executes — the target connection, the source
/// catalog, and the dry-run flag (PLAN.md §7.2: dry-run caps rows per table, default 1000).
/// </summary>
public sealed record WorkflowExecutionContext(bool IsDryRun, int DryRunRowCap);
