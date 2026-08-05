using Sqlm.Core.Mapping;

namespace Sqlm.Core.Sqlite;

/// <summary>
/// Emits <c>CREATE TABLE</c>/index DDL for the target SQLite file from a resolved
/// <see cref="TargetSchema"/>. Indexes are emitted after data load (PLAN.md §5.1, §5.2 step 5).
/// </summary>
public interface IDdlEmitter
{
    string EmitCreateTable(TargetTable table);

    IReadOnlyList<string> EmitCreateIndexes(TargetTable table);
}
