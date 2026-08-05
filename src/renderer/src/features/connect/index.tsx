/**
 * SSO connection manager + recent servers (PLAN.md §1.1, §10 Phase 1). Not a tab like the other
 * features — it gates the rest of the shell until a source connection exists.
 */
export function ConnectPanel() {
  return (
    <div className="flex h-full items-center justify-center p-8 text-center text-sm text-[var(--sqlm-fg-muted)]">
      <p>No connection yet. Windows SSO connect — nothing to show yet.</p>
    </div>
  )
}
