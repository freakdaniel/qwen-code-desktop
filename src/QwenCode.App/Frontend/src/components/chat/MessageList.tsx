import { useEffect, useMemo, useRef } from 'react'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Button } from '@/components/ui/button'
import { EventCard } from './EventCard'
import { MessageBubble } from './MessageBubble'
import { ToolCallGroup } from './ToolCallGroup'
import type { DesktopQuestionAnswer, DesktopSessionDetail } from '@/types/desktop'

interface MessageListProps {
  detail: DesktopSessionDetail
  streamingText: string
  approvingEntryId: string
  answeringEntryId: string
  isLoadingSession: boolean
  onApprove: (entryId: string) => void
  onAnswer: (entryId: string, answers: DesktopQuestionAnswer[]) => void
  onLoadOlder: () => void
  onLoadNewer: () => void
}

const USER_TYPES = new Set(['user', 'human'])
const ASSISTANT_TYPES = new Set(['assistant', 'ai'])
const EVENT_TYPES = new Set([
  'tool', 'tool-approval-required', 'question',
  'command', 'subagent', 'system',
])

type SessionEntry = DesktopSessionDetail['entries'][number]

type RenderBlock =
  | { kind: 'message'; entry: SessionEntry }
  | { kind: 'event-group'; entries: SessionEntry[] }

export function MessageList({
  detail,
  streamingText,
  approvingEntryId,
  answeringEntryId,
  isLoadingSession,
  onApprove,
  onAnswer,
  onLoadOlder,
  onLoadNewer,
}: MessageListProps) {
  const bottomRef = useRef<HTMLDivElement>(null)
  const userScrolledUpRef = useRef(false)
  const blocks = useMemo<RenderBlock[]>(() => {
    const nextBlocks: RenderBlock[] = []
    let currentEventGroup: SessionEntry[] = []

    const flushEventGroup = () => {
      if (currentEventGroup.length === 0) return
      nextBlocks.push({ kind: 'event-group', entries: currentEventGroup })
      currentEventGroup = []
    }

    for (const entry of detail.entries) {
      const isUser = USER_TYPES.has(entry.type)
      const isAssistant = ASSISTANT_TYPES.has(entry.type)
      const isEvent = EVENT_TYPES.has(entry.type) || Boolean(entry.toolName)

      if (isUser || isAssistant) {
        flushEventGroup()
        nextBlocks.push({ kind: 'message', entry })
        continue
      }

      if (isEvent) {
        currentEventGroup.push(entry)
        continue
      }

      flushEventGroup()
    }

    flushEventGroup()
    return nextBlocks
  }, [detail.entries])

  useEffect(() => {
    if (!userScrolledUpRef.current) {
      bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
    }
  }, [detail.entries, streamingText])

  return (
    <ScrollArea
      className="flex-1 min-h-0"
      onScrollCapture={(e: React.SyntheticEvent) => {
        const el = e.currentTarget.querySelector('[data-slot="scroll-area-viewport"]') as HTMLElement
        if (!el) return
        const atBottom = el.scrollTop + el.clientHeight >= el.scrollHeight - 32
        userScrolledUpRef.current = !atBottom
      }}
    >
      <div className="flex flex-col gap-1 px-2 py-5">
        {detail.hasOlderEntries && (
          <div className="flex justify-center py-2">
            <Button
              variant="ghost"
              size="sm"
              className="text-xs text-[--app-muted] hover:text-[--app-text]"
              onClick={onLoadOlder}
              disabled={isLoadingSession}
            >
              Load earlier
            </Button>
          </div>
        )}

        {blocks.map((block) => {
          if (block.kind === 'message') {
            const role = USER_TYPES.has(block.entry.type) ? 'user' : 'assistant'
            return (
              <MessageBubble
                key={block.entry.id}
                role={role}
                content={block.entry.body || block.entry.title}
                timestamp={block.entry.timestamp}
              />
            )
          }

          if (block.entries.length === 1) {
            const entry = block.entries[0]
            return (
              <EventCard
                key={entry.id}
                entry={entry}
                isApproving={approvingEntryId === entry.id}
                isAnswering={answeringEntryId === entry.id}
                onApprove={onApprove}
                onAnswer={onAnswer}
              />
            )
          }

          return (
            <ToolCallGroup
              key={block.entries.map((entry) => entry.id).join(':')}
              entries={block.entries}
              approvingEntryId={approvingEntryId}
              answeringEntryId={answeringEntryId}
              onApprove={onApprove}
              onAnswer={onAnswer}
            />
          )
        })}

        {streamingText && (
          <MessageBubble role="assistant" content={streamingText} />
        )}

        {detail.hasNewerEntries && (
          <div className="flex justify-center py-2">
            <Button
              variant="ghost"
              size="sm"
              className="text-xs text-[--app-muted] hover:text-[--app-text]"
              onClick={onLoadNewer}
              disabled={isLoadingSession}
            >
              Load newer
            </Button>
          </div>
        )}

        <div ref={bottomRef} />
      </div>
    </ScrollArea>
  )
}
