import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TitleBar } from './components/TitleBar'
import { Sidebar } from './components/Sidebar'
import { TabStrip } from './components/TabStrip'
import { StatusBar } from './components/StatusBar'
import { SchemaPanel } from './features/schema'
import { WorkflowPanel } from './features/workflow'
import { DataPanel } from './features/data'
import { SqlPanel } from './features/sql'
import { LogPanel } from './features/log'

const queryClient = new QueryClient()

// PLAN.md §9 shell layout: title bar / sidebar + tab strip / status bar. Each tab panel is a
// feature under src/features/ (§3); Phase 1+ fills them in behind the same RPC bridge.
function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <div className="flex h-screen flex-col">
        <TitleBar projectName="No project open" />
        <div className="flex min-h-0 flex-1">
          <Sidebar />
          <TabStrip
            panels={{
              schema: <SchemaPanel />,
              workflow: <WorkflowPanel />,
              data: <DataPanel />,
              sql: <SqlPanel />,
              log: <LogPanel />,
            }}
          />
        </div>
        <StatusBar />
      </div>
    </QueryClientProvider>
  )
}

export default App
