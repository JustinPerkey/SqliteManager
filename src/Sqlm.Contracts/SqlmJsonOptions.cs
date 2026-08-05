using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sqlm.Contracts;

/// <summary>
/// The one <see cref="JsonSerializerOptions"/> instance every RPC/event message is serialized
/// with — camelCase properties and enums so the wire shape matches TypeScript conventions
/// directly, no per-property <c>[JsonPropertyName]</c> needed. <c>tools/Sqlm.ContractsGen</c>
/// applies the same casing when generating <c>contracts.d.ts</c>, so the two stay in lockstep.
/// </summary>
public static class SqlmJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
