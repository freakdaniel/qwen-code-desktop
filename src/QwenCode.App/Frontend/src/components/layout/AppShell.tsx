// Frontend/src/components/layout/AppShell.tsx
import type { ReactNode } from 'react'

interface AppShellProps {
  sidebar: ReactNode
  main: ReactNode
}

export function AppShell({ sidebar, main }: AppShellProps) {
  return (
    <div className="flex h-screen w-screen overflow-hidden bg-[--app-bg] text-[--app-text]">
      <aside className="w-72 shrink-0 flex flex-col h-full bg-[--app-sidebar] border-r border-[--app-border]">
        {sidebar}
      </aside>
      <main className="flex-1 min-w-0 h-full overflow-hidden">
        {main}
      </main>
    </div>
  )
}
