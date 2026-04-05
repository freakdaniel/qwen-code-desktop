import { useMemo, useState } from 'react'
import { Bot, ChevronDown, ChevronRight, CircleHelp, Wrench } from 'lucide-react'
import { ChatTimestamp } from '@/components/chat/ChatTimestamp'
import { EventCard } from '@/components/chat/EventCard'
import { Badge } from '@/components/ui/badge'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import type { DesktopQuestionAnswer, DesktopSessionEntry } from '@/types/desktop'

interface ToolCallGroupProps {
  entries: DesktopSessionEntry[]
  approvingEntryId: string
  answeringEntryId: string
  onApprove: (entryId: string) => void
  onAnswer: (entryId: string, answers: DesktopQuestionAnswer[]) => void
}

function summarizeText(value: string, maxLength = 140) {
  const normalized = value.replace(/\s+/g, ' ').trim()
  if (!normalized) return ''
  return normalized.length > maxLength ? `${normalized.slice(0, maxLength - 3)}...` : normalized
}

function getEntryContext(entry: DesktopSessionEntry) {
  const bodySummary = summarizeText(entry.body)
  if (bodySummary && bodySummary !== entry.title) return bodySummary

  const argsSummary = summarizeText(entry.arguments)
  if (argsSummary) return argsSummary

  if (entry.changedFiles.length > 0) {
    return `${entry.changedFiles.length} file${entry.changedFiles.length === 1 ? '' : 's'} changed`
  }

  return ''
}

export function ToolCallGroup({
  entries,
  approvingEntryId,
  answeringEntryId,
  onApprove,
  onAnswer,
}: ToolCallGroupProps) {
  const actionable = entries.some((entry) =>
    entry.approvalState === 'pending-approval' || entry.type === 'tool-approval-required' || entry.questions.length > 0,
  )
  const [open, setOpen] = useState(actionable)

  const summary = useMemo(() => {
    const labels = [...new Set(entries.map((entry) => entry.toolName || entry.title).filter(Boolean))].slice(0, 4)
    const context = entries.map(getEntryContext).find(Boolean) ?? ''
    const hasQuestion = entries.some((entry) => entry.type === 'question' || entry.questions.length > 0)
    const hasSubAgent = entries.some((entry) => entry.scope === 'subagent' || entry.toolName === 'Agent')

    return {
      labels,
      context,
      hasQuestion,
      hasSubAgent,
      timestamp: entries[entries.length - 1]?.timestamp ?? '',
    }
  }, [entries])

  const groupLabel = summary.hasQuestion
    ? `${entries.length} decision steps`
    : summary.hasSubAgent
      ? `${entries.length} agent actions`
      : `${entries.length} tool calls`

  return (
    <div className="mx-4 my-2 max-w-3xl rounded-2xl border border-[--app-border] bg-[--app-panel]/80 shadow-[0_24px_80px_-48px_rgba(0,0,0,0.9)]">
      <Collapsible open={open} onOpenChange={setOpen}>
        <div className="flex items-start justify-between gap-3 px-4 py-3">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-[--app-elevated] text-[--app-muted]">
                {summary.hasQuestion ? <CircleHelp size={14} /> : summary.hasSubAgent ? <Bot size={14} /> : <Wrench size={14} />}
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-[--app-text]">{groupLabel}</span>
                  <ChatTimestamp value={summary.timestamp} />
                </div>
                {summary.context && <p className="mt-1 truncate text-xs text-[--app-muted]">{summary.context}</p>}
              </div>
            </div>

            {summary.labels.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-2">
                {summary.labels.map((label) => (
                  <Badge
                    key={label}
                    variant="outline"
                    className="border-[--app-border] bg-[--app-bg] text-[11px] text-[--app-muted]"
                  >
                    {label}
                  </Badge>
                ))}
              </div>
            )}
          </div>

          <CollapsibleTrigger className="mt-0.5 rounded-full p-1.5 text-[--app-muted] transition-colors hover:bg-[--app-elevated] hover:text-[--app-text]">
            {open ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
          </CollapsibleTrigger>
        </div>

        <CollapsibleContent>
          <div className="flex flex-col gap-2 border-t border-[--app-border] px-3 pb-3 pt-2">
            {entries.map((entry) => (
              <EventCard
                key={entry.id}
                entry={entry}
                isApproving={approvingEntryId === entry.id}
                isAnswering={answeringEntryId === entry.id}
                onApprove={onApprove}
                onAnswer={onAnswer}
                compact
              />
            ))}
          </div>
        </CollapsibleContent>
      </Collapsible>
    </div>
  )
}
