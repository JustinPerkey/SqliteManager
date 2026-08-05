# Sqlm.ContractsGen

Reflects over `Sqlm.Contracts` and writes `src/renderer/src/rpc/contracts.d.ts`. PLAN.md §4.2 and
§12: the C# `JobEvent` union and its TypeScript projection must not drift, so the TS side is
generated rather than hand-maintained.

```bash
dotnet run --project tools/Sqlm.ContractsGen -- src/renderer/src/rpc/contracts.d.ts
```

`Sqlm.App.csproj` runs this automatically after every build (see its `GenerateTsContracts`
target). CI runs it again and fails if the regenerated file differs from what's checked in — that
diff is the actual drift gate, not the local auto-run, which is just dev convenience.

Do **not** move this target onto `Sqlm.Contracts.csproj`: this tool `ProjectReference`s
`Sqlm.Contracts`, so a post-build target on `Sqlm.Contracts` itself retriggers every time the
generator's own build restores its dependency, recursing forever. `Sqlm.App` is a safe anchor
because nothing in the generator's build graph depends on it.
