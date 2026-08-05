# Dev container

Linux dev container (Ubuntu 24.04) with .NET 10 + .NET 8 SDKs, Node 22 / pnpm, and a real
**SQL Server 2022** service on the compose network.

## What this container is for

| Component | In container? | Why |
|---|---|---|
| `Sqlm.Core` — engine, mapping, migrate, workflow | ✅ | Pure .NET, no Windows APIs by design (PLAN.md §3). |
| `Sqlm.Cli` | ✅ | Headless; the reason it exists. |
| `Sqlm.Core.Tests`, `Sqlm.Fidelity.Tests`, `Sqlm.Scale.Tests` | ✅ | Run against the `mssql` service. |
| `src/renderer` (React + Vite) | ✅ | Vite dev server on 5173. |
| `Sqlm.App` (WPF + WebView2) | ❌ | WPF is Windows-only, WebView2 is a Windows component. Build and run on the host. |
| Windows SSO / integrated auth | ❌ | Requires a Windows logon token. See below. |
| `Sqlm.E2E.Tests` (Playwright → WebView2) | ❌ | Drives the packaged Windows app. |

## The auth split — read this before wondering why SSO "doesn't work"

The shipping app is **passwordless Windows SSO only** (PLAN.md §1.1). Linux has no Windows logon
token, and the `mcr.microsoft.com/mssql/server` image does not accept Windows auth without an AD
domain join. So inside this container, tests connect with **SQL auth** via `$SQLM_TEST_MSSQL`.

That is exactly the test-only path PLAN.md §11 specifies:

> That mode exists in `Sqlm.Core` as a **test-only** code path — never surfaced in the UI, never
> valid in a `.sqlmproj`, guarded by a build flag so it cannot ship.

**Consequence:** the container proves the engine — type fidelity, streaming, batching, pragmas,
workflows, throughput. It cannot prove the SSPI handshake. That is covered by:

1. `sqllocaldb` on the Windows host (LocalDB is Windows-auth-only, so it exercises the real path), and
2. the Windows CI job against SQL Server Express, and
3. a manual pre-release check against a domain-joined server.

Do not let SSO regressions hide behind a green container build.

## Usage

```bash
# tests against the container's SQL Server
dotnet test

# renderer
cd src/renderer && pnpm dev        # http://localhost:5173

# ad-hoc SQL against the service
/opt/mssql-tools18/bin/sqlcmd -S mssql -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT @@VERSION"
```

The SA password defaults to `Dev_Passw0rd_local` and is a **dev-only** credential for a container
that is not reachable outside the compose network. Override it by exporting `MSSQL_SA_PASSWORD`
before the container starts. It is unrelated to the shipping app, which stores no credentials at all
(PLAN.md §8).

## Host-side prerequisites

- Docker Desktop running (the container needs the daemon; Testcontainers additionally uses the
  mounted host socket).
- First start pulls ~1.5 GB of SQL Server image and builds the feature layers — several minutes.
