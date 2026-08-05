import type { RpcRequest, RpcResponse } from './contracts'

// Request/response bridge to Sqlm.App over `window.chrome.webview` (PLAN.md §4.1). Deliberately
// not `AddHostObjectToScript` — the C# side (Sqlm.App/Rpc/RpcRouter.cs) exposes a plain method
// allow-list instead of a synchronous COM surface.

interface WebViewChrome {
  postMessage(message: string): void
  addEventListener(type: 'message', listener: (event: MessageEvent<string>) => void): void
  removeEventListener(type: 'message', listener: (event: MessageEvent<string>) => void): void
}

declare global {
  interface Window {
    chrome?: { webview?: WebViewChrome }
  }
}

type Pending = {
  resolve: (value: unknown) => void
  reject: (reason: Error) => void
}

const pending = new Map<string, Pending>()
let nextId = 0

function webview(): WebViewChrome {
  const bridge = window.chrome?.webview
  if (!bridge) {
    throw new Error('window.chrome.webview is unavailable — this page is not hosted in Sqlm.App.')
  }
  return bridge
}

function handleMessage(event: MessageEvent<string>) {
  const response = JSON.parse(event.data) as RpcResponse
  const waiter = pending.get(response.id)
  if (!waiter) return

  pending.delete(response.id)
  if (response.error) {
    waiter.reject(new Error(response.error.message))
  } else {
    waiter.resolve(response.result)
  }
}

let listening = false

function ensureListening() {
  if (listening) return
  webview().addEventListener('message', handleMessage)
  listening = true
}

/**
 * Calls an RPC method registered on the C# side (`RpcRouter.Register`) and resolves with its
 * typed result. `TParams`/`TResult` should match the corresponding C# handler's contract.
 */
export function call<TParams, TResult>(method: string, params: TParams): Promise<TResult> {
  ensureListening()

  const id = `rpc-${++nextId}`
  const request: RpcRequest = { id, method, params: params as unknown }

  return new Promise<TResult>((resolve, reject) => {
    pending.set(id, { resolve: resolve as (value: unknown) => void, reject })
    webview().postMessage(JSON.stringify(request))
  })
}
