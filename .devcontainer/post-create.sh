#!/usr/bin/env bash
set -euo pipefail

echo "==> .NET SDKs"
dotnet --list-sdks

echo "==> Node / pnpm"
node --version
corepack enable
corepack prepare pnpm@latest --activate >/dev/null 2>&1 || npm install -g pnpm
pnpm --version

echo "==> Restoring .NET (locked mode when a lock file exists)"
if compgen -G "**/packages.lock.json" >/dev/null 2>&1; then
  dotnet restore --locked-mode
elif compgen -G "*.sln" >/dev/null 2>&1 || compgen -G "src/**/*.csproj" >/dev/null 2>&1; then
  dotnet restore
else
  echo "    (no solution/projects yet - skipping)"
fi

echo "==> Restoring renderer"
if [ -f src/renderer/package.json ]; then
  (cd src/renderer && pnpm install --frozen-lockfile)
else
  echo "    (no renderer yet - skipping)"
fi

echo "==> Waiting for SQL Server"
for i in $(seq 1 30); do
  if (echo > /dev/tcp/mssql/1433) >/dev/null 2>&1; then
    echo "    mssql:1433 reachable"
    break
  fi
  sleep 2
done

cat <<'EOF'

Ready.

  SQL Server        mssql:1433  (sa / $MSSQL_SA_PASSWORD)  -- test-only SQL auth, PLAN.md §11
  Connection string $SQLM_TEST_MSSQL

  NOT available in this container (by design, see .devcontainer/README.md):
    - Windows SSO / integrated auth  -> needs a Windows host token
    - Sqlm.App (WPF + WebView2)      -> Windows-only; build and run it on the host

EOF
