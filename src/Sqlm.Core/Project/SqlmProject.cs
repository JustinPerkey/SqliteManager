using Sqlm.Core.Workflow;

namespace Sqlm.Core.Project;

/// <summary>
/// The <c>.sqlmproj</c> file model. PLAN.md §8 — JSON, versioned, schema-validated,
/// forward-migrated on load. There is no credential storage anywhere in this type:
/// auth is always Windows SSO, so no username/password/token field exists to get wrong.
/// </summary>
public sealed record SqlmProject(
    int Version,
    string Name,
    SourceConnection Source,
    TargetDatabase Target,
    MappingOptions Mapping,
    WorkflowDefinition Workflow,
    RunOptions Options);

public sealed record SourceConnection(string Server, string Database);

public sealed record TargetDatabase(string Path, bool Strict, IReadOnlyDictionary<string, string> Pragmas);

public sealed record MappingOptions(
    NameStrategy NameStrategy,
    IReadOnlyList<TypeOverride> TypeOverrides,
    IReadOnlyList<string> Excluded);

public enum NameStrategy
{
    Flatten,
    DropSchema,
    Prefix,
}

public sealed record TypeOverride(string Table, string Column, string SqliteType);

public sealed record WorkflowDefinition(IReadOnlyList<WorkflowStepDefinition> Steps);

public sealed record RunOptions(int BatchSize, bool CreateIndexes, string Verify);
