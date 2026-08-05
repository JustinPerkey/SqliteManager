import { create } from 'zustand'

export const TABS = ['schema', 'workflow', 'data', 'sql', 'log'] as const
export type TabId = (typeof TABS)[number]

interface AppState {
  activeTab: TabId
  setActiveTab: (tab: TabId) => void
}

export const useAppStore = create<AppState>((set) => ({
  activeTab: 'schema',
  setActiveTab: (tab) => set({ activeTab: tab }),
}))
