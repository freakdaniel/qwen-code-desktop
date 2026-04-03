// Frontend/src/components/layout/Sidebar.tsx
import { SidebarHeader } from '@/components/sidebar/SidebarHeader'
import { SessionList } from '@/components/sidebar/SessionList'
import { SidebarFooter } from '@/components/sidebar/SidebarFooter'
import type { SessionPreview } from '@/types/desktop'
import type { WorkspaceSurface } from '@/types/ui'

interface SidebarProps {
  surface: WorkspaceSurface
  sessions: SessionPreview[]
  selectedSessionId: string
  activeTurnSessions: Record<string, true>
  onNewChat: () => void
  onSelectSession: (sessionId: string) => void
  onOpenSettings: () => void
  onOpenUtilities: () => void
  onUpdate: () => void
}

export function Sidebar({
  surface,
  sessions,
  selectedSessionId,
  activeTurnSessions,
  onNewChat,
  onSelectSession,
  onOpenSettings,
  onOpenUtilities,
  onUpdate,
}: SidebarProps) {
  return (
    <>
      <SidebarHeader
        surface={surface}
        onNewChat={onNewChat}
        onOpenUtilities={onOpenUtilities}
      />
      <SessionList
        sessions={sessions}
        selectedSessionId={selectedSessionId}
        activeTurnSessions={activeTurnSessions}
        onSelectSession={onSelectSession}
      />
      <SidebarFooter
        surface={surface}
        onOpenSettings={onOpenSettings}
        onUpdate={onUpdate}
      />
    </>
  )
}
