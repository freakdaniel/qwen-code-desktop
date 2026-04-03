import { GitBranch, MoreHorizontal, Play, Square } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { SessionPreview } from '@/types/desktop'

interface ChatHeaderProps {
  session: SessionPreview
  isActive: boolean
  onCancel: () => void
}

function getProjectName(workingDirectory: string): string {
  return workingDirectory.split(/[\\/]/).filter(Boolean).at(-1) ?? workingDirectory
}

export function ChatHeader({ session, isActive, onCancel }: ChatHeaderProps) {
  const projectName = getProjectName(session.workingDirectory)

  return (
    <div className="shrink-0 border-b border-[--app-border] bg-[--app-bg]">
      <div className="mx-auto flex w-full max-w-5xl items-center justify-between gap-3 px-4 py-3">
        <div className="min-w-0 flex items-center gap-3">
          <span className="max-w-[min(56vw,32rem)] truncate text-sm font-semibold text-[--app-text]">
            {session.title}
          </span>
          <div className="hidden items-center gap-2 text-xs text-[--app-muted] md:flex">
            <span className="truncate">{projectName}</span>
            {session.gitBranch && (
              <>
                <span className="text-[--app-border]">|</span>
                <span className="flex items-center gap-1">
                  <GitBranch size={11} />
                  {session.gitBranch}
                </span>
              </>
            )}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-1">
          {isActive ? (
            <Button
              variant="ghost"
              size="sm"
              className="gap-1.5 text-xs text-orange-400 hover:bg-orange-500/10 hover:text-orange-300"
              onClick={onCancel}
            >
              <Square size={13} />
              Stop
            </Button>
          ) : (
            <Button
              variant="ghost"
              size="icon-sm"
              className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
            >
              <Play size={14} />
            </Button>
          )}
          <Button
            variant="ghost"
            size="icon-sm"
            className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
          >
            <MoreHorizontal size={15} />
          </Button>
        </div>
      </div>
    </div>
  )
}
