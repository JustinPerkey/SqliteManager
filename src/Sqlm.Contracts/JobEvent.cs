using System.Text.Json.Serialization;

namespace Sqlm.Contracts;

/// <summary>
/// Execution phases of a migration run, in order. PLAN.md §5.2.
/// </summary>
public enum Phase
{
    Introspect,
    Plan,
    Ddl,
    Copy,
    Index,
    Workflow,
    Verify,
    Finalize,
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
}

/// <summary>
/// One discriminated union, consumed by both the CLI and the UI. PLAN.md §4.2 — the TypeScript
/// side (renderer/src/rpc/contracts.d.ts) is generated from this file, so the two cannot drift.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PhaseEvent), "phase")]
[JsonDerivedType(typeof(TableEvent), "table")]
[JsonDerivedType(typeof(LogEvent), "log")]
[JsonDerivedType(typeof(WarningEvent), "warning")]
[JsonDerivedType(typeof(DoneEvent), "done")]
[JsonDerivedType(typeof(FailedEvent), "failed")]
public abstract record JobEvent;

public sealed record PhaseEvent(Phase Phase) : JobEvent;

public sealed record TableEvent(string Table, long RowsDone, long? RowsTotal, long Bytes) : JobEvent;

public sealed record LogEvent(LogLevel Level, string Message, object? Context) : JobEvent;

public sealed record WarningEvent(string Code, string? Table, string? Column, string Detail) : JobEvent;

public sealed record DoneEvent(RunSummary Summary) : JobEvent;

public sealed record FailedEvent(SerializedError Error, string? ResumeToken) : JobEvent;

public sealed record RunSummary(
    int TablesMigrated,
    long RowsMigrated,
    long WarningCount,
    TimeSpan Elapsed);

public sealed record SerializedError(string Message, string? Detail, string? StackTrace);
