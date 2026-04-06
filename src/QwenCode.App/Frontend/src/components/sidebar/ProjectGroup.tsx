// Frontend/src/components/sidebar/ProjectGroup.tsx
import { useState } from 'react'
import { Folder, FolderOpen, ChevronRight } from 'lucide-react'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import type { SessionPreview } from '@/types/desktop'

interface ProjectGroupProps {
  projectName: string
  sessions: SessionPreview[]
  selectedSessionId: string
  activeTurnSessions: Record<string, true>
  onSelectSession: (sessionId: string) => void
}

function formatRelativeTime(lastActivity: string): string {
  if (!lastActivity.includes('T') && !lastActivity.includes('-')) {
    return lastActivity
  }
  const ms = Date.now() - Date.parse(lastActivity)
  if (ms < 60_000) return 'сейчас'
  if (ms < 3_600_000) return `${Math.floor(ms / 60_000)}м`
  if (ms < 86_400_000) return `${Math.floor(ms / 3_600_000)}ч`
  return `${Math.floor(ms / 86_400_000)}д`
}

export function ProjectGroup({
  projectName,
  sessions,
  selectedSessionId,
  activeTurnSessions,
  onSelectSession,
}: ProjectGroupProps) {
  const [open, setOpen] = useState(true)

  return (
    <Collapsible open={open} onOpenChange={(nextOpen: boolean) => setOpen(nextOpen)}>
      <CollapsibleTrigger
        className="flex w-full items-center gap-1.5 px-2.5 py-1 text-xs font-medium text-[--app-muted] hover:text-[--app-text] transition-colors"
      >
        <ChevronRight
          size={12}
          className={`shrink-0 transition-transform duration-150 ${open ? 'rotate-90' : ''}`}
        />
        {open ? (
          <FolderOpen size={13} className="shrink-0 text-[--app-muted]" />
        ) : (
          <Folder size={13} className="shrink-0 text-[--app-muted]" />
        )}
        <span className="truncate">{projectName}</span>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <div className="flex flex-col gap-0.5 pb-1">
          {sessions.map((session) => {
            const isSelected = session.sessionId === selectedSessionId
            const isActive = Boolean(activeTurnSessions[session.sessionId])

            return (
              <button
                key={session.sessionId}
                type="button"
                onClick={() => onSelectSession(session.sessionId)}
                className={`flex w-full items-center justify-between gap-2 rounded-md px-3 py-1.5 text-left transition-colors ${
                  isSelected
                    ? 'bg-[--app-elevated] text-[--app-text]'
                    : 'text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]'
                }`}
              >
                <span className="flex-1 truncate text-sm leading-snug">
                  {session.title}
                </span>
                <span className="flex shrink-0 items-center gap-1.5 text-xs text-[--app-muted]">
                  {isActive && (
                    <span className="h-1.5 w-1.5 rounded-full bg-orange-500 animate-pulse" />
                  )}
                  {formatRelativeTime(session.lastActivity)}
                </span>
              </button>
            )
          })}
        </div>
      </CollapsibleContent>
    </Collapsible>
  )
}
