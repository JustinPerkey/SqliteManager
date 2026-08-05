using Sqlm.Core.Project;

namespace Sqlm.Core.Mapping;

/// <summary>
/// The planned SQLite target schema, produced by <see cref="ITypeMap"/> resolving every source
/// column against the default table in PLAN.md §5.1 plus any project-level
/// <c>typeOverrides</c>.
/// </summary>
public sealed record TargetTable(
    string Name,
    IReadOnlyList<TargetColumn> Columns,
    IReadOnlyList<TargetIndex> Indexes,
    bool Strict);

public sealed record TargetColumn(
    string Name,
    SqliteStorageClass StorageClass,
    bool IsPrimaryKey,
    bool IsNotNull,
    string? DefaultExpression);

public sealed record TargetIndex(string Name, IReadOnlyList<string> Columns, bool IsUnique, string? PartialPredicate);

public enum SqliteStorageClass
{
    Integer,
    Real,
    Text,
    Blob,
}

/// <summary>
/// Resolves one source column to a <see cref="TargetColumn"/> plus, when the mapping loses
/// information, a <see cref="LossinessWarning"/> (PLAN.md §5.1 — "every lossy mapping raises a
/// warning event that lands in the run report").
/// </summary>
public interface ITypeMap
{
    (TargetColumn Column, LossinessWarning? Warning) Resolve(Mssql.SourceColumn column, TypeOverride? overrideRule);
}

public sealed record LossinessWarning(string Code, string Detail);
