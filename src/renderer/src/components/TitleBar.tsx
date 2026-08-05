interface TitleBarProps {
  projectName: string
}

/**
 * PLAN.md §9 layout: title bar with the project name and the primary lifecycle actions.
 * The actions are disabled until a project/connection exists — Phase 1+ wires them up.
 */
export function TitleBar({ projectName }: TitleBarProps) {
  return (
    <header className="flex h-11 shrink-0 items-center gap-3 border-b border-[var(--sqlm-border)] bg-[var(--sqlm-bg-inset)] px-3 text-sm">
      <span className="font-medium text-[var(--sqlm-fg)]">SqliteManager</span>
      <span className="text-[var(--sqlm-fg-muted)]">{projectName}</span>
      <div className="ml-auto flex gap-2">
        {['Connect', 'Plan', 'Run', 'Dry run'].map((action) => (
          <button
            key={action}
            type="button"
            disabled
            className="rounded border border-[var(--sqlm-border)] px-2 py-1 text-xs text-[var(--sqlm-fg-muted)] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {action}
          </button>
        ))}
      </div>
    </header>
  )
}
