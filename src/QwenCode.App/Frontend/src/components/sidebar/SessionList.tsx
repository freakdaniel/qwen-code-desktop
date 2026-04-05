// Frontend/src/components/sidebar/SessionList.tsx
import { useDeferredValue, useState } from 'react'
import { Search } from 'lucide-react'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Input } from '@/components/ui/input'
import { ProjectGroup } from './ProjectGroup'
import type { SessionPreview } from '@/types/desktop'

interface SessionListProps {
  sessions: SessionPreview[]
  selectedSessionId: string
  activeTurnSessions: Record<string, true>
  onSelectSession: (sessionId: string) => void
}

function getProjectName(workingDirectory: string): string {
  return workingDirectory.split(/[\\/]/).filter(Boolean).at(-1) ?? workingDirectory
}

function groupByProject(sessions: SessionPreview[]) {
  const map = new Map<string, { projectName: string; sessions: SessionPreview[] }>()
  for (const session of sessions) {
    const key = session.workingDirectory
    if (!map.has(key)) {
      map.set(key, { projectName: getProjectName(key), sessions: [] })
    }
    map.get(key)!.sessions.push(session)
  }
  return [...map.entries()].map(([key, value]) => ({ key, ...value }))
}

export function SessionList({
  sessions,
  selectedSessionId,
  activeTurnSessions,
  onSelectSession,
}: SessionListProps) {
  const [query, setQuery] = useState('')
  const deferredQuery = useDeferredValue(query)

  const filtered = deferredQuery
    ? sessions.filter((s) =>
        `${s.title} ${s.category} ${s.gitBranch}`.toLowerCase().includes(deferredQuery.toLowerCase()),
      )
    : sessions

  const groups = groupByProject(filtered)

  return (
    <div className="flex flex-col flex-1 min-h-0 gap-1">
      <div className="px-2 pt-1 pb-1">
        <div className="relative">
          <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-[--app-muted]" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Поиск бесед..."
            className="h-7 pl-7 text-sm bg-[--app-elevated] border-[--app-border] text-[--app-text] placeholder:text-[--app-muted] focus-visible:ring-0 focus-visible:border-[--app-border]"
          />
        </div>
      </div>

      <div className="px-2 pb-1">
        <span className="text-xs font-medium text-[--app-muted] uppercase tracking-wide px-0.5">
          Беседы
        </span>
      </div>

      <ScrollArea className="flex-1">
        <div className="flex flex-col gap-1 px-1 pb-2">
          {groups.length === 0 && (
            <p className="px-3 py-4 text-xs text-[--app-muted] text-center">Бесед не найдено</p>
          )}
          {groups.map((group) => (
            <ProjectGroup
              key={group.key}
              projectName={group.projectName}
              sessions={group.sessions}
              selectedSessionId={selectedSessionId}
              activeTurnSessions={activeTurnSessions}
              onSelectSession={onSelectSession}
            />
          ))}
        </div>
      </ScrollArea>
    </div>
  )
}
