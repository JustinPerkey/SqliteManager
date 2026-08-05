using System.Text.Json;
using Sqlm.Contracts;

namespace Sqlm.App.Rpc;

/// <summary>
/// Typed method registry for the request/response RPC channel described in PLAN.md §4.1:
/// request/response over <c>PostWebMessageAsJson</c> / <c>WebMessageReceived</c>, correlated by
/// <see cref="RpcRequest.Id"/>. Deliberately not <c>AddHostObjectToScript</c> — COM host objects
/// are synchronous and expose more surface than a method allow-list.
/// </summary>
public sealed class RpcRouter
{
    private readonly Dictionary<string, Func<JsonElement?, CancellationToken, Task<object?>>> _handlers = new();

    public void Register(string method, Func<JsonElement?, CancellationToken, Task<object?>> handler)
    {
        _handlers[method] = handler;
    }

    public async Task<RpcResponse> DispatchAsync(RpcRequest request, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(request.Method, out var handler))
        {
            return new RpcResponse(request.Id, null, new SerializedError($"Unknown RPC method '{request.Method}'.", null, null));
        }

        try
        {
            var result = await handler(request.Params, cancellationToken).ConfigureAwait(false);
            var resultElement = result is null
                ? (JsonElement?)null
                : JsonSerializer.SerializeToElement(result, SqlmJsonOptions.Default);
            return new RpcResponse(request.Id, resultElement, null);
        }
        catch (Exception ex)
        {
            return new RpcResponse(request.Id, null, new SerializedError(ex.Message, ex.GetType().FullName, ex.StackTrace));
        }
    }
}
