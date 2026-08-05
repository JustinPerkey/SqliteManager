# renderer

The SqliteManager UI — React 19 + TypeScript, built with Vite and served by `Sqlm.App` from
`https://app.sqlm/` via `SetVirtualHostNameToFolderMapping` (see docs/PLAN.md §4.1, §9). No
router, no server-side anything: this is a single embedded page.

```bash
pnpm dev     # http://localhost:5173, for iterating on the UI standalone
pnpm build   # -> dist/, copied next to the Sqlm.App executable at publish time
pnpm lint    # oxlint
```

## Layout

- `src/App.tsx` — the app shell (title bar / sidebar / tab strip / status bar, PLAN.md §9)
- `src/features/{connect,schema,workflow,data,sql,log}/` — one panel per tab
- `src/components/` — shell chrome, not feature-specific
- `src/rpc/client.ts` — typed request/response bridge to `Sqlm.App` over
  `window.chrome.webview`
- `src/rpc/contracts.d.ts` — **generated**, do not hand-edit. Produced from `Sqlm.Contracts` by
  `tools/Sqlm.ContractsGen`; regenerates on every `Sqlm.App` build, and CI fails if the checked-in
  file doesn't match (PLAN.md §12).

## Package versions

Exact versions only — no `^`, `~`, or `*` (PLAN.md §2.2). CI rejects any range specifier in
`package.json`.
