import { useMemo, useState } from 'react'
import {
  AlertTriangle,
  Bot,
  CheckCircle,
  ChevronDown,
  ChevronRight,
  CircleHelp,
  Loader2,
  Wrench,
  XCircle,
} from 'lucide-react'
import { ChatTimestamp } from '@/components/chat/ChatTimestamp'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Input } from '@/components/ui/input'
import type { DesktopQuestionAnswer, DesktopSessionEntry } from '@/types/desktop'

interface EventCardProps {
  entry: DesktopSessionEntry
  isApproving?: boolean
  isAnswering?: boolean
  onApprove?: (entryId: string) => void
  onAnswer?: (entryId: string, answers: DesktopQuestionAnswer[]) => void
  compact?: boolean
}

function StatusBadge({ status }: { status: string }) {
  if (status === 'completed' || status === 'succeeded') {
    return (
      <span className="flex items-center gap-1 text-xs text-emerald-400">
        <CheckCircle size={12} />
        Done
      </span>
    )
  }
  if (status === 'failed' || status === 'error') {
    return (
      <span className="flex items-center gap-1 text-xs text-red-400">
        <XCircle size={12} />
        Error
      </span>
    )
  }
  if (status === 'running' || status === 'pending') {
    return (
      <span className="flex items-center gap-1 text-xs text-[--app-muted]">
        <Loader2 size={12} className="animate-spin" />
        Running
      </span>
    )
  }
  if (status === 'blocked') {
    return (
      <span className="flex items-center gap-1 text-xs text-orange-400">
        <AlertTriangle size={12} />
        Blocked
      </span>
    )
  }
  return null
}

function summarizeText(value: string, maxLength = 120) {
  const normalized = value.replace(/\s+/g, ' ').trim()
  if (!normalized) return ''
  return normalized.length > maxLength ? `${normalized.slice(0, maxLength - 3)}...` : normalized
}

function getContextSummary(entry: DesktopSessionEntry) {
  const bodySummary = summarizeText(entry.body)
  if (bodySummary && bodySummary !== entry.title) return bodySummary

  const argsSummary = summarizeText(entry.arguments)
  if (argsSummary) return argsSummary

  if (entry.changedFiles.length > 0) {
    return `${entry.changedFiles.length} file${entry.changedFiles.length === 1 ? '' : 's'} changed`
  }

  return ''
}

export function EventCard({
  entry,
  isApproving,
  isAnswering,
  onApprove,
  onAnswer,
  compact = false,
}: EventCardProps) {
  const needsApproval = entry.approvalState === 'pending-approval' || entry.type === 'tool-approval-required'
  const needsAnswer = entry.type === 'question' || entry.questions.length > 0
  const isSubAgent = entry.scope === 'subagent' || entry.toolName === 'Agent'
  const [open, setOpen] = useState(needsAnswer)
  const [freeformAnswers, setFreeformAnswers] = useState<Record<number, string>>({})
  const [selectedOptions, setSelectedOptions] = useState<Record<number, string[]>>({})
  const contextSummary = getContextSummary(entry)

  const answers = useMemo<DesktopQuestionAnswer[]>(
    () =>
      entry.questions.map((question, questionIndex) => ({
        questionIndex,
        value:
          question.options.length > 0
            ? (selectedOptions[questionIndex] ?? []).join(', ')
            : (freeformAnswers[questionIndex] ?? '').trim(),
      })),
    [entry.questions, freeformAnswers, selectedOptions],
  )

  const canSubmitAnswers = needsAnswer && answers.every((answer) => Boolean(answer.value.trim()))

  const toggleOption = (questionIndex: number, value: string, multiSelect: boolean) => {
    setSelectedOptions((current) => {
      const selected = current[questionIndex] ?? []
      const exists = selected.includes(value)

      if (multiSelect) {
        return {
          ...current,
          [questionIndex]: exists
            ? selected.filter((item) => item !== value)
            : [...selected, value],
        }
      }

      return {
        ...current,
        [questionIndex]: exists ? [] : [value],
      }
    })
  }

  return (
    <div
      className={`${compact ? 'mx-0 my-0' : 'mx-4 my-2 max-w-3xl'} rounded-2xl border ${
        needsApproval
          ? 'border-l-2 border-l-orange-500 border-[--app-border] bg-orange-500/5'
          : 'border-[--app-border] bg-[--app-panel]'
      } shadow-[0_24px_80px_-48px_rgba(0,0,0,0.9)]`}
    >
      <Collapsible open={open} onOpenChange={setOpen}>
        <div className="flex items-start justify-between gap-3 px-4 py-3">
          <div className="flex min-w-0 gap-3">
            <div className="shrink-0 text-[--app-muted]">
              <div className="flex h-7 w-7 items-center justify-center rounded-full border border-[--app-border] bg-[--app-elevated]">
                {needsAnswer ? <CircleHelp size={14} /> : isSubAgent ? <Bot size={14} /> : <Wrench size={14} />}
              </div>
            </div>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm font-medium text-[--app-text]">
                  {entry.toolName || entry.title}
                </span>
                {entry.timestamp && <ChatTimestamp value={entry.timestamp} />}
              </div>
              {contextSummary && <p className="mt-1 text-xs text-[--app-muted]">{contextSummary}</p>}
            </div>
          </div>

          <div className="flex shrink-0 items-center gap-2 pt-0.5">
            <StatusBadge status={entry.status} />
            {(entry.body || needsAnswer) && (
              <CollapsibleTrigger className="text-[--app-muted] transition-colors hover:text-[--app-text]">
                {open ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
              </CollapsibleTrigger>
            )}
          </div>
        </div>

        {needsApproval && onApprove && (
          <div className="flex items-center justify-end gap-2 border-t border-[--app-border] px-4 py-2.5">
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs text-[--app-muted] hover:text-[--app-text]"
            >
              Reject
            </Button>
            <Button
              size="sm"
              className="h-7 bg-orange-500 text-xs text-white hover:bg-orange-600"
              onClick={() => onApprove(entry.id)}
              disabled={isApproving}
            >
              {isApproving ? <Loader2 size={12} className="mr-1 animate-spin" /> : null}
              Accept
            </Button>
          </div>
        )}

        {needsAnswer && onAnswer && (
          <div className="border-t border-[--app-border] px-4 py-3">
            <div className="flex flex-col gap-3">
              {entry.questions.map((question, questionIndex) => (
                <div key={`${entry.id}-${questionIndex}`} className="flex flex-col gap-2">
                  <div className="flex flex-col gap-1">
                    <span className="text-xs font-medium text-[--app-text]">
                      {question.header || `Question ${questionIndex + 1}`}
                    </span>
                    <span className="text-xs text-[--app-muted]">{question.question}</span>
                  </div>

                  {question.options.length > 0 ? (
                    <div className="flex flex-wrap gap-2">
                      {question.options.map((option) => {
                        const isSelected = (selectedOptions[questionIndex] ?? []).includes(option.label)
                        return (
                          <button
                            key={`${questionIndex}-${option.label}`}
                            type="button"
                            className={`rounded-md border px-3 py-2 text-left text-xs transition-colors ${
                              isSelected
                                ? 'border-orange-500/50 bg-orange-500/10 text-[--app-text]'
                                : 'border-[--app-border] bg-[--app-bg] text-[--app-muted] hover:text-[--app-text]'
                            }`}
                            onClick={() => toggleOption(questionIndex, option.label, question.multiSelect)}
                          >
                            <span className="block font-medium">{option.label}</span>
                            {option.description && (
                              <span className="mt-1 block text-[10px] text-[--app-muted]">
                                {option.description}
                              </span>
                            )}
                          </button>
                        )
                      })}
                    </div>
                  ) : (
                    <Input
                      value={freeformAnswers[questionIndex] ?? ''}
                      onChange={(event) =>
                        setFreeformAnswers((current) => ({
                          ...current,
                          [questionIndex]: event.target.value,
                        }))
                      }
                      placeholder="Type your answer"
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  )}
                </div>
              ))}

              <div className="flex justify-end">
                <Button
                  size="sm"
                  className="h-8 bg-orange-500 text-white hover:bg-orange-600"
                  disabled={!canSubmitAnswers || isAnswering}
                  onClick={() => onAnswer(entry.id, answers)}
                >
                  {isAnswering ? <Loader2 size={12} className="mr-1 animate-spin" /> : null}
                  Send answers
                </Button>
              </div>
            </div>
          </div>
        )}

        <CollapsibleContent>
          <div className="border-t border-[--app-border] px-4 py-3">
            <pre className="max-h-48 overflow-y-auto whitespace-pre-wrap break-words font-mono text-xs text-[--app-muted]">
              {entry.body}
            </pre>
          </div>
        </CollapsibleContent>
      </Collapsible>
    </div>
  )
}
