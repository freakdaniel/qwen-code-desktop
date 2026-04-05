import { Bot } from 'lucide-react'
import { ChatTimestamp } from '@/components/chat/ChatTimestamp'

interface MessageBubbleProps {
  role: 'user' | 'assistant'
  content: string
  timestamp?: string
}

export function MessageBubble({ role, content, timestamp }: MessageBubbleProps) {
  if (role === 'user') {
    return (
      <div className="flex justify-end px-4 py-1.5">
        <div className="max-w-[42rem] rounded-[24px] rounded-tr-md border border-[--app-border] bg-[--app-elevated] px-4 py-3 shadow-[0_24px_60px_-48px_rgba(0,0,0,0.9)]">
          <p className="whitespace-pre-wrap break-words text-sm leading-relaxed text-[--app-text]">
            {content}
          </p>
          {timestamp && (
            <div className="mt-2 flex justify-end">
              <ChatTimestamp value={timestamp} align="end" />
            </div>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="flex items-start gap-3 px-4 py-1.5">
      <div className="mt-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-[--app-border] bg-[--app-elevated]">
        <Bot size={13} className="text-[--app-muted]" />
      </div>
      <div className="min-w-0 max-w-[42rem] flex-1 rounded-[24px] rounded-tl-md border border-[--app-border] bg-[--app-panel] px-4 py-3 shadow-[0_24px_60px_-48px_rgba(0,0,0,0.9)]">
        <p className="whitespace-pre-wrap break-words text-sm leading-relaxed text-[--app-text]">
          {content}
        </p>
        {timestamp && (
          <div className="mt-2 flex justify-start">
            <ChatTimestamp value={timestamp} />
          </div>
        )}
      </div>
    </div>
  )
}
