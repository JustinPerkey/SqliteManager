/** PLAN.md §9 layout: "phase · rows/s · ETA · warnings(3) · [Cancel]". Idle until a job runs. */
export function StatusBar() {
  return (
    <footer className="flex h-7 shrink-0 items-center gap-4 border-t border-[var(--sqlm-border)] bg-[var(--sqlm-bg-inset)] px-3 text-xs text-[var(--sqlm-fg-muted)]">
      <span>idle</span>
      <span className="ml-auto">no warnings</span>
    </footer>
  )
}
