import { ChatHeader } from '@/components/chat/ChatHeader'
import { ChatInput } from '@/components/chat/ChatInput'
import { MessageList } from '@/components/chat/MessageList'
import { Separator } from '@/components/ui/separator'
import type { DesktopQuestionAnswer, DesktopSessionDetail, DesktopSessionEvent } from '@/types/desktop'
import type { AgentMode } from '@/types/ui'

interface ChatViewProps {
  detail: DesktopSessionDetail
  streamingText: string
  isActive: boolean
  isLoadingSession: boolean
  isSubmittingPrompt: boolean
  isRemovingSession: boolean
  approvingEntryId: string
  answeringEntryId: string
  latestSessionEvent: DesktopSessionEvent | null
  mode: AgentMode
  onModeChange: (mode: AgentMode) => void
  onCancel: () => void
  onRemoveSession: () => void
  onSubmit: (prompt: string, sessionId: string) => void
  onApprove: (entryId: string) => void
  onAnswer: (entryId: string, answers: DesktopQuestionAnswer[]) => void
  onLoadOlder: () => void
  onLoadNewer: () => void
}

function estimateTokenUsage(detail: DesktopSessionDetail, streamingText: string) {
  const transcript = detail.entries
    .map((entry) => [entry.title, entry.body, entry.arguments].filter(Boolean).join('\n'))
    .join('\n')
  return Math.max(1, Math.ceil((transcript.length + streamingText.length) / 4))
}

function getStatusText(detail: DesktopSessionDetail, latestSessionEvent: DesktopSessionEvent | null) {
  if (!latestSessionEvent || latestSessionEvent.sessionId !== detail.session.sessionId) {
    return ''
  }

  if (latestSessionEvent.message) {
    return latestSessionEvent.message
  }

  if (latestSessionEvent.toolName) {
    return `${latestSessionEvent.kind}: ${latestSessionEvent.toolName}`
  }

  return latestSessionEvent.kind
}

export function ChatView({
  detail,
  streamingText,
  isActive,
  isLoadingSession,
  isSubmittingPrompt,
  isRemovingSession,
  approvingEntryId,
  answeringEntryId,
  latestSessionEvent,
  mode,
  onModeChange,
  onCancel,
  onRemoveSession,
  onSubmit,
  onApprove,
  onAnswer,
  onLoadOlder,
  onLoadNewer,
}: ChatViewProps) {
  const usedTokens = estimateTokenUsage(detail, streamingText)
  const totalTokens = 128_000
  const statusText = getStatusText(detail, latestSessionEvent)

  return (
    <div className="flex h-full flex-col bg-[--app-bg]">
      <ChatHeader
        session={detail.session}
        isActive={isActive}
        isRemoving={isRemovingSession}
        onCancel={onCancel}
        onRemove={onRemoveSession}
      />

      <div className="mx-auto flex h-full w-full max-w-5xl min-h-0 flex-col">
        {statusText && (
          <>
            <div className="px-4 py-2.5 text-xs text-[--app-muted]">
              {statusText}
            </div>
            <Separator className="bg-[--app-border]" />
          </>
        )}

        <MessageList
          detail={detail}
          streamingText={streamingText}
          approvingEntryId={approvingEntryId}
          answeringEntryId={answeringEntryId}
          isLoadingSession={isLoadingSession}
          onApprove={onApprove}
          onAnswer={onAnswer}
          onLoadOlder={onLoadOlder}
          onLoadNewer={onLoadNewer}
        />

        <ChatInput
          sessionId={detail.session.sessionId}
          isSubmitting={isSubmittingPrompt}
          isActive={isActive}
          usedTokens={usedTokens}
          totalTokens={totalTokens}
          latestSessionEvent={latestSessionEvent}
          mode={mode}
          onModeChange={onModeChange}
          onSubmit={onSubmit}
        />
      </div>
    </div>
  )
}
