namespace Sqlm.Core.Mssql;

/// <summary>
/// Reads the source schema from the SQL Server catalog views — never from driver-reported
/// metadata (PLAN.md §2.3 "Fidelity note"): <c>sys.tables</c>, <c>sys.columns</c>, <c>sys.types</c>,
/// <c>sys.indexes</c>, <c>sys.index_columns</c>, <c>sys.foreign_keys</c>,
/// <c>sys.check_constraints</c>, <c>sys.default_constraints</c>, <c>sys.extended_properties</c>.
/// </summary>
public interface ICatalogReader
{
    Task<SourceDatabase> ReadAsync(CancellationToken cancellationToken);
}

public sealed record SourceDatabase(string Name, IReadOnlyList<SourceTable> Tables);

public sealed record SourceTable(
    string Schema,
    string Name,
    IReadOnlyList<SourceColumn> Columns,
    long EstimatedRowCount);

public sealed record SourceColumn(
    string Name,
    string SqlType,
    int? Precision,
    int? Scale,
    int? MaxLength,
    bool IsNullable,
    bool IsIdentity,
    bool IsComputed,
    string? Collation);
