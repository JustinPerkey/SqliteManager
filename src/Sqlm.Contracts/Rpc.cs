using System.Text.Json;

namespace Sqlm.Contracts;

/// <summary>
/// Request envelope sent from the renderer to Sqlm.App over
/// <c>window.chrome.webview.postMessage</c>. PLAN.md §4.1.
/// </summary>
public sealed record RpcRequest(string Id, string Method, JsonElement? Params);

/// <summary>
/// Response envelope sent back to the renderer for a given <see cref="RpcRequest.Id"/>.
/// Exactly one of <see cref="Result"/> or <see cref="Error"/> is set.
/// </summary>
public sealed record RpcResponse(string Id, JsonElement? Result, SerializedError? Error);
