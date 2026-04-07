import { useEffect, useRef, useState } from 'react'
import { ArrowUp, Check, Paperclip } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ContextRing } from '@/components/chat/ContextRing'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { AGENT_MODES } from '@/types/ui'
import type { DesktopSessionEvent } from '@/types/desktop'
import type { AgentMode } from '@/types/ui'

interface ChatInputProps {
  sessionId: string
  isSubmitting: boolean
  isActive: boolean
  usedTokens: number
  totalTokens: number
  latestSessionEvent: DesktopSessionEvent | null
  mode: AgentMode
  onModeChange: (mode: AgentMode) => void
  onSubmit: (prompt: string, sessionId: string) => void
}

const DISCLAIMER_TEXT =
  '\u0051\u0077\u0065\u006e \u044d\u0442\u043e \u0418\u0418, \u0438 \u043e\u043d \u043c\u043e\u0436\u0435\u0442 \u0434\u043e\u043f\u0443\u0441\u043a\u0430\u0442\u044c \u043e\u0448\u0438\u0431\u043a\u0438. \u041f\u0440\u043e\u0432\u0435\u0440\u044f\u0439 \u0432\u0430\u0436\u043d\u044b\u0435 \u0438\u0437\u043c\u0435\u043d\u0435\u043d\u0438\u044f, \u043a\u043e\u043c\u0430\u043d\u0434\u044b \u0438 \u043e\u0442\u0432\u0435\u0442\u044b.'

function getContextBarText(event: DesktopSessionEvent | null): string {
  if (!event) return ''
  switch (event.kind) {
    case 'assistantStreaming':
    case 'assistantGenerating':
    case 'assistantPreparingContext':
      return 'Agent is generating a response...'
    case 'toolApprovalRequired':
      return `Awaiting approval: ${event.toolName || event.commandName}`
    case 'userInputRequired':
      return 'Agent is waiting for an answer'
    case 'toolCompleted':
      return event.toolName ? `Completed: ${event.toolName}` : ''
    default:
      return event.message || ''
  }
}

export function ChatInput({
  sessionId,
  isSubmitting,
  isActive,
  usedTokens,
  totalTokens,
  latestSessionEvent,
  mode,
  onModeChange,
  onSubmit,
}: ChatInputProps) {
  const { t } = useTranslation()
  const [prompt, setPrompt] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const contextBarText = getContextBarText(latestSessionEvent)
  const currentModeOption = AGENT_MODES.find((m) => m.value === mode) ?? AGENT_MODES[0]

  useEffect(() => {
    const el = textareaRef.current
    if (!el) return
    el.style.height = 'auto'
    const maxHeight = 24 * 6
    el.style.height = `${Math.min(el.scrollHeight, maxHeight)}px`
  }, [prompt])

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && e.ctrlKey) {
      e.preventDefault()
      handleSubmit()
    }
  }

  const handleSubmit = () => {
    if (!prompt.trim() || isSubmitting) return
    onSubmit(prompt, sessionId)
    setPrompt('')
  }

  return (
    <div className="shrink-0 bg-[--app-bg] px-4 pb-4 pt-3">
      <div className="mx-auto w-full max-w-4xl">
        <div className="overflow-hidden rounded-[28px] border border-[--app-border] bg-[--app-panel] shadow-[0_24px_80px_-48px_rgba(0,0,0,0.95)]">
          {contextBarText && (
            <div className="border-b border-[--app-border] px-5 py-2">
              <span className="block truncate text-xs text-[--app-muted]">{contextBarText}</span>
            </div>
          )}

          <div className="px-5 pt-4">
            <Textarea
              ref={textareaRef}
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={isActive ? 'Continue the conversation...' : 'What needs to be done?'}
              rows={1}
              className="min-h-[96px] resize-none overflow-hidden border-none bg-transparent p-0 text-sm leading-relaxed text-[--app-text] placeholder:text-[--app-muted] focus-visible:ring-0"
            />
          </div>

          <div className="flex items-center justify-between gap-3 px-4 py-3">
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="icon-sm"
                className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
              >
                <Paperclip size={14} />
              </Button>

              <DropdownMenu>
                <DropdownMenuTrigger
                  className="inline-flex h-8 items-center gap-1.5 rounded-full border border-[--app-border] bg-[--app-bg] px-3 text-xs text-[--app-muted] transition-colors hover:bg-[--app-elevated] hover:text-[--app-text]"
                >
                  <span className="rounded-full bg-[--app-elevated] px-1.5 py-0.5 font-mono text-[10px]">
                    {'</>'}
                  </span>
                  {t(currentModeOption.labelKey)}
                  <svg width="10" height="10" viewBox="0 0 10 10" fill="none" className="opacity-50">
                    <path d="M2 3.5L5 6.5L8 3.5" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round" />
                  </svg>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  align="start"
                  className="w-56 border-[--app-border] bg-[--app-elevated] text-[--app-text]"
                >
                  {AGENT_MODES.map((m) => (
                    <DropdownMenuItem
                      key={m.value}
                      onClick={() => onModeChange(m.value)}
                      className="flex cursor-pointer items-start gap-2.5 py-2.5 hover:bg-[--app-elevated-hover] focus:bg-[--app-elevated-hover]"
                    >
                      <div className="mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center">
                        {mode === m.value && <Check size={13} className="text-orange-400" />}
                      </div>
                      <div className="flex flex-col gap-0.5">
                        <span className="text-sm font-medium">{t(m.labelKey)}</span>
                        <span className="text-xs text-[--app-muted]">{t(m.descriptionKey)}</span>
                      </div>
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            </div>

            <div className="flex items-center gap-2">
              <ContextRing usedTokens={usedTokens} totalTokens={totalTokens} />
              <Button
                size="icon-lg"
                className="rounded-full bg-orange-500 text-white hover:bg-orange-600 disabled:cursor-not-allowed disabled:opacity-30"
                onClick={handleSubmit}
                disabled={!prompt.trim() || isSubmitting}
                aria-label="Send"
              >
                <ArrowUp size={16} />
              </Button>
            </div>
          </div>
        </div>

        <p className="mt-2 px-2 text-[11px] text-[--app-muted]">{DISCLAIMER_TEXT}</p>
      </div>
    </div>
  )
}
