// Frontend/src/hooks/useSession.ts
import { useEffect, useState } from 'react'
import type {
  AppBootstrapPayload,
  DesktopQuestionAnswer,
  DesktopSessionDetail,
  DesktopSessionEvent,
  DesktopSessionTurnResult,
  SessionPreview,
} from '@/types/desktop'

const SESSION_PAGE_SIZE = 120

interface UseSessionOptions {
  bootstrap: AppBootstrapPayload
  latestSessionEvent: DesktopSessionEvent | null
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
  selectedSessionIdRef: React.MutableRefObject<string>
  selectedSessionDetailRef: React.MutableRefObject<DesktopSessionDetail | null>
}

export function useSession({
  bootstrap,
  latestSessionEvent,
  setBootstrap,
  selectedSessionIdRef,
  selectedSessionDetailRef,
}: UseSessionOptions) {
  const [selectedSessionId, setSelectedSessionId] = useState('')
  const [selectedSessionDetail, setSelectedSessionDetail] = useState<DesktopSessionDetail | null>(null)
  const [isLoadingSession, setIsLoadingSession] = useState(false)
  const [latestTurn, setLatestTurn] = useState<DesktopSessionTurnResult | null>(null)
  const [isSubmittingPrompt, setIsSubmittingPrompt] = useState(false)
  const [approvingEntryId, setApprovingEntryId] = useState('')
  const [answeringEntryId, setAnsweringEntryId] = useState('')
  const [recoveringSessionId, setRecoveringSessionId] = useState('')
  const [dismissingSessionId, setDismissingSessionId] = useState('')

  useEffect(() => {
    selectedSessionIdRef.current = selectedSessionId
  }, [selectedSessionId, selectedSessionIdRef])

  useEffect(() => {
    selectedSessionDetailRef.current = selectedSessionDetail
  }, [selectedSessionDetail, selectedSessionDetailRef])

  // Reload session when a session event arrives for the selected session
  useEffect(() => {
    if (!latestSessionEvent) return
    if (latestSessionEvent.sessionId !== selectedSessionIdRef.current) return

    const currentDetail = selectedSessionDetailRef.current
    const request = currentDetail?.hasNewerEntries
      ? { sessionId: latestSessionEvent.sessionId, offset: currentDetail.windowOffset, limit: currentDetail.windowSize || SESSION_PAGE_SIZE }
      : { sessionId: latestSessionEvent.sessionId, offset: null, limit: SESSION_PAGE_SIZE }

    void window.qwenDesktop?.getSession(request).then((detail) => {
      setSelectedSessionDetail(detail ?? null)
    })
  }, [latestSessionEvent, selectedSessionIdRef, selectedSessionDetailRef])

  // Load session when selection changes
  useEffect(() => {
    if (!selectedSessionId) {
      setSelectedSessionDetail(null)
      return
    }

    if (!window.qwenDesktop) {
      if (latestTurn?.session.sessionId === selectedSessionId) {
        setSelectedSessionDetail(buildPreviewDetail(latestTurn))
      }
      return
    }

    let cancelled = false
    const load = async () => {
      setIsLoadingSession(true)
      try {
        const detail = await window.qwenDesktop?.getSession({
          sessionId: selectedSessionId,
          offset: null,
          limit: SESSION_PAGE_SIZE,
        })
        if (!cancelled) setSelectedSessionDetail(detail ?? null)
      } finally {
        if (!cancelled) setIsLoadingSession(false)
      }
    }

    void load()
    return () => { cancelled = true }
  }, [latestTurn, selectedSessionId])

  const applyTurnResult = (result: DesktopSessionTurnResult) => {
    setLatestTurn(result)
    setSelectedSessionId(result.session.sessionId)
    setBootstrap((current) => ({
      ...current,
      recentSessions: [
        result.session,
        ...current.recentSessions.filter((s) => s.sessionId !== result.session.sessionId),
      ].slice(0, 24),
    }))
  }

  const handleSelectSession = (sessionId: string) => {
    setSelectedSessionId(sessionId)
    setSelectedSessionDetail(null)
  }

  const handleStartNewChat = () => {
    setSelectedSessionId('')
    setSelectedSessionDetail(null)
  }

  const handleSubmitNewTurn = async (prompt: string, sessionId: string) => {
    const trimmed = prompt.trim()
    if (!trimmed || isSubmittingPrompt) return
    setIsSubmittingPrompt(true)
    try {
      if (!window.qwenDesktop) {
        const preview: SessionPreview = {
          sessionId: sessionId || `preview-${Date.now()}`,
          title: trimmed.length > 120 ? `${trimmed.slice(0, 120)}...` : trimmed,
          lastActivity: 'just now',
          category: 'code',
          mode: 'code',
          status: 'resume-ready',
          workingDirectory: bootstrap.workspaceRoot,
          gitBranch: 'main',
          messageCount: 2,
          transcriptPath: `${bootstrap.workspaceRoot}/.qwen/chats/preview.jsonl`,
        }
        applyTurnResult({
          session: preview,
          assistantSummary: 'Preview turn (no bridge).',
          createdNewSession: !sessionId,
          resolvedCommand: null,
          toolExecution: {
            toolName: '', status: 'not-requested', approvalState: 'allow',
            workingDirectory: bootstrap.workspaceRoot, output: '', errorMessage: '',
            exitCode: 0, changedFiles: [], questions: [], answers: [],
          },
        })
        return
      }

      const result = await window.qwenDesktop.startSessionTurn({
        sessionId,
        prompt: trimmed,
        workingDirectory: selectedSessionDetail?.session.workingDirectory ?? bootstrap.workspaceRoot,
        toolName: '',
        toolArgumentsJson: '{}',
        approveToolExecution: false,
      })
      applyTurnResult(result)
    } finally {
      setIsSubmittingPrompt(false)
    }
  }

  const handleCancelTurn = async () => {
    if (!selectedSessionId || !window.qwenDesktop) return
    await window.qwenDesktop.cancelSessionTurn({ sessionId: selectedSessionId })
  }

  const handleApprovePendingTool = async (entryId: string) => {
    if (!selectedSessionId || !window.qwenDesktop || approvingEntryId) return
    setApprovingEntryId(entryId)
    try {
      const result = await window.qwenDesktop.approvePendingTool({ sessionId: selectedSessionId, entryId })
      applyTurnResult(result)
    } finally {
      setApprovingEntryId('')
    }
  }

  const handleAnswerPendingQuestion = async (entryId: string, answers: DesktopQuestionAnswer[]) => {
    if (!selectedSessionId || !window.qwenDesktop || answeringEntryId) return
    setAnsweringEntryId(entryId)
    try {
      const result = await window.qwenDesktop.answerPendingQuestion({ sessionId: selectedSessionId, entryId, answers })
      applyTurnResult(result)
    } finally {
      setAnsweringEntryId('')
    }
  }

  const handleResumeInterruptedTurn = async (sessionId: string) => {
    if (!window.qwenDesktop || recoveringSessionId) return
    setRecoveringSessionId(sessionId)
    try {
      const result = await window.qwenDesktop.resumeInterruptedTurn({ sessionId, recoveryNote: '' })
      setBootstrap((c) => ({
        ...c,
        recoverableTurns: c.recoverableTurns.filter((t) => t.sessionId !== sessionId),
      }))
      applyTurnResult(result)
    } finally {
      setRecoveringSessionId('')
    }
  }

  const handleDismissInterruptedTurn = async (sessionId: string) => {
    if (!window.qwenDesktop || dismissingSessionId) return
    setDismissingSessionId(sessionId)
    try {
      await window.qwenDesktop.dismissInterruptedTurn({ sessionId })
      setBootstrap((c) => ({
        ...c,
        recoverableTurns: c.recoverableTurns.filter((t) => t.sessionId !== sessionId),
      }))
    } finally {
      setDismissingSessionId('')
    }
  }

  const handleLoadOlderEntries = async () => {
    if (!window.qwenDesktop || !selectedSessionDetail || isLoadingSession) return
    setIsLoadingSession(true)
    try {
      const offset = Math.max(0, selectedSessionDetail.windowOffset - selectedSessionDetail.windowSize)
      const detail = await window.qwenDesktop.getSession({
        sessionId: selectedSessionDetail.session.sessionId,
        offset,
        limit: selectedSessionDetail.windowSize || SESSION_PAGE_SIZE,
      })
      setSelectedSessionDetail(detail ?? null)
    } finally {
      setIsLoadingSession(false)
    }
  }

  const handleLoadNewerEntries = async () => {
    if (!window.qwenDesktop || !selectedSessionDetail || isLoadingSession) return
    setIsLoadingSession(true)
    try {
      const offset = Math.min(
        Math.max(0, selectedSessionDetail.entryCount - selectedSessionDetail.windowSize),
        selectedSessionDetail.windowOffset + selectedSessionDetail.windowSize,
      )
      const detail = await window.qwenDesktop.getSession({
        sessionId: selectedSessionDetail.session.sessionId,
        offset,
        limit: selectedSessionDetail.windowSize || SESSION_PAGE_SIZE,
      })
      setSelectedSessionDetail(detail ?? null)
    } finally {
      setIsLoadingSession(false)
    }
  }

  return {
    selectedSessionId,
    setSelectedSessionId,
    selectedSessionDetail,
    isLoadingSession,
    latestTurn,
    isSubmittingPrompt,
    approvingEntryId,
    answeringEntryId,
    handleSelectSession,
    handleStartNewChat,
    handleSubmitNewTurn,
    handleCancelTurn,
    handleApprovePendingTool,
    handleAnswerPendingQuestion,
    handleResumeInterruptedTurn,
    handleDismissInterruptedTurn,
    handleLoadOlderEntries,
    handleLoadNewerEntries,
  }
}

function buildPreviewDetail(turn: DesktopSessionTurnResult): DesktopSessionDetail {
  return {
    session: turn.session,
    transcriptPath: turn.session.transcriptPath,
    entryCount: 1,
    windowOffset: 0,
    windowSize: 120,
    hasOlderEntries: false,
    hasNewerEntries: false,
    summary: {
      userCount: 1, assistantCount: 1, commandCount: 0,
      toolCount: 0, pendingApprovalCount: 0, pendingQuestionCount: 0,
      completedToolCount: 0, failedToolCount: 0, lastTimestamp: turn.session.lastActivity,
    },
    entries: [{
      id: 'preview-entry',
      type: 'assistant',
      timestamp: turn.session.lastActivity,
      workingDirectory: turn.session.workingDirectory,
      gitBranch: turn.session.gitBranch,
      title: 'Assistant',
      body: turn.assistantSummary,
      status: 'completed',
      toolName: '',
      approvalState: 'allow',
      exitCode: null,
      arguments: '',
      scope: '',
      sourcePath: '',
      resolutionStatus: '',
      resolvedAt: '',
      changedFiles: [],
      questions: [],
      answers: [],
    }],
  }
}
