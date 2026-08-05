const SECTIONS = ['Connections', 'Source tree', 'Target tree'] as const

/** PLAN.md §9 layout: connections + source/target object trees. Populated from Phase 1. */
export function Sidebar() {
  return (
    <nav
      aria-label="Project navigator"
      className="w-56 shrink-0 overflow-y-auto border-r border-[var(--sqlm-border)] bg-[var(--sqlm-bg-inset)] p-2 text-sm"
    >
      {SECTIONS.map((section) => (
        <div key={section} className="mb-3">
          <h2 className="px-1 text-xs font-semibold uppercase tracking-wide text-[var(--sqlm-fg-muted)]">
            {section}
          </h2>
          <p className="px-1 py-2 text-[var(--sqlm-fg-muted)] italic">Nothing yet</p>
        </div>
      ))}
    </nav>
  )
}
