import * as Tabs from '@radix-ui/react-tabs'
import type { ReactNode } from 'react'
import { TABS, useAppStore, type TabId } from '../store/useAppStore'

const LABELS: Record<TabId, string> = {
  schema: 'Schema map',
  workflow: 'Workflow',
  data: 'Data',
  sql: 'SQL',
  log: 'Log',
}

interface TabStripProps {
  panels: Record<TabId, ReactNode>
}

/** PLAN.md §9 layout: "Tab strip: Schema map · Workflow · Data · SQL · Log". */
export function TabStrip({ panels }: TabStripProps) {
  const activeTab = useAppStore((state) => state.activeTab)
  const setActiveTab = useAppStore((state) => state.setActiveTab)

  return (
    <Tabs.Root
      value={activeTab}
      onValueChange={(value) => setActiveTab(value as TabId)}
      className="flex min-h-0 flex-1 flex-col"
    >
      <Tabs.List className="flex shrink-0 border-b border-[var(--sqlm-border)]" aria-label="Views">
        {TABS.map((tab) => (
          <Tabs.Trigger
            key={tab}
            value={tab}
            className="border-b-2 border-transparent px-3 py-2 text-sm text-[var(--sqlm-fg-muted)] data-[state=active]:border-[var(--sqlm-accent)] data-[state=active]:text-[var(--sqlm-fg)]"
          >
            {LABELS[tab]}
          </Tabs.Trigger>
        ))}
      </Tabs.List>
      {TABS.map((tab) => (
        <Tabs.Content key={tab} value={tab} className="min-h-0 flex-1 overflow-auto">
          {panels[tab]}
        </Tabs.Content>
      ))}
    </Tabs.Root>
  )
}
