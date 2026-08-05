# SqliteManager — Implementation Plan

A Windows desktop application that converts Microsoft SQL Server databases into SQLite databases,
lets the user browse and edit the resulting data, and lets the user define repeatable **workflows**
that run as part of the translation (including creating additional tables that do not exist in the
source).

**Decided:** .NET backend + WebView2/React frontend, passwordless Windows SSO, everything bundled.
See §2 for why, §2.3 for what was rejected.

---

## 1. Product scope

### 1.1 Core capabilities

1. **Connect** to one or more MSSQL instances. **Windows authentication only, passwordless** —
   the app uses the logged-in Windows token (`Integrated Security=True`) and never prompts for
   credentials. SQL logins and Azure AD are not offered in the UI.
2. **Introspect** the source database: schemas, tables, views, columns, types, PKs, FKs, unique
   constraints, indexes, check constraints, defaults, computed columns, identity, collations,
   row-count estimates.
3. **Map** the source schema to a SQLite target schema with an editable, per-column type map and
   explicit lossiness reporting.
4. **Migrate** data with a streaming, batched, cancellable, resumable engine and live progress.
5. **Browse & edit** the produced SQLite file: virtualized data grid, inline editing, insert/delete,
   filtering/sorting, foreign-key navigation, SQL console.
6. **Workflows**: an ordered, versioned pipeline of steps attached to a migration project, executed
   at defined lifecycle hooks, capable of adding tables, deriving columns, filtering rows,
   renaming/reshaping, and running arbitrary SQL.
7. **Projects**: everything above persisted to a `.sqlmproj` file so a conversion is repeatable and
   diffable in source control.

### 1.2 Explicit non-goals (v1)

- Two-way sync / writing changes back to MSSQL.
- Live replication or CDC.
- Migrating SQL Server programmability (stored procedures, triggers, functions) — surfaced in the
  report as "not migrated", optionally dumped to a `.sql` sidecar file for reference.
- Non-Windows support. Windows SSO is a core requirement, so the app is Windows-only by design;
  `Sqlm.Core` stays free of Windows-specific APIs regardless, so this stays a policy, not a corner.

---

## 2. Technology choice

**Stack: .NET 10 (LTS) backend + WebView2 host + React/TypeScript frontend.**

| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 LTS, `win-x64` self-contained | No runtime install on the target machine. |
| Shell | WPF window hosting a single `Microsoft.Web.WebView2` control | Microsoft-supported, correct per-monitor DPI, Windows-native window chrome/menus. |
| Frontend | React 19 + TypeScript + Vite + Tailwind + Radix primitives | Same polished UI as any web app; built to static assets at build time. |
| Data grid | TanStack Table (headless) + TanStack Virtual | Virtualized rows/columns; full control over editing UX; no commercial grid. |
| Frontend state | Zustand + TanStack Query over the RPC bridge | Small, predictable, good async/caching story for IPC-backed data. |
| MSSQL client | `Microsoft.Data.SqlClient` | `Integrated Security=True` uses OS SSPI with **zero** prerequisites. Exact-value accessors (`GetSqlDecimal`, `GetSqlMoney`, `GetSqlBytes`) eliminate a whole class of precision bugs. |
| SQLite client | `Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3` | SQLite compiled into the app; no system SQLite, no `sqlite3.dll`. |
| Pipeline | `System.Threading.Channels` (bounded) + `Task` + `CancellationToken` | Real threads, real backpressure; no worker-process gymnastics. |
| Expressions | Hand-rolled expression parser (default) + `Jint` (opt-in advanced mode) | Both pure managed, sandboxable, bundled. No `eval`. |
| Packaging | `dotnet publish` self-contained single-file + **Velopack** installer/updater; WiX MSI variant for GPO deployment | One-file install, silent auto-update, enterprise-deployable. |
| Testing | xUnit, `Testcontainers.MsSql`, FluentAssertions, Playwright-over-CDP against WebView2 | Real SQL Server in CI; the UI is drivable because WebView2 exposes a CDP endpoint. |

### 2.1 Why this stack (the constraint that decided it)

Requirement 1.1 is passwordless Windows SSO. Requirement 2.2 is that nothing gets installed
alongside the app. Those two together eliminate Node: `tedious` implements NTLM in JavaScript from a
supplied password and exposes no hook for an OS-generated SSPI/SPNEGO token, so SSO on Node means
`msnodesqlv8` + the MS ODBC Driver 18 — a machine-level prerequisite.

`Microsoft.Data.SqlClient` does integrated auth against Windows SSPI natively, in-process, with no
extra install. Combined with a self-contained publish it satisfies both constraints at once, and it
happens to be the highest-fidelity MSSQL client available in any language.

The UI plan is unaffected: the frontend is still React + TanStack rendered in a Chromium engine. Only
the host and the language behind the RPC boundary changed.

### 2.2 Bundling policy — the app is self-contained

**Rule: a fresh Windows machine installs SqliteManager and it works. No prerequisites, no runtime
downloads, no network at runtime.**

*Runtime — what ships in the package*

| Thing | How it is bundled | What is explicitly *not* used |
|---|---|---|
| .NET runtime | `--self-contained -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` | A machine-wide .NET install; `dotnet` on PATH |
| SQLite | `SQLitePCLRaw.bundle_e_sqlite3` — `e_sqlite3.dll` compiled from the amalgamation, shipped in-package | System `sqlite3.dll`, SQLite ODBC, `System.Data.SQLite` |
| MSSQL client | `Microsoft.Data.SqlClient` managed TDS + OS SSPI (`sspicli.dll`, already part of Windows) | MS ODBC Driver 17/18, SQL Native Client, `sqlcmd`, `bcp`, SSMS |
| Frontend assets | Vite build output embedded as resources, extracted to app dir and served via `SetVirtualHostNameToFolderMapping` | A localhost HTTP server (would trigger a firewall prompt), any CDN |
| Editor (SQL console) | Monaco bundled through Vite, workers emitted as local assets | Any `cdn.jsdelivr.net` / `unpkg` loader |
| Fonts & icons | `.woff2` and SVGs vendored into the repo, referenced by relative URL | Google Fonts, icon CDNs, any webfont fetch |
| Node / pnpm | **Build-time only** — produces static assets that get embedded. Never present at runtime | Shipping Node with the app |
| Updates | Velopack, bundled; the *only* outbound network call the app makes, and it is opt-in | Any silent phone-home, any telemetry |

**WebView2 is the single OS-level dependency.** It is an Evergreen component preinstalled on Windows
10 (recent) and Windows 11. The installer chains the WebView2 bootstrapper for older images; on a
current machine that is a no-op. This is documented as the one accepted exception, and the app
detects a missing runtime and says so plainly rather than crashing.

*Build — pinned versions*

- **NuGet**: `<PackageReference>` with exact versions only, plus **Central Package Management**
  (`Directory.Packages.props`) so a version is declared once. `packages.lock.json` committed;
  CI restores with `--locked-mode`. `NuGetAudit` on, build fails on known-vulnerable packages.
- **npm**: exact versions only — no `^`, no `~`, no `latest`, no `*`. Enforced by a CI lint that
  fails on any range specifier. `pnpm-lock.yaml` committed; CI runs `--frozen-lockfile`.
- `global.json` pins the .NET SDK; `packageManager` + `.nvmrc` pin the JS build toolchain.
- `PublishReadyToRun=true` for startup. **No trimming, no NativeAOT** — `Microsoft.Data.SqlClient`
  and `System.Text.Json` polymorphism are reflection-heavy and trimming them is a bug farm for a
  binary-size win nobody asked for.
- Renderer bundle is verified self-contained: CI greps the built output for `http://`/`https://`
  asset references and fails on any hit.
- A strict CSP on the WebView2 document forbids remote script/style/font/img origins, so a CDN
  dependency fails loudly in dev instead of silently at a customer site.

*Dev/CI only, never shipped:* the Testcontainers SQL Server image, Playwright browsers, and the
type-fidelity fixture generator. These pull from the network at build time; nothing they pull enters
the installer.

### 2.3 Rejected alternatives

- *Electron + tedious* — cannot do passwordless SSO without an installed ODBC driver (§2.1). Was the
  leading candidate until the Windows-auth-only constraint landed.
- *Electron + `msnodesqlv8` + chained ODBC MSI* — works, but installs a machine-level component and
  puts two native modules on the critical path, each needing a rebuild per Electron ABI bump.
- *Tauri + Rust* — `tiberius` has partial SSPI support but is less battle-tested on exotic types,
  and it splits the codebase across two languages for no gain here.
- *WPF/Avalonia native UI* — best-in-class data binding, but building a genuinely nice virtualized
  editable grid is far more work than the React equivalent, and the UI quality bar in §9 is explicit.
- *ASP.NET Core + browser* — a localhost listener means a Windows Firewall prompt on first run and an
  open port on a machine with database credentials in scope. `SetVirtualHostNameToFolderMapping`
  gives the same developer experience with neither.

**Fidelity note:** do not rely on driver-reported column metadata. Read the schema from the SQL
Server catalog views (`sys.tables`, `sys.columns`, `sys.types`, `sys.indexes`, `sys.index_columns`,
`sys.foreign_keys`, `sys.check_constraints`, `sys.default_constraints`, `sys.extended_properties`).
That yields exact precision, scale, collation, computed-column definitions, and identity
seed/increment independent of any driver's opinion.

---

## 3. Repository layout

```
SqliteManager/
├─ global.json                    # pinned SDK
├─ Directory.Packages.props       # central NuGet version management
├─ Directory.Build.props          # nullable, warnings-as-errors, LangVersion
├─ SqliteManager.sln
├─ docs/
│  ├─ PLAN.md
│  ├─ type-mapping.md             # normative spec — fidelity tests generate from it
│  └─ workflow-schema.md
├─ src/
│  ├─ Sqlm.Core/                  # engine — no UI, no Windows-only APIs
│  │  ├─ Mssql/                   # connection factory, catalog introspection, streaming reader
│  │  ├─ Sqlite/                  # DDL emitter, bulk writer, browse/edit data access
│  │  ├─ Mapping/                 # type map, name sanitizer, lossiness rules
│  │  ├─ Migrate/                 # planner, executor, checkpointing, JobEvent stream
│  │  ├─ Workflow/                # step registry, DAG resolver, runner, expression eval
│  │  └─ Project/                 # .sqlmproj model, load/save/version migration
│  ├─ Sqlm.Contracts/             # DTOs + JobEvent union; source of generated TS types
│  ├─ Sqlm.App/                   # WPF host: window, WebView2, RPC router, file dialogs
│  ├─ Sqlm.Cli/                   # headless: sqlm plan|run|dry-run  (CI-testable engine)
│  └─ renderer/                   # React app (Vite) — built assets embedded into Sqlm.App
│     ├─ src/features/{connect,schema,workflow,data,sql,log}/
│     ├─ src/components/          # design-system primitives
│     └─ src/rpc/                 # typed client + generated contracts.d.ts
└─ tests/
   ├─ Sqlm.Core.Tests/            # unit
   ├─ Sqlm.Fidelity.Tests/        # Testcontainers MSSQL — type round-trip
   ├─ Sqlm.Scale.Tests/           # throughput/memory/cancel
   └─ Sqlm.E2E.Tests/             # Playwright over CDP against the packaged app
```

`Sqlm.Cli` is not a nice-to-have — it makes the entire engine testable and scriptable without a UI,
and it is nearly free once `Sqlm.Core` exists.

---

## 4. Architecture

### 4.1 Process & threading model

```
┌──────────────────────────────┐        ┌────────────────────────────────────┐
│ WebView2 (Chromium procs)    │        │ Sqlm.App  (.NET, single process)   │
│  React UI                    │        │                                    │
│  window.chrome.webview       │◄──────►│  RpcRouter  (System.Text.Json)     │
│   .postMessage / onmessage   │  JSON  │  JobHost    (Task + CancellationTS)│
└──────────────────────────────┘        │  Sqlm.Core  engine                 │
                                        └────────────────────────────────────┘
```

- One .NET process. Long jobs run on the thread pool, not the UI thread; the WebView2 renderer is
  already a separate OS process, so the UI cannot be blocked by engine work at all.
- **RPC**: request/response over `PostWebMessageAsJson` / `WebMessageReceived`, with a correlation id
  and a typed method registry. No `AddHostObjectToScript` — COM host objects are synchronous,
  awkward to type, and expose more surface than a method allow-list.
- **Events**: the engine writes `JobEvent`s to a `Channel<JobEvent>`; a pump coalesces them to
  ≈10 Hz and pushes to the renderer, so a 50M-row copy cannot drown the UI in messages.
- **Cancellation**: `CancellationTokenSource` per job, checked between batches — cancel lands within
  one batch (<1 s at default sizes).
- **Isolation**: `SetVirtualHostNameToFolderMapping("app.sqlm", assetDir, DenyCors)` serves the UI
  from `https://app.sqlm/index.html`. No local HTTP listener, no port, no firewall prompt.
  `AreDevToolsEnabled=false` in release; navigation to any non-`app.sqlm` origin is blocked in
  `NavigationStarting`; `NewWindowRequested` routes real links to the system browser.

### 4.2 Progress/event contract

One discriminated union, consumed by both the CLI and the UI. C# records with
`System.Text.Json` polymorphism; the TypeScript definitions are **generated** from
`Sqlm.Contracts` at build time so the two sides cannot drift.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PhaseEvent),   "phase")]
[JsonDerivedType(typeof(TableEvent),   "table")]
[JsonDerivedType(typeof(LogEvent),     "log")]
[JsonDerivedType(typeof(WarningEvent), "warning")]
[JsonDerivedType(typeof(DoneEvent),    "done")]
[JsonDerivedType(typeof(FailedEvent),  "failed")]
public abstract record JobEvent;

public sealed record PhaseEvent(Phase Phase) : JobEvent;              // introspect|plan|ddl|copy|index|workflow|verify|finalize
public sealed record TableEvent(string Table, long RowsDone, long? RowsTotal, long Bytes) : JobEvent;
public sealed record LogEvent(LogLevel Level, string Message, object? Context) : JobEvent;
public sealed record WarningEvent(string Code, string? Table, string? Column, string Detail) : JobEvent;
public sealed record DoneEvent(RunSummary Summary) : JobEvent;
public sealed record FailedEvent(SerializedError Error, string? ResumeToken) : JobEvent;
```

---

## 5. Conversion engine

### 5.1 Type mapping (default table)

SQLite has five storage classes, so mapping is inherently lossy for several MSSQL types. Every
non-trivial mapping is user-overridable per column, and every lossy mapping raises a `warning` event
that lands in the run report.

Read values with the **`GetSql*` accessors**, not the CLR ones — `GetSqlDecimal` returns the exact
`SqlDecimal`, where `GetDecimal` can throw or round on values outside `System.Decimal`'s range.

| MSSQL type | Reader accessor | Default SQLite | Notes / lossiness |
|---|---|---|---|
| `bit` | `GetBoolean` | `INTEGER` (0/1) | Lossless. |
| `tinyint`/`smallint`/`int`/`bigint` | `GetSqlInt*` | `INTEGER` | Lossless (SQLite INTEGER is 64-bit). |
| `decimal`/`numeric` | `GetSqlDecimal` | `TEXT` (default) or `REAL` (opt-in) | **Lossy if REAL.** `SqlDecimal` handles precision 38 exactly, which `System.Decimal` (28–29) cannot. TEXT stores the canonical string. |
| `money`/`smallmoney` | `GetSqlMoney` | `TEXT` | Exact 4-decimal scale preserved. |
| `float`/`real` | `GetDouble`/`GetFloat` | `REAL` | Lossless (both IEEE-754). |
| `char`/`varchar`/`text` | `GetSqlChars` | `TEXT` | Converted to UTF-8; collation dropped → warning. |
| `nchar`/`nvarchar`/`ntext` | `GetSqlChars` | `TEXT` | UTF-16 → UTF-8. |
| `date` | `GetDateTime` | `TEXT` `YYYY-MM-DD` | Lossless. |
| `time` | `GetTimeSpan` | `TEXT` `HH:MM:SS[.fffffff]` | Lossless at declared scale. |
| `datetime`/`smalldatetime` | `GetDateTime` | `TEXT` ISO-8601 | `datetime` rounds to 1/300 s at the source; preserved as stored. |
| `datetime2` | `GetDateTime` | `TEXT` ISO-8601 | Preserve declared fractional scale. |
| `datetimeoffset` | `GetDateTimeOffset` | `TEXT` ISO-8601 with offset | Lossless as text; ordering needs normalization → offer "store UTC + separate offset column". |
| `uniqueidentifier` | `GetGuid` | `TEXT` uppercase canonical | Optional `BLOB(16)` mode for space. |
| `binary`/`varbinary`/`image` | `GetStream` | `BLOB` | Lossless, streamed — never fully buffered. |
| `xml` | `GetSqlXml` → `CreateReader`/value | `TEXT` | Lossless as serialized text. |
| `rowversion`/`timestamp` | — | skip (default) or `BLOB(8)` | Meaningless in SQLite → skipped with a warning. |
| `hierarchyid` | source-side `CAST(col AS nvarchar(4000))` | `TEXT` | Avoids the `Microsoft.SqlServer.Types` dependency entirely. |
| `geography`/`geometry` | source-side `col.STAsText()` | `TEXT` WKT | Lossy for some curve types → warning. Same dependency-avoidance reason. |
| `sql_variant` | `GetValue` + `SQL_VARIANT_PROPERTY` | `TEXT` + companion `_type` column (opt-in) | Lossy by definition; warned. |
| computed columns | — | materialized column, or SQLite `GENERATED ALWAYS AS` when the expression is translatable | Default: materialize the value and flag the column. |

Additional schema-level decisions:

- **STRICT tables**: opt-in per project. Sensible default *on* for new work, *off* when the source
  has messy data, since STRICT rejects affinity violations at insert time.
- **Identity** → `INTEGER PRIMARY KEY` (rowid alias) when the PK is a single integer column;
  `AUTOINCREMENT` only when monotonic guarantees are explicitly wanted (it costs a sequence table).
- **Names**: schema-qualified objects (`sales.Order`) flatten to `sales_Order` by default;
  configurable strategy (flatten / drop-schema / prefix), with collision detection at plan time.
  Reserved-word and invalid-identifier sanitizing, with a reversible mapping stored in the project.
- **Constraints**: PK, UNIQUE, NOT NULL, DEFAULT and FK are emitted. CHECK constraints are translated
  when the expression is in a supported subset, otherwise dropped with a warning.
- **Indexes**: emitted **after** the data load (large speedup); filtered indexes become SQLite
  partial indexes where the predicate is supported.
- **Views**: optional; translated best-effort and reported when the dialects diverge.

### 5.2 Migration execution order

1. `introspect` — read catalog, build the source model.
2. `plan` — apply mapping + workflow schema mutations, resolve name collisions, topologically sort
   tables by FK dependency, compute a per-table copy plan (ordering key, batch size, projection SQL).
   **The plan is shown to the user for review/edit before anything is written.**
3. `ddl` — create the SQLite file, apply load-time pragmas, emit `CREATE TABLE` (no indexes yet),
   run `beforeSchema`/`afterSchema` workflow hooks.
4. `copy` — per table: stream from MSSQL → transform → batch-insert.
5. `index` — create indexes and unique constraints.
6. `workflow` — run `afterData` steps (derived tables, aggregates, enrichment).
7. `verify` — per-table row-count reconciliation, `PRAGMA foreign_key_check`,
   `PRAGMA integrity_check`, optional checksum sampling.
8. `finalize` — restore durable pragmas, `ANALYZE`, optional `VACUUM`, write the run report.

### 5.3 Performance design

- **Source read**: `ExecuteReaderAsync(CommandBehavior.SequentialAccess)` — columns read strictly in
  order, large values streamed via `GetStream`/`GetTextReader`, nothing buffered per row beyond the
  current value. Projection SQL selects only mapped columns and applies the source-side `CAST`/
  `.STAsText()` conversions from §5.1.
- **Pipeline**: reader task → `Channel<RowBatch>` (bounded, e.g. 4 batches) → writer task.
  The bounded channel *is* the backpressure: a slow writer blocks the reader without any manual
  pause/resume logic.
- **Target write**: one `SqliteCommand` per table, prepared once, parameters rebound per row, inside
  an explicit transaction per batch (default 20 000 rows, adaptive on observed row width).
- **Load-time pragmas**: `journal_mode = OFF` (or `MEMORY`), `synchronous = OFF`,
  `foreign_keys = OFF`, `temp_store = MEMORY`, `cache_size = -256000`, `page_size = 8192`
  (must be set before the first write). Restored to durable settings (`WAL`,
  `synchronous = NORMAL`, `foreign_keys = ON`) at finalize.
  **These unsafe pragmas are only ever applied to a file the app is building** — never to a
  user-supplied database opened for browsing.
- **Parallelism**: copying N tables concurrently into one SQLite file is pointless — SQLite
  serializes writers. Instead run several *readers* ahead of a single writer queue, which is where
  the latency actually lives. Cross-table write parallelism only when targeting separate files.
- **Resumability**: per-table checkpoint (last committed ordering-key value + row count) written to a
  sidecar `.sqlm-state` JSON after each committed batch; resume replays from the checkpoint.
- Tables with no usable ordering key fall back to a non-resumable single-pass copy, flagged in the
  plan up front rather than discovered at hour three.

---

## 6. Browse & edit

- **Object tree**: tables, views, indexes, triggers; row counts; quick filter.
- **Data grid**: virtualized (rows *and* columns), paged against the engine with **keyset
  pagination**, not `OFFSET` (constant-time on large tables).
- **Editing**: inline cell edit with per-type editors (text, number, date picker, boolean, BLOB
  viewer/upload, NULL toggle). Writes are `UPDATE … WHERE <pk> = @p`, falling back to `rowid` when
  the table has no PK; `WITHOUT ROWID` tables lacking a PK are read-only, stated clearly in the UI.
- **Transactions**: edits stage into a pending set shown as a dirty-cell overlay, committed as one
  transaction on save; discard reverts. Session-wide undo/redo.
- **Filter/sort**: per-column filter chips compiled to parameterized SQL; multi-column sort.
- **FK navigation**: click a FK cell → open the referenced row in a side panel or new tab.
- **SQL console**: Monaco with SQLite dialect completion driven by the live schema, `Ctrl+Enter` to
  run, result grid, automatic `LIMIT` injection on unbounded `SELECT`s, an explicit "this statement
  writes" confirmation for DDL/DML, query timeout + cancel, and an `EXPLAIN QUERY PLAN` view.
- **Export**: CSV / JSON / SQL dump for a table or a query result. (Parquet: stretch.)

Connection handling: a dedicated long-lived `SqliteConnection` for browsing, separate from any
migration job, so grid paging stays responsive while a conversion runs.

---

## 7. Workflows

### 7.1 Model

A workflow is an ordered list of **steps**, each bound to a **hook**, stored in the project file.

Hooks: `beforeSchema` → `afterSchema` → `beforeTable(table)` → `rowTransform(table)` →
`afterTable(table)` → `afterData` → `afterIndexes` → `finalize`.

Step types (v1):

| Step | Purpose |
|---|---|
| `addTable` | Create a table that does not exist in the source (explicit column defs, or `CREATE TABLE AS SELECT`). |
| `addColumn` | Add a column to a mapped table, from a constant, expression, or lookup source. |
| `dropObject` | Exclude a table/column from the target. |
| `rename` | Rename a table or column, with the mapping recorded for traceability. |
| `filterRows` | Push a `WHERE` predicate into the source query for a table. |
| `transformColumn` | Per-value transform (expression, regex replace, trim, case, date reformat, hash/redact). |
| `deriveTable` | Populate a new table from a SQL query over already-loaded target data (aggregates, junction tables, denormalized reporting tables). |
| `lookupEnrich` | Join a table against a CSV/JSON file or another table to add columns. |
| `runSql` | Arbitrary SQL against the target (escape hatch; flagged in the report). |
| `seedData` | Insert literal rows or import a CSV into any table. |
| `assert` | Post-condition check (row count, uniqueness, no-nulls) that fails the run or warns. |

Each step carries: `id`, `type`, `hook`, `enabled`, `name`, `description`, `params`, `dependsOn[]`.
Step types are registered in a `IWorkflowStep` registry, so adding one is a new class plus a schema
entry — not a change to the runner.

### 7.2 Execution semantics

- Steps at the same hook run in **dependency order** (`dependsOn` forms a DAG; cycles rejected at
  plan time), then declaration order for ties.
- `rowTransform` steps compile into a single delegate chain per table (built once, via compiled
  expression trees) so the hot path is one call, not N interpreted lookups per row.
- **Dry-run mode**: executes the whole pipeline against a temporary SQLite file with a row cap
  (default 1 000 rows/table), producing the full report and a schema preview in seconds. This is the
  primary iteration loop for authoring workflows.
- Per-step failure policy: `abort` (default) / `continue` / `retry(n)`.
- Every step emits timing, rows affected, and warnings into the run report.

### 7.3 Expression evaluation (security-relevant)

- **Default**: a small expression language — hand-rolled Pratt parser compiled to expression trees —
  with a fixed function library (string, math, date, hash, coalesce, regex) and **no** I/O, no
  network, no filesystem, no reflection, no CLR type access.
- **Advanced mode** (opt-in per project): `Jint`, a managed JS interpreter, configured with
  `LimitMemory`, `TimeoutInterval`, `MaxStatements`, and **no** CLR interop
  (`AllowClrAccess(false)`). Fully bundled, no `eval` of arbitrary .NET.
- Because a `.sqlmproj` may be shared, the app warns before running advanced-mode expressions or
  `runSql` steps from a project file the user did not author.
- `runSql` steps are parameterized where possible and always listed verbatim in the pre-run plan
  review, so the user sees exactly what will execute before it runs.

### 7.4 Authoring UI

- Workflow tab: step list (drag to reorder, grouped by hook) + step editor panel + live dry-run
  preview showing before/after sample rows for the selected step.
- Schema diff view: source schema vs. planned target schema, with workflow-caused changes highlighted.
- Steps are plain JSON in the project file — they diff cleanly in git and can be hand-edited.

---

## 8. Project file

`.sqlmproj` — JSON, versioned, schema-validated, forward-migrated on load:

```jsonc
{
  "version": 1,
  "name": "Northwind → SQLite",
  "source": { "server": "SQL01\\PROD", "database": "Northwind" },
  //         auth is always Windows SSO — no auth mode, no username, no secret, ever
  "target": { "path": "./northwind.db", "strict": true, "pragmas": { } },
  "mapping": { "nameStrategy": "flatten", "typeOverrides": [ ], "excluded": [ ] },
  "workflow": { "steps": [ ] },
  "options": { "batchSize": 20000, "createIndexes": true, "verify": "counts+fk" }
}
```

**There is no credential storage anywhere in this app.** Windows-SSO-only means the connection rides
the logged-in token, so there is no password, no token cache, no DPAPI blob, and no credential store
integration to get wrong. That is a meaningful security simplification and a direct consequence of
the §1.1 decision.

- Connection strings are never written to logs; the logger redacts `password`, `pwd`, `token`, and
  `AccountKey` patterns anyway, as defence in depth against a future auth mode.
- TLS: `Encrypt=True` (the `Microsoft.Data.SqlClient` 4.x+ default);
  `TrustServerCertificate` stays `False` and requires an explicit per-connection opt-in with a
  visible warning — the common failure on internal servers with self-signed certs, so the error
  message must name the fix rather than leaving the user to guess.
- Server discovery: manual entry + a recent-servers list, with best-effort `SqlDataSourceEnumerator`
  browse as a convenience (it is unreliable across subnets, so it is never the only path).

---

## 9. UI design

**Layout**

```
┌────────────────────────────────────────────────────────────────────┐
│ Title bar · project name · [Connect] [Plan] [Run] [Dry run]        │
├──────────────┬─────────────────────────────────────────────────────┤
│ Sidebar      │ Tab strip: Schema map │ Workflow │ Data │ SQL │ Log  │
│ ─ Connections│                                                     │
│ ─ Source tree│              main pane (virtualized)                │
│ ─ Target tree│                                                     │
├──────────────┴─────────────────────────────────────────────────────┤
│ Status bar: phase · rows/s · ETA · warnings(3) · [Cancel]          │
└────────────────────────────────────────────────────────────────────┘
```

**Responsiveness rules (non-negotiable)**

- No synchronous work >8 ms on the renderer thread; everything DB-touching is an RPC call.
- Every list/grid virtualized; no unbounded `SELECT *` render.
- Optimistic UI for edits, with rollback on failure.
- Progress is always cancellable, and cancel takes effect within one batch (<1 s at default sizes).
- Skeleton states, not spinners, for schema/grid loads; toasts for background completions.

**Quality bar**

- Full keyboard navigation; command palette (`Ctrl+K`); Excel-like grid keyboard editing.
- Light/dark themes from CSS variables, following the OS preference.
- Accessible: focus rings, ARIA grid semantics, `prefers-reduced-motion` respected.
- Errors are actionable: message + likely cause + a "copy diagnostics" button. The three errors that
  will actually happen — SSPI login failure, TLS certificate rejection, and permission-denied on a
  catalog view — get hand-written explanations, not raw driver text.

---

## 10. Phased delivery

| Phase | Deliverable | Rough effort |
|---|---|---|
| **0 — Scaffolding** | Solution + project layout, `Directory.Packages.props`, lock files, WPF+WebView2 host with virtual-host asset mapping, typed RPC bridge + generated TS contracts, app shell (sidebar/tabs/status bar), Windows CI, the §2.2 lint gates. | 5–7 d |
| **1 — Connect & introspect** | SSO connection manager + recent servers, catalog reader, source object tree, table/column detail, row-count estimates, the three hand-written error explanations. | 5–8 d |
| **2 — Map & migrate (MVP)** | Type-map engine + override UI, DDL emitter, channel pipeline with batching/pragmas, progress + cancel, verify (counts, FK check), run report. **First end-to-end conversion.** | 10–15 d |
| **3 — CLI + test harness** | `Sqlm.Cli` (`plan`/`run`/`dry-run`), Testcontainers fixtures, type-fidelity suite generated from `docs/type-mapping.md`. | 4–6 d |
| **4 — Browse & edit** | Virtualized grid, keyset paging, inline editing + staged transactions, filter/sort, FK navigation, BLOB viewer, export. | 10–14 d |
| **5 — SQL console** | Monaco + SQLite dialect, schema completion, safe execution, `EXPLAIN QUERY PLAN`, result export. | 4–6 d |
| **6 — Workflows** | Step registry + runner + DAG, the 11 v1 step types, expression compiler + Jint sandbox, dry-run preview, authoring UI, schema diff view. | 12–18 d |
| **7 — Resume & scale** | Checkpoint/resume, adaptive batch sizing, parallel reader pipeline, large-BLOB streaming, perf benchmark suite. | 5–8 d |
| **8 — Polish & ship** | Theming pass, command palette, error UX, crash reporting (local, no telemetry), code signing, Velopack installer + auto-update, WiX MSI variant, docs. | 6–10 d |

Phases 0–3 give a genuinely useful tool; 4–5 make it a manager; 6 is the differentiator.

---

## 11. Testing strategy

- **Type fidelity suite** (highest value): a generated MSSQL database containing every type at
  boundary values — `decimal(38,10)` at max/min, `datetime2(7)`, negative `money`, large `varbinary`,
  full-Unicode `nvarchar` including surrogate pairs, NULLs everywhere. Migrate, then assert exact
  round-trip against `docs/type-mapping.md`. Runs against real SQL Server 2022 via
  `Testcontainers.MsSql`.
- **Schema suite**: self-referencing FKs, circular FKs across tables, composite PKs, filtered
  indexes, computed columns, `WITHOUT ROWID` candidates, reserved-word and Unicode identifiers,
  cross-schema name collisions.
- **Scale suite**: 10M-row synthetic table — assert a throughput floor, flat memory profile, and that
  cancel returns within one batch.
- **Workflow suite**: every step type unit-tested; DAG cycle detection; dry-run schema equals
  full-run schema.
- **E2E**: Playwright connecting over CDP to the WebView2 instance of the **packaged** app —
  connect → plan → run → browse → edit → save.
- **Packaging smoke test**: install the built artifact on a clean Windows CI image with no .NET SDK
  and no ODBC driver, launch it, and complete one conversion. This is the test that actually enforces
  §2.2; without it the bundling policy is just a paragraph.

**Auth in the test harness.** The `mcr.microsoft.com/mssql/server` Linux container does not do
Windows auth without an AD domain join, so the fidelity and scale suites connect with SQL auth. That
mode exists in `Sqlm.Core` as a **test-only** code path — never surfaced in the UI, never valid in a
`.sqlmproj`, guarded by a build flag so it cannot ship. The real SSPI handshake is covered by a
separate CI job against a Windows-hosted SQL Server Express instance, plus a manual pre-release check
against a domain-joined server.

---

## 12. Key risks & mitigations

| Risk | Mitigation |
|---|---|
| `decimal`/`money` precision loss | Read via `GetSqlDecimal`/`GetSqlMoney`, default to TEXT, warn per column, make REAL an explicit opt-in. Covered by the fidelity suite. |
| CI cannot exercise Windows SSO against a Linux container | Test-only SQL-auth path in `Sqlm.Core` (§11) + one Windows-hosted SQL Server CI job for the real handshake. |
| WebView2 runtime missing on an old image | Installer chains the bootstrapper; the app detects absence at startup and shows a plain-language message instead of crashing. |
| Self-contained publish bloats the installer (~70–150 MB) | Accepted — it is the cost of zero prerequisites. `PublishReadyToRun` yes, trimming/AOT no (§2.2). |
| `TrustServerCertificate=False` breaks against internal self-signed certs | Detect the specific TLS failure and offer the per-connection opt-in inline, with the risk stated — do not silently disable validation. |
| Huge tables exhaust memory | `SequentialAccess` + streamed BLOBs + bounded channel; nothing is fully buffered. Enforced by the scale suite's memory assertion. |
| `runSql` / advanced expressions run project-authored code | Sandboxed by default; Jint with no CLR access; plan review shows every statement; warn on projects of unknown origin. |
| Scope creep in workflows | Freeze the v1 step list; anything else is `runSql` until there is real demand. |
| SQLite single-writer limits perceived speed | Parallel readers, serialized writer, and honest rows/s + ETA in the status bar. |
| Contract drift between C# and TypeScript | TS types are **generated** from `Sqlm.Contracts` in CI; a stale checked-in `contracts.d.ts` fails the build. |

---

## 13. Development environment

The stack is deliberately Windows-only at the edges (SSO, WPF, WebView2) but portable at the core,
so the dev environment splits along the same line. See `.devcontainer/README.md` for details.

| | Dev container (Linux) | Windows host |
|---|---|---|
| `Sqlm.Core`, `Sqlm.Cli` | ✅ build + test | ✅ |
| `src/renderer` (Vite dev server) | ✅ | ✅ |
| Unit / fidelity / scale suites | ✅ against the `mssql` compose service | ✅ |
| `Sqlm.App` (WPF + WebView2) | ❌ Windows-only | ✅ |
| Windows SSO handshake | ❌ no Windows token on Linux | ✅ via LocalDB / SQL Express |
| E2E (Playwright → WebView2) | ❌ | ✅ |

The container runs `mcr.microsoft.com/mssql/server:2022-latest` as a compose service and connects
with **SQL auth**, because that image will not accept Windows auth without an AD domain join. This is
the §11 test-only path and nothing more.

**The trap to avoid:** a green container build says nothing about SSO. Local SSO verification uses
`sqllocaldb` on the host — LocalDB accepts Windows auth *only*, so it exercises the real SSPI path
without needing a domain. That plus the Windows CI job are what actually cover requirement 1.1.

.NET 10 is the target; the container installs 8.0 alongside it so the engine can be checked against
both while the host toolchain catches up.

---

## 14. Immediate next steps

1. **Phase 0 scaffolding** — solution layout, WPF+WebView2 host, RPC bridge, TS contract generation,
   Windows CI with the exact-version and no-remote-asset gates from §2.2 in place from day one.
2. Write `docs/type-mapping.md` as the normative spec **before** writing the mapper — the fidelity
   suite is generated from it, so the spec is the test.
3. Stand up the `Testcontainers.MsSql` fixture and the Windows SQL Server Express CI job early; they
   gate every meaningful test.
4. Prove the riskiest assumption first: a throwaway spike that opens an SSO connection against
   LocalDB, reads one table with `SequentialAccess`, and writes it to SQLite through the bounded
   channel. If anything in this plan is wrong, it is in that path, and it is a day to find out.
