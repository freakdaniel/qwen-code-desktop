import { startTransition, useDeferredValue, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import './App.css'
import { Icon } from './AppIcons'
import { fallbackBootstrap } from './appData'
import qwenLogo from './assets/qwen-logo.svg'
import type {
  ActiveTurnState,
  AppBootstrapPayload,
  AuthStatusSnapshot,
  DesktopQuestionAnswer,
  McpSnapshot,
  DesktopSessionDetail,
  DesktopSessionEntry,
  DesktopSessionEvent,
  DesktopSessionTurnResult,
  SessionPreview,
} from './types/desktop'

const SESSION_PAGE_SIZE = 120
type WorkspaceSurface = 'sessions' | 'auth' | 'mcp'

const WITTY_LOADING_PHRASES = [
  'Мне повезёт!',
  'Доставляем крутизну...',
  'Разогреваем ИИ-хомячков...',
  'Генерируем остроумный ответ...',
  'Полируем алгоритмы...',
  'Завариваем свежие байты...',
  'Компилируем гениальность...',
  'Призываем облако мудрости...',
  'Секунду, идёт отладка реальности...',
  'Превращаем кофе в код...',
  'Переподключаем синапсы...',
  'Ищем лишнюю точку с запятой...',
  'Разогреваем серверы...',
  'Варп-прыжок активирован...',
  'Без паники...',
  'Следуем за белым кроликом...',
]

type UiCopy = {
  newChat: string
  searchChats: string
  allChats: string
  workspace: string
  environment: string
  connected: string
  localPreview: string
  activeTurns: string
  nativeTools: string
  focusLabel: string
  heroEyebrow: string
  heroTitle: string
  heroSubtitle: string
  heroNote: string
  composerPlaceholder: string
  continuePlaceholder: string
  send: string
  sending: string
  loadOlder: string
  loadNewer: string
  noSessions: string
  emptySession: string
  loading: string
  pendingApprovals: string
  pendingQuestions: string
  completedTools: string
  failedTools: string
  commands: string
  tools: string
  users: string
  assistant: string
  recoverableTurns: string
  resumeInterrupted: string
  dismissInterrupted: string
  recoveryReason: string
  liveTurn: string
  cancelTurn: string
  approveAndExecute: string
  answerQuestions: string
  answerPlaceholder: string
  showing: string
  of: string
  suggestionsTitle: string
  suggestions: string[]
  architectureTitle: string
  architectureSubtitle: string
  toolPolicyTitle: string
  toolPolicySubtitle: string
  projectMemoryTitle: string
  projectMemoryEmpty: string
  surfacesTitle: string
  sessionOverviewTitle: string
  contextTitle: string
  timelineTitle: string
  transcriptTitle: string
  detailsLabel: string
  sourceLabel: string
  changedFilesLabel: string
  branchLabel: string
  statusLabel: string
  latestEventLabel: string
  readinessLabel: string
  approvalsLabel: string
  allowedLabel: string
  askLabel: string
  noPendingApprovals: string
  noPendingQuestions: string
  modeCodeLabel: string
  projectSummaryLabel: string
  updatedNowLabel: string
  newConversation: string
  skillsLabel: string
  mcpLabel: string
  authLabel: string
  toolsNavLabel: string
  agentsLabel: string
  settingsLabel: string
  modePlan: string
  modeDefault: string
  modeAutoEdit: string
  modeYolo: string
  attachFileLabel: string
  mcpTitle: string
  mcpSubtitle: string
  mcpConnected: string
  mcpDisconnected: string
  mcpMissing: string
  mcpTokens: string
  mcpEmpty: string
  mcpAddServer: string
  mcpReconnect: string
  mcpRemove: string
  mcpName: string
  mcpScope: string
  mcpTransport: string
  mcpCommandOrUrl: string
  mcpDescription: string
  mcpUserScope: string
  mcpProjectScope: string
  mcpSave: string
  authTitle: string
  authSubtitle: string
  authSelectedType: string
  authSelectedScope: string
  authStatus: string
  authModel: string
  authEndpoint: string
  authApiKeyEnv: string
  authCredentials: string
  authLastError: string
  authConnected: string
  authMissingCredentials: string
  authConfigureQwenOAuth: string
  authConfigureCodingPlan: string
  authConfigureOpenAi: string
  authDisconnect: string
  authAccessToken: string
  authRefreshToken: string
  authApiKey: string
  authBaseUrl: string
  authModelName: string
  authScope: string
  authRegion: string
  authScopeUser: string
  authScopeProject: string
  authRegionChina: string
  authRegionGlobal: string
  authSave: string
  authClearCredentials: string
  authDisplayName: string
  authCredentialPath: string
  authStartBrowserFlow: string
  authCancelFlow: string
  authFlowTitle: string
  authFlowStatus: string
  authFlowUserCode: string
  authFlowUrl: string
  authFlowExpires: string
  authFlowPending: string
  authFlowSucceeded: string
  authFlowCancelled: string
  authFlowTimedOut: string
  authFlowError: string
}

function App() {
  const { i18n } = useTranslation()
  const [bootstrap, setBootstrap] = useState<AppBootstrapPayload>(fallbackBootstrap)
  const [query, setQuery] = useState('')
  const [homePrompt, setHomePrompt] = useState('')
  const [sessionPrompt, setSessionPrompt] = useState('')
  const [isSubmittingPrompt, setIsSubmittingPrompt] = useState(false)
  const [latestTurn, setLatestTurn] = useState<DesktopSessionTurnResult | null>(null)
  const [latestSessionEvent, setLatestSessionEvent] = useState<DesktopSessionEvent | null>(null)
  const [reattachedSessionId, setReattachedSessionId] = useState('')
  const [streamingSnapshots, setStreamingSnapshots] = useState<Record<string, string>>({})
  const [activeTurnSessions, setActiveTurnSessions] = useState<Record<string, true>>({})
  const [selectedSessionId, setSelectedSessionId] = useState('')
  const [selectedSessionDetail, setSelectedSessionDetail] = useState<DesktopSessionDetail | null>(null)
  const [isLoadingSession, setIsLoadingSession] = useState(false)
  const [approvingEntryId, setApprovingEntryId] = useState('')
  const [answeringEntryId, setAnsweringEntryId] = useState('')
  const [recoveringSessionId, setRecoveringSessionId] = useState('')
  const [dismissingSessionId, setDismissingSessionId] = useState('')
  const [selectedMode, setSelectedMode] = useState<'plan' | 'default' | 'auto-edit' | 'yolo'>('default')
  const [workspaceSurface, setWorkspaceSurface] = useState<WorkspaceSurface>('sessions')
  const [authSnapshot, setAuthSnapshot] = useState<AuthStatusSnapshot>(fallbackBootstrap.qwenAuth)
  const [isSavingAuth, setIsSavingAuth] = useState(false)
  const [isStartingOAuthFlow, setIsStartingOAuthFlow] = useState(false)
  const [isCancellingOAuthFlow, setIsCancellingOAuthFlow] = useState(false)
  const [mcpSnapshot, setMcpSnapshot] = useState<McpSnapshot>(fallbackBootstrap.qwenMcp)
  const [isSavingMcp, setIsSavingMcp] = useState(false)
  const [reconnectingMcpName, setReconnectingMcpName] = useState('')
  const [removingMcpName, setRemovingMcpName] = useState('')
  const [wittyPhraseIndex, setWittyPhraseIndex] = useState(0)
  const didHydrateDesktopRef = useRef(false)
  const selectedSessionIdRef = useRef(selectedSessionId)
  const selectedSessionDetailRef = useRef<DesktopSessionDetail | null>(selectedSessionDetail)
  const deferredQuery = useDeferredValue(query)
  const copy = getUiCopy(bootstrap.currentLocale)

  const syncActiveTurns = (turns: ActiveTurnState[], preferredSessionId = '') => {
    setActiveTurnSessions(Object.fromEntries(turns.map((turn) => [turn.sessionId, true] as const)))
    setStreamingSnapshots(
      Object.fromEntries(
        turns
          .filter((turn) => turn.contentSnapshot)
          .map((turn) => [turn.sessionId, turn.contentSnapshot] as const),
      ),
    )

    if (turns.length === 0) {
      setReattachedSessionId('')
      return
    }

    const sortedTurns = [...turns].sort((left, right) =>
      Date.parse(right.lastUpdatedAtUtc) - Date.parse(left.lastUpdatedAtUtc))
    const targetSessionId = preferredSessionId
      || selectedSessionIdRef.current
      || sortedTurns[0]?.sessionId
      || ''

    if (!targetSessionId) {
      return
    }

    const activeTurn = sortedTurns.find((turn) => turn.sessionId === targetSessionId)
    if (!activeTurn) {
      return
    }

    setReattachedSessionId(activeTurn.sessionId)
    setSelectedSessionId((current) => current || activeTurn.sessionId)
    setLatestSessionEvent((current) => {
      if (
        current &&
        current.sessionId === activeTurn.sessionId &&
        current.kind !== 'turnCompleted' &&
        current.kind !== 'turnCancelled'
      ) {
        return current
      }

      return {
        sessionId: activeTurn.sessionId,
        kind: 'turnReattached',
        timestampUtc: activeTurn.lastUpdatedAtUtc,
        message: activeTurn.contentSnapshot || `Reattached to active turn at ${activeTurn.stage}.`,
        workingDirectory: activeTurn.workingDirectory,
        gitBranch: activeTurn.gitBranch,
        commandName: '',
        toolName: activeTurn.toolName,
        status: activeTurn.status,
        contentDelta: '',
        contentSnapshot: activeTurn.contentSnapshot,
      }
    })
  }

  useEffect(() => {
    selectedSessionIdRef.current = selectedSessionId
  }, [selectedSessionId])

  useEffect(() => {
    selectedSessionDetailRef.current = selectedSessionDetail
  }, [selectedSessionDetail])

  useEffect(() => {
    if (didHydrateDesktopRef.current) {
      return
    }

    didHydrateDesktopRef.current = true
    const disposers: Array<() => void> = []

    const hydrate = async () => {
      if (!window.qwenDesktop) {
        await i18n.changeLanguage(fallbackBootstrap.currentLocale)
        return
      }

      const payload = await window.qwenDesktop.bootstrap()
      setBootstrap(payload)
      setAuthSnapshot(payload.qwenAuth)
      setMcpSnapshot(payload.qwenMcp)
      syncActiveTurns(payload.activeTurns)
      await i18n.changeLanguage(payload.currentLocale)

      disposers.push(window.qwenDesktop.subscribeStateChanged((event) => {
        setBootstrap((current) => ({
          ...current,
          currentLocale: event.currentLocale,
        }))

        startTransition(() => {
          void i18n.changeLanguage(event.currentLocale)
        })
      }))

      disposers.push(window.qwenDesktop.subscribeAuthChanged((snapshot) => {
        updateAuthSnapshot(snapshot)
      }))

      disposers.push(window.qwenDesktop.subscribeSessionEvents((event) => {
        setLatestSessionEvent(event)
        setActiveTurnSessions((current) => {
          if (event.kind === 'turnStarted') {
            setReattachedSessionId('')
            return {
              ...current,
              [event.sessionId]: true,
            }
          }

          if (event.kind === 'turnCompleted' || event.kind === 'turnCancelled') {
            setReattachedSessionId((currentReattached) =>
              currentReattached === event.sessionId ? '' : currentReattached)
            if (!(event.sessionId in current)) {
              return current
            }

            const next = { ...current }
            delete next[event.sessionId]
            return next
          }

          return current
        })

        setStreamingSnapshots((current) => {
          if (event.kind === 'turnReattached' && event.contentSnapshot) {
            return {
              ...current,
              [event.sessionId]: event.contentSnapshot,
            }
          }

          if (event.kind === 'assistantStreaming' && event.contentSnapshot) {
            return {
              ...current,
              [event.sessionId]: event.contentSnapshot,
            }
          }

          if (
            event.kind === 'assistantCompleted'
            || event.kind === 'turnCompleted'
            || event.kind === 'turnCancelled'
          ) {
            if (!(event.sessionId in current)) {
              return current
            }

            const next = { ...current }
            delete next[event.sessionId]
            return next
          }

          return current
        })

        if (event.sessionId !== selectedSessionIdRef.current) {
          return
        }

        const currentDetail = selectedSessionDetailRef.current
        const request = currentDetail && currentDetail.hasNewerEntries
          ? {
              sessionId: event.sessionId,
              offset: currentDetail.windowOffset,
              limit: currentDetail.windowSize || SESSION_PAGE_SIZE,
            }
          : {
              sessionId: event.sessionId,
              offset: null,
              limit: SESSION_PAGE_SIZE,
            }

        void window.qwenDesktop?.getSession(request).then((detail) => {
          setSelectedSessionDetail(detail ?? null)
        })
      }))
    }

    void hydrate()
    return () => {
      disposers.forEach((dispose) => dispose())
    }
  }, [i18n])

  useEffect(() => {
    if (!window.qwenDesktop) {
      return
    }

    let disposed = false

    const resyncActiveTurns = async () => {
      const turns = await window.qwenDesktop?.getActiveTurns()
      if (!disposed && turns) {
        syncActiveTurns(turns)
      }
    }

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        void resyncActiveTurns()
      }
    }

    window.addEventListener('focus', resyncActiveTurns)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      disposed = true
      window.removeEventListener('focus', resyncActiveTurns)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [])

  useEffect(() => {
    document.documentElement.dir = i18n.language === 'ar' ? 'rtl' : 'ltr'
  }, [i18n.language])

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
        if (!cancelled) {
          setSelectedSessionDetail(detail ?? null)
        }
      } finally {
        if (!cancelled) {
          setIsLoadingSession(false)
        }
      }
    }

    void load()

    return () => {
      cancelled = true
    }
  }, [latestTurn, selectedSessionId])

  const visibleSessions = bootstrap.recentSessions.filter((session) => {
    const haystack = `${session.title} ${session.category} ${session.lastActivity} ${session.gitBranch}`.toLowerCase()
    return haystack.includes(deferredQuery.toLowerCase())
  })
  const groupedSessions = groupSessionsByProject(visibleSessions)

  const latestProjectName = getWorkspaceName(
    bootstrap.recentSessions[0]?.workingDirectory || bootstrap.workspaceRoot,
  )
  const selectedSessionPreview = selectedSessionDetail?.session
    ?? bootstrap.recentSessions.find((session) => session.sessionId === selectedSessionId)
    ?? null
  const selectedSessionStreamingText = selectedSessionId
    ? streamingSnapshots[selectedSessionId] ?? ''
    : ''
  const selectedSessionIsActive = Boolean(selectedSessionId && activeTurnSessions[selectedSessionId])
  const selectedSessionWasReattached = Boolean(selectedSessionId && reattachedSessionId === selectedSessionId)
  const heroVisible = homePrompt.length === 0 && !isSubmittingPrompt

  useEffect(() => {
    if (!isSubmittingPrompt) {
      return
    }

    const interval = setInterval(() => {
      setWittyPhraseIndex((i) => (i + 1) % WITTY_LOADING_PHRASES.length)
    }, 1800)

    return () => clearInterval(interval)
  }, [isSubmittingPrompt])

  const applyTurnResult = (result: DesktopSessionTurnResult) => {
    setLatestTurn(result)
    setSelectedSessionId(result.session.sessionId)
    setBootstrap((current) => {
      const sessions = [
        result.session,
        ...current.recentSessions.filter((session) => session.sessionId !== result.session.sessionId),
      ]

      return {
        ...current,
        recentSessions: sessions.slice(0, 24),
      }
    })
  }

  const handleStartNewChat = () => {
    setWorkspaceSurface('sessions')
    setSelectedSessionId('')
    setSelectedSessionDetail(null)
    setSessionPrompt('')
    setHomePrompt('')
  }

  const handleSelectSession = (sessionId: string) => {
    setWorkspaceSurface('sessions')
    setSelectedSessionId(sessionId)
    setSelectedSessionDetail(null)
  }

  const handleAddMcpServer = async (request: {
    name: string
    scope: 'user' | 'project'
    transport: 'stdio' | 'http' | 'sse'
    commandOrUrl: string
    description: string
  }) => {
    if (!window.qwenDesktop || isSavingMcp) {
      return
    }

    setIsSavingMcp(true)
    try {
      const snapshot = await window.qwenDesktop.addMcpServer({
        name: request.name,
        scope: request.scope,
        transport: request.transport,
        commandOrUrl: request.commandOrUrl,
        description: request.description,
        arguments: [],
        environmentVariables: {},
        headers: {},
        timeoutMs: null,
        trust: false,
        includeTools: [],
        excludeTools: [],
      })
      setMcpSnapshot(snapshot)
      setBootstrap((current) => ({ ...current, qwenMcp: snapshot }))
    } finally {
      setIsSavingMcp(false)
    }
  }

  const updateAuthSnapshot = (snapshot: AuthStatusSnapshot) => {
    setAuthSnapshot(snapshot)
    setBootstrap((current) => ({ ...current, qwenAuth: snapshot }))
  }

  const handleConfigureQwenOAuth = async (request: {
    scope: 'user' | 'project'
    accessToken: string
    refreshToken: string
  }) => {
    if (!window.qwenDesktop || isSavingAuth) {
      return
    }

    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.configureQwenOAuth({
        scope: request.scope,
        accessToken: request.accessToken,
        refreshToken: request.refreshToken,
        tokenType: 'Bearer',
        resourceUrl: '',
        idToken: '',
        expiresAtUtc: null,
      })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleConfigureCodingPlan = async (request: {
    scope: 'user' | 'project'
    region: 'china' | 'global'
    apiKey: string
    model: string
  }) => {
    if (!window.qwenDesktop || isSavingAuth) {
      return
    }

    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.configureCodingPlanAuth(request)
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleConfigureOpenAiCompatibleAuth = async (request: {
    scope: 'user' | 'project'
    authType: string
    model: string
    baseUrl: string
    apiKey: string
    apiKeyEnvironmentVariable: string
  }) => {
    if (!window.qwenDesktop || isSavingAuth) {
      return
    }

    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.configureOpenAiCompatibleAuth(request)
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleDisconnectAuth = async (scope: 'user' | 'project', clearPersistedCredentials: boolean) => {
    if (!window.qwenDesktop || isSavingAuth) {
      return
    }

    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.disconnectAuth({
        scope,
        clearPersistedCredentials,
      })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleStartQwenOAuthDeviceFlow = async (scope: 'user' | 'project') => {
    if (!window.qwenDesktop || isStartingOAuthFlow) {
      return
    }

    setIsStartingOAuthFlow(true)
    try {
      const snapshot = await window.qwenDesktop.startQwenOAuthDeviceFlow({ scope })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsStartingOAuthFlow(false)
    }
  }

  const handleCancelQwenOAuthDeviceFlow = async (flowId: string) => {
    if (!window.qwenDesktop || isCancellingOAuthFlow) {
      return
    }

    setIsCancellingOAuthFlow(true)
    try {
      const snapshot = await window.qwenDesktop.cancelQwenOAuthDeviceFlow({ flowId })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsCancellingOAuthFlow(false)
    }
  }

  const handleReconnectMcpServer = async (name: string) => {
    if (!window.qwenDesktop || reconnectingMcpName) {
      return
    }

    setReconnectingMcpName(name)
    try {
      const snapshot = await window.qwenDesktop.reconnectMcpServer({ name })
      setMcpSnapshot(snapshot)
      setBootstrap((current) => ({ ...current, qwenMcp: snapshot }))
    } finally {
      setReconnectingMcpName('')
    }
  }

  const handleRemoveMcpServer = async (name: string, scope: string) => {
    if (!window.qwenDesktop || removingMcpName) {
      return
    }

    setRemovingMcpName(name)
    try {
      const snapshot = await window.qwenDesktop.removeMcpServer({ name, scope })
      setMcpSnapshot(snapshot)
      setBootstrap((current) => ({ ...current, qwenMcp: snapshot }))
    } finally {
      setRemovingMcpName('')
    }
  }

  const handleSubmitNewTurn = async (prompt: string, sessionId: string) => {
    const trimmedPrompt = prompt.trim()
    if (!trimmedPrompt || isSubmittingPrompt) {
      return
    }

    setIsSubmittingPrompt(true)

    try {
      if (!window.qwenDesktop) {
        const previewSession: SessionPreview = {
          sessionId: sessionId || `preview-${Date.now()}`,
          title: trimmedPrompt.length > 120 ? `${trimmedPrompt.slice(0, 120)}...` : trimmedPrompt,
          lastActivity: copy.updatedNowLabel,
          category: copy.modeCodeLabel,
          mode: 'code',
          status: 'resume-ready',
          workingDirectory: bootstrap.workspaceRoot,
          gitBranch: 'main',
          messageCount: 2,
          transcriptPath: `${bootstrap.workspaceRoot}/.qwen/chats/preview.jsonl`,
        }

        applyTurnResult({
          session: previewSession,
          assistantSummary: 'Preview turn appended without the native desktop bridge.',
          createdNewSession: sessionId.length === 0,
          resolvedCommand: null,
          toolExecution: {
            toolName: '',
            status: 'not-requested',
            approvalState: 'allow',
            workingDirectory: bootstrap.workspaceRoot,
            output: '',
            errorMessage: '',
            exitCode: 0,
            changedFiles: [],
            questions: [],
            answers: [],
          },
        })
      } else {
        const result = await window.qwenDesktop.startSessionTurn({
          sessionId,
          prompt: trimmedPrompt,
          workingDirectory: selectedSessionPreview?.workingDirectory ?? bootstrap.workspaceRoot,
          toolName: '',
          toolArgumentsJson: '{}',
          approveToolExecution: false,
        })

        applyTurnResult(result)
      }

      if (sessionId) {
        setSessionPrompt('')
      } else {
        setHomePrompt('')
      }
    } finally {
      setIsSubmittingPrompt(false)
    }
  }

  const handleCancelTurn = async () => {
    if (!selectedSessionId || !window.qwenDesktop) {
      return
    }

    await window.qwenDesktop.cancelSessionTurn({
      sessionId: selectedSessionId,
    })
  }

  const handleApprovePendingTool = async (entryId: string) => {
    if (!selectedSessionId || !window.qwenDesktop || approvingEntryId) {
      return
    }

    setApprovingEntryId(entryId)

    try {
      const result = await window.qwenDesktop.approvePendingTool({
        sessionId: selectedSessionId,
        entryId,
      })

      applyTurnResult(result)
    } finally {
      setApprovingEntryId('')
    }
  }

  const handleAnswerPendingQuestion = async (entryId: string, answers: DesktopQuestionAnswer[]) => {
    if (!selectedSessionId || !window.qwenDesktop || answeringEntryId) {
      return
    }

    setAnsweringEntryId(entryId)

    try {
      const result = await window.qwenDesktop.answerPendingQuestion({
        sessionId: selectedSessionId,
        entryId,
        answers,
      })

      applyTurnResult(result)
    } finally {
      setAnsweringEntryId('')
    }
  }

  const handleResumeInterruptedTurn = async (sessionId: string) => {
    if (!window.qwenDesktop || recoveringSessionId) {
      return
    }

    setRecoveringSessionId(sessionId)

    try {
      const result = await window.qwenDesktop.resumeInterruptedTurn({
        sessionId,
        recoveryNote: '',
      })

      setBootstrap((current) => ({
        ...current,
        recoverableTurns: current.recoverableTurns.filter((turn) => turn.sessionId !== sessionId),
      }))
      applyTurnResult(result)
    } finally {
      setRecoveringSessionId('')
    }
  }

  const handleDismissInterruptedTurn = async (sessionId: string) => {
    if (!window.qwenDesktop || dismissingSessionId) {
      return
    }

    setDismissingSessionId(sessionId)

    try {
      await window.qwenDesktop.dismissInterruptedTurn({
        sessionId,
      })

      setBootstrap((current) => ({
        ...current,
        recoverableTurns: current.recoverableTurns.filter((turn) => turn.sessionId !== sessionId),
      }))
    } finally {
      setDismissingSessionId('')
    }
  }

  const handleLoadOlderEntries = async () => {
    if (!window.qwenDesktop || !selectedSessionDetail || isLoadingSession) {
      return
    }

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
    if (!window.qwenDesktop || !selectedSessionDetail || isLoadingSession) {
      return
    }

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

  return (
    <div className="app-shell">
      <aside className="sidebar-shell">
        <div className="sidebar-actions">
          <button className="sidebar-action-btn" onClick={handleStartNewChat} type="button">
            <Icon name="plus" />
            <span>{copy.newConversation}</span>
          </button>
          <button
            className={`sidebar-action-btn${workspaceSurface === 'auth' ? ' is-active' : ''}`}
            onClick={() => {
              setWorkspaceSurface('auth')
              setSelectedSessionId('')
            }}
            type="button"
          >
            <Icon name="settings" />
            <span>{copy.authLabel}</span>
          </button>
          <button
            className={`sidebar-action-btn${workspaceSurface === 'mcp' ? ' is-active' : ''}`}
            onClick={() => {
              setWorkspaceSurface('mcp')
              setSelectedSessionId('')
            }}
            type="button"
          >
            <Icon name="cpu" />
            <span>{copy.mcpLabel}</span>
          </button>
          <button
            className={`sidebar-action-btn${workspaceSurface === 'sessions' ? ' is-active' : ''}`}
            onClick={() => setWorkspaceSurface('sessions')}
            type="button"
          >
            <Icon name="code" />
            <span>{copy.toolsNavLabel}</span>
          </button>
          <button className="sidebar-action-btn" type="button">
            <Icon name="cpu" />
            <span>{copy.agentsLabel}</span>
          </button>
        </div>

        <label className="search-field">
          <Icon name="search" />
          <input
            onChange={(event) => setQuery(event.target.value)}
            placeholder={copy.searchChats}
            value={query}
          />
        </label>

        <div className="sidebar-section">
          <div className="session-list">
            {visibleSessions.length === 0 && (
              <div className="empty-note empty-note--sidebar">{copy.noSessions}</div>
            )}

            {groupedSessions.map((group) => (
              <section className="project-group" key={group.projectKey}>
                <div className="project-group__header">
                  <span className="project-group__title">
                    <Icon name="folder" />
                    {group.projectName}
                  </span>
                </div>

                <div className="project-group__sessions">
                  {group.sessions.map((session) => {
                    const isSelected = selectedSessionId === session.sessionId
                    const isActive = Boolean(activeTurnSessions[session.sessionId])

                    return (
                      <button
                        className={`session-list-item ${isSelected ? 'is-active' : ''}`}
                        key={session.sessionId}
                        onClick={() => handleSelectSession(session.sessionId)}
                        type="button"
                      >
                        <div className="session-list-item__title">
                          <strong>{session.title}</strong>
                          <span>{formatActivityLabel(session.lastActivity)}</span>
                        </div>

                        <div className="session-list-item__meta">
                          <span className="session-list-item__project">
                            {getWorkspaceName(session.workingDirectory)}
                          </span>
                          <span className="session-list-item__stats">
                            {session.messageCount > 0 && `+${session.messageCount}`}
                            {isActive && <span className="session-live-dot" />}
                          </span>
                        </div>
                      </button>
                    )
                  })}
                </div>
              </section>
            ))}
          </div>
        </div>

        <div className="sidebar-footer">
          <button className="sidebar-settings-btn" type="button">
            <Icon name="settings" />
            <span>{copy.settingsLabel}</span>
          </button>
        </div>
      </aside>

      <main className="workspace-shell">
        <div className="workspace-content">
          {bootstrap.recoverableTurns.length > 0 && (
            <RecoverableStrip
              copy={copy}
              dismissingSessionId={dismissingSessionId}
              onDismiss={handleDismissInterruptedTurn}
              onResume={handleResumeInterruptedTurn}
              recoveringSessionId={recoveringSessionId}
              turns={bootstrap.recoverableTurns}
            />
          )}

          {!selectedSessionId && workspaceSurface === 'sessions' && (
            <HomeWorkspace
              copy={copy}
              heroVisible={heroVisible}
              isSubmittingPrompt={isSubmittingPrompt}
              projectName={latestProjectName}
              wittyPhrase={WITTY_LOADING_PHRASES[wittyPhraseIndex]}
            />
          )}

          {!selectedSessionId && workspaceSurface === 'mcp' && (
            <McpWorkspace
              copy={copy}
              isSavingMcp={isSavingMcp}
              mcpSnapshot={mcpSnapshot}
              onAddServer={handleAddMcpServer}
              onReconnectServer={handleReconnectMcpServer}
              onRemoveServer={handleRemoveMcpServer}
              reconnectingMcpName={reconnectingMcpName}
              removingMcpName={removingMcpName}
            />
          )}

          {!selectedSessionId && workspaceSurface === 'auth' && (
            <AuthWorkspace
              authSnapshot={authSnapshot}
              copy={copy}
              isCancellingOAuthFlow={isCancellingOAuthFlow}
              isSavingAuth={isSavingAuth}
              isStartingOAuthFlow={isStartingOAuthFlow}
              onCancelQwenOAuthDeviceFlow={handleCancelQwenOAuthDeviceFlow}
              onConfigureCodingPlan={handleConfigureCodingPlan}
              onConfigureOpenAiCompatible={handleConfigureOpenAiCompatibleAuth}
              onConfigureQwenOAuth={handleConfigureQwenOAuth}
              onDisconnect={handleDisconnectAuth}
              onStartQwenOAuthDeviceFlow={handleStartQwenOAuthDeviceFlow}
            />
          )}

          {selectedSessionId && workspaceSurface === 'sessions' && (
            <SessionWorkspace
              approvingEntryId={approvingEntryId}
              answeringEntryId={answeringEntryId}
              bootstrap={bootstrap}
              copy={copy}
              isLoadingSession={isLoadingSession}
              latestSessionEvent={latestSessionEvent}
              onApprovePendingTool={handleApprovePendingTool}
              onAnswerPendingQuestion={handleAnswerPendingQuestion}
              onCancelTurn={() => void handleCancelTurn()}
              onLoadNewerEntries={() => void handleLoadNewerEntries()}
              onLoadOlderEntries={() => void handleLoadOlderEntries()}
              selectedSessionDetail={selectedSessionDetail}
              selectedSessionIsActive={selectedSessionIsActive}
              selectedSessionStreamingText={selectedSessionStreamingText}
              selectedSessionWasReattached={selectedSessionWasReattached}
            />
          )}
        </div>

        {workspaceSurface === 'sessions' && (
        <div className="workspace-composer-dock">
          <Composer
            isBusy={isSubmittingPrompt}
            modeCopy={{
              plan: copy.modePlan,
              default: copy.modeDefault,
              'auto-edit': copy.modeAutoEdit,
              yolo: copy.modeYolo,
            }}
            onChange={selectedSessionId ? setSessionPrompt : setHomePrompt}
            onModeChange={setSelectedMode}
            onSubmit={() => void handleSubmitNewTurn(
              selectedSessionId ? sessionPrompt : homePrompt,
              selectedSessionId,
            )}
            placeholder={selectedSessionId ? copy.continuePlaceholder : copy.composerPlaceholder}
            selectedMode={selectedMode}
            submitLabel={copy.send}
            submittingLabel={copy.sending}
            value={selectedSessionId ? sessionPrompt : homePrompt}
          />
        </div>
        )}
      </main>
    </div>
  )
}

function RecoverableStrip({
  copy,
  dismissingSessionId,
  onDismiss,
  onResume,
  recoveringSessionId,
  turns,
}: {
  copy: UiCopy
  dismissingSessionId: string
  onDismiss: (sessionId: string) => void
  onResume: (sessionId: string) => void
  recoveringSessionId: string
  turns: AppBootstrapPayload['recoverableTurns']
}) {
  return (
    <section className="recoverable-strip">
      <div className="section-heading">
        <div>
          <span className="section-heading__eyebrow">{copy.recoverableTurns}</span>
          <h2>{turns.length}</h2>
        </div>
        <p>{copy.recoveryReason}</p>
      </div>

      <div className="recoverable-strip__list">
        {turns.slice(0, 3).map((turn) => (
          <article className="recoverable-card" key={turn.sessionId}>
            <div className="recoverable-card__copy">
              <strong>{turn.prompt}</strong>
              <p>{turn.recoveryReason}</p>
              <span>{turn.gitBranch || 'main'} · {getWorkspaceName(turn.workingDirectory)}</span>
            </div>

            <div className="recoverable-card__actions">
              <button
                className="button button--ghost"
                disabled={recoveringSessionId === turn.sessionId}
                onClick={() => onResume(turn.sessionId)}
                type="button"
              >
                {recoveringSessionId === turn.sessionId ? copy.sending : copy.resumeInterrupted}
              </button>
              <button
                className="button button--ghost"
                disabled={dismissingSessionId === turn.sessionId}
                onClick={() => onDismiss(turn.sessionId)}
                type="button"
              >
                {dismissingSessionId === turn.sessionId ? copy.sending : copy.dismissInterrupted}
              </button>
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}

function HomeWorkspace({
  copy,
  heroVisible,
  isSubmittingPrompt,
  projectName,
  wittyPhrase,
}: {
  copy: UiCopy
  heroVisible: boolean
  isSubmittingPrompt: boolean
  projectName: string
  wittyPhrase: string
}) {
  return (
    <section className="home-workspace">
      <div className={`home-hero${heroVisible ? '' : ' home-hero--hidden'}`}>
        <img alt="Qwen" className="home-hero__logo" src={qwenLogo} />

        <div className="home-hero__copy">
          <span className="home-hero__headline">{copy.heroTitle}</span>
          <button className="home-hero__project" type="button">
            <span>{projectName}</span>
            <span className="home-hero__chevron">›</span>
          </button>
        </div>
      </div>

      {isSubmittingPrompt && (
        <div className="home-witty-phrase">{wittyPhrase}</div>
      )}
    </section>
  )
}

function AuthWorkspace({
  authSnapshot,
  copy,
  isCancellingOAuthFlow,
  isSavingAuth,
  isStartingOAuthFlow,
  onCancelQwenOAuthDeviceFlow,
  onConfigureCodingPlan,
  onConfigureOpenAiCompatible,
  onConfigureQwenOAuth,
  onDisconnect,
  onStartQwenOAuthDeviceFlow,
}: {
  authSnapshot: AuthStatusSnapshot
  copy: UiCopy
  isCancellingOAuthFlow: boolean
  isSavingAuth: boolean
  isStartingOAuthFlow: boolean
  onCancelQwenOAuthDeviceFlow: (flowId: string) => void
  onConfigureCodingPlan: (request: {
    scope: 'user' | 'project'
    region: 'china' | 'global'
    apiKey: string
    model: string
  }) => void
  onConfigureOpenAiCompatible: (request: {
    scope: 'user' | 'project'
    authType: string
    model: string
    baseUrl: string
    apiKey: string
    apiKeyEnvironmentVariable: string
  }) => void
  onConfigureQwenOAuth: (request: {
    scope: 'user' | 'project'
    accessToken: string
    refreshToken: string
  }) => void
  onDisconnect: (scope: 'user' | 'project', clearPersistedCredentials: boolean) => void
  onStartQwenOAuthDeviceFlow: (scope: 'user' | 'project') => void
}) {
  const [oauthScope, setOauthScope] = useState<'user' | 'project'>('user')
  const [oauthAccessToken, setOauthAccessToken] = useState('')
  const [oauthRefreshToken, setOauthRefreshToken] = useState('')

  const [codingPlanScope, setCodingPlanScope] = useState<'user' | 'project'>('project')
  const [codingPlanRegion, setCodingPlanRegion] = useState<'china' | 'global'>('global')
  const [codingPlanApiKey, setCodingPlanApiKey] = useState('')
  const [codingPlanModel, setCodingPlanModel] = useState('qwen3-coder-plus')

  const [openAiScope, setOpenAiScope] = useState<'user' | 'project'>('project')
  const [openAiAuthType, setOpenAiAuthType] = useState('openai')
  const [openAiModel, setOpenAiModel] = useState(authSnapshot.model || 'qwen3-coder-plus')
  const [openAiBaseUrl, setOpenAiBaseUrl] = useState(authSnapshot.endpoint.replace(/\/chat\/completions$/i, ''))
  const [openAiApiKey, setOpenAiApiKey] = useState('')
  const [openAiApiKeyEnv, setOpenAiApiKeyEnv] = useState(authSnapshot.apiKeyEnvironmentVariable || 'OPENAI_API_KEY')
  const [disconnectScope, setDisconnectScope] = useState<'user' | 'project'>(
    authSnapshot.selectedScope === 'project' ? 'project' : 'user',
  )
  const [clearPersistedCredentials, setClearPersistedCredentials] = useState(true)
  const [deviceFlowScope, setDeviceFlowScope] = useState<'user' | 'project'>(
    authSnapshot.selectedScope === 'project' ? 'project' : 'user',
  )

  const deviceFlowStatusLabel = authSnapshot.deviceFlow
    ? ({
        pending: copy.authFlowPending,
        succeeded: copy.authFlowSucceeded,
        cancelled: copy.authFlowCancelled,
        timeout: copy.authFlowTimedOut,
        error: copy.authFlowError,
      }[authSnapshot.deviceFlow.status] ?? formatTokenLabel(authSnapshot.deviceFlow.status))
    : ''

  return (
    <section className="auth-workspace">
      <header className="session-hero">
        <div className="session-hero__copy">
          <span className="section-heading__eyebrow">{copy.authLabel}</span>
          <h2>{copy.authTitle}</h2>
          <p>{copy.authSubtitle}</p>
        </div>
      </header>

      <section className="surface-card">
        <div className="auth-summary-grid">
          <article className="auth-summary-card">
            <span>{copy.authDisplayName}</span>
            <strong>{authSnapshot.displayName}</strong>
          </article>
          <article className="auth-summary-card">
            <span>{copy.authSelectedType}</span>
            <strong>{formatTokenLabel(authSnapshot.selectedType)}</strong>
          </article>
          <article className="auth-summary-card">
            <span>{copy.authSelectedScope}</span>
            <strong>{formatTokenLabel(authSnapshot.selectedScope)}</strong>
          </article>
          <article className="auth-summary-card">
            <span>{copy.authStatus}</span>
            <strong>
              {authSnapshot.status === 'connected' ? copy.authConnected : copy.authMissingCredentials}
            </strong>
          </article>
          <article className="auth-summary-card">
            <span>{copy.authModel}</span>
            <strong>{authSnapshot.model || '—'}</strong>
          </article>
          <article className="auth-summary-card">
            <span>{copy.authApiKeyEnv}</span>
            <strong>{authSnapshot.apiKeyEnvironmentVariable || '—'}</strong>
          </article>
        </div>

        <div className="auth-detail-grid">
          <article className="detail-block">
            <span>{copy.authEndpoint}</span>
            <pre>{authSnapshot.endpoint || '—'}</pre>
          </article>
          <article className="detail-block">
            <span>{copy.authCredentialPath}</span>
            <pre>{authSnapshot.credentialPath || '—'}</pre>
          </article>
          <article className="detail-block">
            <span>{copy.authCredentials}</span>
            <pre>{authSnapshot.hasQwenOAuthCredentials ? 'Qwen OAuth credentials present' : 'No persisted OAuth credentials'}</pre>
          </article>
          <article className="detail-block">
            <span>{copy.authLastError}</span>
            <pre>{authSnapshot.lastError || '—'}</pre>
          </article>
        </div>
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.authLabel}</span>
              <h3>{copy.authFlowTitle}</h3>
            </div>
          </div>
        </div>

        <div className="auth-form-grid">
          <label className="auth-field">
            <span>{copy.authScope}</span>
            <select onChange={(event) => setDeviceFlowScope(event.target.value as 'user' | 'project')} value={deviceFlowScope}>
              <option value="user">{copy.authScopeUser}</option>
              <option value="project">{copy.authScopeProject}</option>
            </select>
          </label>
        </div>

        <div className="timeline-entry__actions">
          <button
            className="button button--primary"
            disabled={isStartingOAuthFlow}
            onClick={() => onStartQwenOAuthDeviceFlow(deviceFlowScope)}
            type="button"
          >
            {isStartingOAuthFlow ? copy.sending : copy.authStartBrowserFlow}
          </button>
          {authSnapshot.deviceFlow && authSnapshot.deviceFlow.status === 'pending' && (
            <button
              className="button button--ghost"
              disabled={isCancellingOAuthFlow}
              onClick={() => onCancelQwenOAuthDeviceFlow(authSnapshot.deviceFlow!.flowId)}
              type="button"
            >
              {isCancellingOAuthFlow ? copy.sending : copy.authCancelFlow}
            </button>
          )}
        </div>

        {authSnapshot.deviceFlow && (
          <div className="auth-device-flow">
            <div className="auth-device-flow__meta">
              <article className="detail-block">
                <span>{copy.authFlowStatus}</span>
                <pre>{deviceFlowStatusLabel}</pre>
              </article>
              <article className="detail-block">
                <span>{copy.authFlowUserCode}</span>
                <pre>{authSnapshot.deviceFlow.userCode}</pre>
              </article>
              <article className="detail-block">
                <span>{copy.authFlowUrl}</span>
                <pre>{authSnapshot.deviceFlow.verificationUriComplete}</pre>
              </article>
              <article className="detail-block">
                <span>{copy.authFlowExpires}</span>
                <pre>{authSnapshot.deviceFlow.expiresAtUtc}</pre>
              </article>
            </div>
            {authSnapshot.deviceFlow.errorMessage && (
              <div className="empty-note">{authSnapshot.deviceFlow.errorMessage}</div>
            )}
          </div>
        )}
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.authLabel}</span>
              <h3>{copy.authConfigureQwenOAuth}</h3>
            </div>
          </div>
        </div>

        <div className="auth-form-grid">
          <label className="auth-field">
            <span>{copy.authScope}</span>
            <select onChange={(event) => setOauthScope(event.target.value as 'user' | 'project')} value={oauthScope}>
              <option value="user">{copy.authScopeUser}</option>
              <option value="project">{copy.authScopeProject}</option>
            </select>
          </label>
          <label className="auth-field auth-field--wide">
            <span>{copy.authAccessToken}</span>
            <input onChange={(event) => setOauthAccessToken(event.target.value)} value={oauthAccessToken} />
          </label>
          <label className="auth-field auth-field--wide">
            <span>{copy.authRefreshToken}</span>
            <input onChange={(event) => setOauthRefreshToken(event.target.value)} value={oauthRefreshToken} />
          </label>
        </div>

        <div className="timeline-entry__actions">
          <button
            className="button button--primary"
            disabled={isSavingAuth || oauthAccessToken.trim().length === 0}
            onClick={() => onConfigureQwenOAuth({
              scope: oauthScope,
              accessToken: oauthAccessToken.trim(),
              refreshToken: oauthRefreshToken.trim(),
            })}
            type="button"
          >
            {isSavingAuth ? copy.sending : copy.authSave}
          </button>
        </div>
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.authLabel}</span>
              <h3>{copy.authConfigureCodingPlan}</h3>
            </div>
          </div>
        </div>

        <div className="auth-form-grid">
          <label className="auth-field">
            <span>{copy.authScope}</span>
            <select onChange={(event) => setCodingPlanScope(event.target.value as 'user' | 'project')} value={codingPlanScope}>
              <option value="user">{copy.authScopeUser}</option>
              <option value="project">{copy.authScopeProject}</option>
            </select>
          </label>
          <label className="auth-field">
            <span>{copy.authRegion}</span>
            <select onChange={(event) => setCodingPlanRegion(event.target.value as 'china' | 'global')} value={codingPlanRegion}>
              <option value="china">{copy.authRegionChina}</option>
              <option value="global">{copy.authRegionGlobal}</option>
            </select>
          </label>
          <label className="auth-field">
            <span>{copy.authModelName}</span>
            <input onChange={(event) => setCodingPlanModel(event.target.value)} value={codingPlanModel} />
          </label>
          <label className="auth-field auth-field--wide">
            <span>{copy.authApiKey}</span>
            <input onChange={(event) => setCodingPlanApiKey(event.target.value)} value={codingPlanApiKey} />
          </label>
        </div>

        <div className="timeline-entry__actions">
          <button
            className="button button--primary"
            disabled={isSavingAuth || codingPlanApiKey.trim().length === 0}
            onClick={() => onConfigureCodingPlan({
              scope: codingPlanScope,
              region: codingPlanRegion,
              apiKey: codingPlanApiKey.trim(),
              model: codingPlanModel.trim(),
            })}
            type="button"
          >
            {isSavingAuth ? copy.sending : copy.authSave}
          </button>
        </div>
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.authLabel}</span>
              <h3>{copy.authConfigureOpenAi}</h3>
            </div>
          </div>
        </div>

        <div className="auth-form-grid">
          <label className="auth-field">
            <span>{copy.authScope}</span>
            <select onChange={(event) => setOpenAiScope(event.target.value as 'user' | 'project')} value={openAiScope}>
              <option value="user">{copy.authScopeUser}</option>
              <option value="project">{copy.authScopeProject}</option>
            </select>
          </label>
          <label className="auth-field">
            <span>{copy.authSelectedType}</span>
            <input onChange={(event) => setOpenAiAuthType(event.target.value)} value={openAiAuthType} />
          </label>
          <label className="auth-field">
            <span>{copy.authModelName}</span>
            <input onChange={(event) => setOpenAiModel(event.target.value)} value={openAiModel} />
          </label>
          <label className="auth-field auth-field--wide">
            <span>{copy.authBaseUrl}</span>
            <input onChange={(event) => setOpenAiBaseUrl(event.target.value)} value={openAiBaseUrl} />
          </label>
          <label className="auth-field auth-field--wide">
            <span>{copy.authApiKey}</span>
            <input onChange={(event) => setOpenAiApiKey(event.target.value)} value={openAiApiKey} />
          </label>
          <label className="auth-field">
            <span>{copy.authApiKeyEnv}</span>
            <input onChange={(event) => setOpenAiApiKeyEnv(event.target.value)} value={openAiApiKeyEnv} />
          </label>
        </div>

        <div className="timeline-entry__actions">
          <button
            className="button button--primary"
            disabled={isSavingAuth || openAiApiKey.trim().length === 0 || openAiBaseUrl.trim().length === 0}
            onClick={() => onConfigureOpenAiCompatible({
              scope: openAiScope,
              authType: openAiAuthType.trim() || 'openai',
              model: openAiModel.trim(),
              baseUrl: openAiBaseUrl.trim(),
              apiKey: openAiApiKey.trim(),
              apiKeyEnvironmentVariable: openAiApiKeyEnv.trim(),
            })}
            type="button"
          >
            {isSavingAuth ? copy.sending : copy.authSave}
          </button>
        </div>
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.authLabel}</span>
              <h3>{copy.authDisconnect}</h3>
            </div>
          </div>
        </div>

        <div className="auth-form-grid">
          <label className="auth-field">
            <span>{copy.authScope}</span>
            <select onChange={(event) => setDisconnectScope(event.target.value as 'user' | 'project')} value={disconnectScope}>
              <option value="user">{copy.authScopeUser}</option>
              <option value="project">{copy.authScopeProject}</option>
            </select>
          </label>
          <label className="auth-field auth-field--toggle">
            <span>{copy.authClearCredentials}</span>
            <input
              checked={clearPersistedCredentials}
              onChange={(event) => setClearPersistedCredentials(event.target.checked)}
              type="checkbox"
            />
          </label>
        </div>

        <div className="timeline-entry__actions">
          <button
            className="button button--ghost"
            disabled={isSavingAuth}
            onClick={() => onDisconnect(disconnectScope, clearPersistedCredentials)}
            type="button"
          >
            {isSavingAuth ? copy.sending : copy.authDisconnect}
          </button>
        </div>
      </section>
    </section>
  )
}

function McpWorkspace({
  copy,
  isSavingMcp,
  mcpSnapshot,
  onAddServer,
  onReconnectServer,
  onRemoveServer,
  reconnectingMcpName,
  removingMcpName,
}: {
  copy: UiCopy
  isSavingMcp: boolean
  mcpSnapshot: McpSnapshot
  onAddServer: (request: {
    name: string
    scope: 'user' | 'project'
    transport: 'stdio' | 'http' | 'sse'
    commandOrUrl: string
    description: string
  }) => void
  onReconnectServer: (name: string) => void
  onRemoveServer: (name: string, scope: string) => void
  reconnectingMcpName: string
  removingMcpName: string
}) {
  const [name, setName] = useState('')
  const [scope, setScope] = useState<'user' | 'project'>('user')
  const [transport, setTransport] = useState<'stdio' | 'http' | 'sse'>('stdio')
  const [commandOrUrl, setCommandOrUrl] = useState('')
  const [description, setDescription] = useState('')

  const canSubmit = name.trim().length > 0 && commandOrUrl.trim().length > 0 && !isSavingMcp

  return (
    <section className="mcp-workspace">
      <header className="session-hero">
        <div className="session-hero__copy">
          <span className="section-heading__eyebrow">{copy.mcpLabel}</span>
          <h2>{copy.mcpTitle}</h2>
          <p>{copy.mcpSubtitle}</p>
        </div>
      </header>

      <section className="surface-card">
        <div className="mcp-summary-grid">
          <article className="mcp-summary-card">
            <span>{copy.mcpLabel}</span>
            <strong>{mcpSnapshot.totalCount}</strong>
          </article>
          <article className="mcp-summary-card">
            <span>{copy.mcpConnected}</span>
            <strong>{mcpSnapshot.connectedCount}</strong>
          </article>
          <article className="mcp-summary-card">
            <span>{copy.mcpDisconnected}</span>
            <strong>{mcpSnapshot.disconnectedCount}</strong>
          </article>
          <article className="mcp-summary-card">
            <span>{copy.mcpMissing}</span>
            <strong>{mcpSnapshot.missingCount}</strong>
          </article>
          <article className="mcp-summary-card">
            <span>{copy.mcpTokens}</span>
            <strong>{mcpSnapshot.tokenCount}</strong>
          </article>
        </div>
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.mcpAddServer}</span>
              <h3>{copy.mcpTitle}</h3>
            </div>
          </div>
        </div>

        <div className="mcp-form-grid">
          <label className="mcp-field">
            <span>{copy.mcpName}</span>
            <input onChange={(event) => setName(event.target.value)} value={name} />
          </label>
          <label className="mcp-field">
            <span>{copy.mcpScope}</span>
            <select onChange={(event) => setScope(event.target.value as 'user' | 'project')} value={scope}>
              <option value="user">{copy.mcpUserScope}</option>
              <option value="project">{copy.mcpProjectScope}</option>
            </select>
          </label>
          <label className="mcp-field">
            <span>{copy.mcpTransport}</span>
            <select onChange={(event) => setTransport(event.target.value as 'stdio' | 'http' | 'sse')} value={transport}>
              <option value="stdio">stdio</option>
              <option value="http">http</option>
              <option value="sse">sse</option>
            </select>
          </label>
          <label className="mcp-field mcp-field--wide">
            <span>{copy.mcpCommandOrUrl}</span>
            <input onChange={(event) => setCommandOrUrl(event.target.value)} value={commandOrUrl} />
          </label>
          <label className="mcp-field mcp-field--wide">
            <span>{copy.mcpDescription}</span>
            <input onChange={(event) => setDescription(event.target.value)} value={description} />
          </label>
        </div>

        <div className="timeline-entry__actions">
          <button
            className="button button--primary"
            disabled={!canSubmit}
            onClick={() => {
              onAddServer({
                name: name.trim(),
                scope,
                transport,
                commandOrUrl: commandOrUrl.trim(),
                description: description.trim(),
              })
              setName('')
              setCommandOrUrl('')
              setDescription('')
            }}
            type="button"
          >
            {isSavingMcp ? copy.sending : copy.mcpSave}
          </button>
        </div>
      </section>

      <section className="surface-card">
        <div className="surface-card__toolbar">
          <div className="section-heading">
            <div>
              <span className="section-heading__eyebrow">{copy.mcpLabel}</span>
              <h3>{mcpSnapshot.totalCount}</h3>
            </div>
          </div>
        </div>

        {mcpSnapshot.servers.length === 0 ? (
          <div className="empty-note">{copy.mcpEmpty}</div>
        ) : (
          <div className="mcp-server-list">
            {mcpSnapshot.servers.map((server) => (
              <article className="mcp-server-card" key={`${server.scope}:${server.name}`}>
                <div className="mcp-server-card__header">
                  <div className="timeline-entry__copy">
                    <div className="timeline-entry__eyebrow">
                      <span>{server.scope}</span>
                      <span>{server.transport}</span>
                    </div>
                    <strong>{server.name}</strong>
                  </div>

                  <div className="entry-tags">
                    <span>{formatTokenLabel(server.status)}</span>
                    {server.hasPersistedToken && <span>{copy.mcpTokens}</span>}
                  </div>
                </div>

                <div className="timeline-entry__body">
                  {server.commandOrUrl}
                  {server.description && `\n${server.description}`}
                  {server.lastError && `\n${server.lastError}`}
                </div>

                <div className="timeline-entry__actions">
                  <button
                    className="button button--ghost"
                    disabled={reconnectingMcpName === server.name}
                    onClick={() => onReconnectServer(server.name)}
                    type="button"
                  >
                    {reconnectingMcpName === server.name ? copy.sending : copy.mcpReconnect}
                  </button>
                  <button
                    className="button button--ghost"
                    disabled={removingMcpName === server.name}
                    onClick={() => onRemoveServer(server.name, server.scope)}
                    type="button"
                  >
                    {removingMcpName === server.name ? copy.sending : copy.mcpRemove}
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>
    </section>
  )
}

function SessionWorkspace({
  approvingEntryId,
  answeringEntryId,
  bootstrap,
  copy,
  isLoadingSession,
  latestSessionEvent,
  onApprovePendingTool,
  onAnswerPendingQuestion,
  onCancelTurn,
  onLoadNewerEntries,
  onLoadOlderEntries,
  selectedSessionDetail,
  selectedSessionIsActive,
  selectedSessionStreamingText,
  selectedSessionWasReattached,
}: {
  approvingEntryId: string
  answeringEntryId: string
  bootstrap: AppBootstrapPayload
  copy: UiCopy
  isLoadingSession: boolean
  latestSessionEvent: DesktopSessionEvent | null
  onApprovePendingTool: (entryId: string) => void
  onAnswerPendingQuestion: (entryId: string, answers: DesktopQuestionAnswer[]) => void
  onCancelTurn: () => void
  onLoadNewerEntries: () => void
  onLoadOlderEntries: () => void
  selectedSessionDetail: DesktopSessionDetail | null
  selectedSessionIsActive: boolean
  selectedSessionStreamingText: string
  selectedSessionWasReattached: boolean
}) {
  if (isLoadingSession) {
    return (
      <section className="session-workspace session-workspace--centered">
        <div className="empty-state">{copy.loading}</div>
      </section>
    )
  }

  if (!selectedSessionDetail) {
    return (
      <section className="session-workspace session-workspace--centered">
        <div className="empty-state">{copy.emptySession}</div>
      </section>
    )
  }

  const sessionLatestEvent = latestSessionEvent?.sessionId === selectedSessionDetail.session.sessionId
    ? latestSessionEvent
    : null
  const liveMessage = selectedSessionStreamingText
    || sessionLatestEvent?.contentSnapshot
    || sessionLatestEvent?.message
    || ''
  const shouldShowLiveTurn = selectedSessionIsActive
    || selectedSessionWasReattached
    || Boolean(sessionLatestEvent && liveMessage)
  const pendingApprovalEntries = selectedSessionDetail.entries.filter((entry) =>
    entry.type === 'tool' && entry.status === 'approval-required' && !entry.resolutionStatus)
  const pendingQuestionEntries = selectedSessionDetail.entries.filter((entry) =>
    entry.type === 'tool'
    && entry.toolName === 'ask_user_question'
    && entry.status === 'input-required'
    && !entry.resolutionStatus)
  const rangeStart = selectedSessionDetail.entries.length === 0
    ? 0
    : selectedSessionDetail.windowOffset + 1
  const rangeEnd = selectedSessionDetail.windowOffset + selectedSessionDetail.entries.length

  return (
    <section className="session-workspace">
      <div className="session-layout">
        <div className="session-primary">
          <header className="session-hero">
            <div className="session-hero__copy">
              <span className="section-heading__eyebrow">{copy.transcriptTitle}</span>
              <h2>{selectedSessionDetail.session.title}</h2>
              <p>{selectedSessionDetail.transcriptPath}</p>
            </div>

            <div className="inline-meta-list">
              <span>{copy.branchLabel}: {selectedSessionDetail.session.gitBranch || 'main'}</span>
              <span>{copy.statusLabel}: {formatTokenLabel(selectedSessionDetail.session.status)}</span>
              <span>{copy.modeCodeLabel}</span>
            </div>
          </header>

          {shouldShowLiveTurn && (
            <div className="live-banner">
              <div>
                <span className="section-heading__eyebrow">{copy.liveTurn}</span>
                <p>{liveMessage || 'Reattached to the current active turn.'}</p>
              </div>

              {selectedSessionIsActive && (
                <button className="button button--ghost" onClick={onCancelTurn} type="button">
                  {copy.cancelTurn}
                </button>
              )}
            </div>
          )}

          <section className="surface-card surface-card--transcript">
            <div className="surface-card__toolbar">
              <div className="section-heading">
                <div>
                  <span className="section-heading__eyebrow">{copy.timelineTitle}</span>
                  <h3>
                    {copy.showing} {rangeStart}-{rangeEnd} {copy.of} {selectedSessionDetail.entryCount}
                  </h3>
                </div>
              </div>

              <div className="toolbar-actions">
                {selectedSessionDetail.hasOlderEntries && (
                  <button className="button button--ghost" onClick={onLoadOlderEntries} type="button">
                    {copy.loadOlder}
                  </button>
                )}
                {selectedSessionDetail.hasNewerEntries && (
                  <button className="button button--ghost" onClick={onLoadNewerEntries} type="button">
                    {copy.loadNewer}
                  </button>
                )}
              </div>
            </div>

            <div className="timeline-list">
              {selectedSessionDetail.entries.length === 0 && (
                <div className="empty-note">{copy.emptySession}</div>
              )}

              {selectedSessionDetail.entries.map((entry) => (
                <SessionEntryCard
                  approvingEntryId={approvingEntryId}
                  answeringEntryId={answeringEntryId}
                  copy={copy}
                  entry={entry}
                  key={entry.id}
                  onApprovePendingTool={onApprovePendingTool}
                  onAnswerPendingQuestion={onAnswerPendingQuestion}
                />
              ))}
            </div>
          </section>

        </div>

        <aside className="session-rail">
          <section className="surface-card surface-card--rail">
            <div className="section-heading">
              <div>
                <span className="section-heading__eyebrow">{copy.sessionOverviewTitle}</span>
                <h3>{selectedSessionDetail.session.title}</h3>
              </div>
            </div>

            <div className="rail-stats">
              <article>
                <span>{copy.users}</span>
                <strong>{selectedSessionDetail.summary.userCount}</strong>
              </article>
              <article>
                <span>{copy.assistant}</span>
                <strong>{selectedSessionDetail.summary.assistantCount}</strong>
              </article>
              <article>
                <span>{copy.commands}</span>
                <strong>{selectedSessionDetail.summary.commandCount}</strong>
              </article>
              <article>
                <span>{copy.tools}</span>
                <strong>{selectedSessionDetail.summary.toolCount}</strong>
              </article>
              <article>
                <span>{copy.pendingApprovals}</span>
                <strong>{selectedSessionDetail.summary.pendingApprovalCount}</strong>
              </article>
              <article>
                <span>{copy.pendingQuestions}</span>
                <strong>{selectedSessionDetail.summary.pendingQuestionCount}</strong>
              </article>
              <article>
                <span>{copy.completedTools}</span>
                <strong>{selectedSessionDetail.summary.completedToolCount}</strong>
              </article>
              <article>
                <span>{copy.failedTools}</span>
                <strong>{selectedSessionDetail.summary.failedToolCount}</strong>
              </article>
              <article>
                <span>{copy.nativeTools}</span>
                <strong>{bootstrap.qwenNativeHost.readyCount}</strong>
              </article>
            </div>
          </section>

          <section className="surface-card surface-card--rail">
            <div className="section-heading">
              <div>
                <span className="section-heading__eyebrow">{copy.contextTitle}</span>
                <h3>{copy.latestEventLabel}</h3>
              </div>
            </div>

            <div className="context-block">
              <article>
                <span>{copy.workspace}</span>
                <strong>{getWorkspaceName(selectedSessionDetail.session.workingDirectory)}</strong>
              </article>
              <article>
                <span>{copy.branchLabel}</span>
                <strong>{selectedSessionDetail.session.gitBranch || 'main'}</strong>
              </article>
              <article>
                <span>{copy.statusLabel}</span>
                <strong>{formatTokenLabel(sessionLatestEvent?.status || selectedSessionDetail.session.status)}</strong>
              </article>
              {sessionLatestEvent && (
                <article>
                  <span>{copy.latestEventLabel}</span>
                  <p>{sessionLatestEvent.message || formatTokenLabel(sessionLatestEvent.kind)}</p>
                </article>
              )}
            </div>
          </section>

          <section className="surface-card surface-card--rail">
            <div className="section-heading">
              <div>
                <span className="section-heading__eyebrow">{copy.approvalsLabel}</span>
                <h3>{pendingApprovalEntries.length}</h3>
              </div>
            </div>

            {pendingApprovalEntries.length === 0 ? (
              <div className="empty-note">{copy.noPendingApprovals}</div>
            ) : (
              <div className="approval-list">
                {pendingApprovalEntries.map((entry) => (
                  <article className="approval-list__item" key={entry.id}>
                    <strong>{entry.toolName || entry.title}</strong>
                    <p>{entry.body || entry.arguments || entry.sourcePath}</p>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className="surface-card surface-card--rail">
            <div className="section-heading">
              <div>
                <span className="section-heading__eyebrow">{copy.pendingQuestions}</span>
                <h3>{pendingQuestionEntries.length}</h3>
              </div>
            </div>

            {pendingQuestionEntries.length === 0 ? (
              <div className="empty-note">{copy.noPendingQuestions}</div>
            ) : (
              <div className="approval-list">
                {pendingQuestionEntries.map((entry) => (
                  <article className="approval-list__item" key={entry.id}>
                    <strong>{entry.title}</strong>
                    <p>{entry.body || entry.arguments}</p>
                  </article>
                ))}
              </div>
            )}
          </section>
        </aside>
      </div>
    </section>
  )
}

function Composer({
  isBusy,
  modeCopy,
  onChange,
  onModeChange,
  onSubmit,
  placeholder,
  selectedMode,
  submitLabel,
  submittingLabel,
  value,
}: {
  isBusy: boolean
  modeCopy: Record<'plan' | 'default' | 'auto-edit' | 'yolo', string>
  onChange: (value: string) => void
  onModeChange: (mode: 'plan' | 'default' | 'auto-edit' | 'yolo') => void
  onSubmit: () => void
  placeholder: string
  selectedMode: 'plan' | 'default' | 'auto-edit' | 'yolo'
  submitLabel: string
  submittingLabel: string
  value: string
}) {
  return (
    <div className="composer-bar">
      <button
        className="composer-attachment-btn"
        disabled={isBusy}
        title="Прикрепить файл"
        type="button"
      >
        <Icon name="paperclip" />
      </button>

      <div className="composer-mode-selector">
        {(['plan', 'default', 'auto-edit', 'yolo'] as const).map((mode) => (
          <button
            className={`composer-mode-pill${selectedMode === mode ? ' is-active' : ''}`}
            key={mode}
            onClick={() => onModeChange(mode)}
            type="button"
          >
            {modeCopy[mode]}
          </button>
        ))}
      </div>

      <textarea
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault()
            onSubmit()
          }
        }}
        placeholder={placeholder}
        rows={1}
        value={value}
      />

      <div className="composer-actions">
        <button
          className="composer-action composer-action--primary"
          disabled={isBusy || !value.trim()}
          onClick={onSubmit}
          type="button"
        >
          <Icon name="forward" />
          <span>{isBusy ? submittingLabel : submitLabel}</span>
        </button>
      </div>
    </div>
  )
}

function SessionEntryCard({
  approvingEntryId,
  answeringEntryId,
  copy,
  entry,
  onApprovePendingTool,
  onAnswerPendingQuestion,
}: {
  approvingEntryId: string
  answeringEntryId: string
  copy: UiCopy
  entry: DesktopSessionEntry
  onApprovePendingTool: (entryId: string) => void
  onAnswerPendingQuestion: (entryId: string, answers: DesktopQuestionAnswer[]) => void
}) {
  const canApprove = entry.type === 'tool' && entry.status === 'approval-required' && !entry.resolutionStatus
  const canAnswer = entry.type === 'tool'
    && entry.toolName === 'ask_user_question'
    && entry.status === 'input-required'
    && !entry.resolutionStatus
  const isApproving = approvingEntryId === entry.id
  const isAnswering = answeringEntryId === entry.id
  const toneClass = getEntryToneClass(entry.type)

  return (
    <article className={`timeline-entry ${toneClass}`}>
      <div className="timeline-entry__header">
        <div className="timeline-entry__copy">
          <div className="timeline-entry__eyebrow">
            <span>{formatTokenLabel(entry.type || entry.title)}</span>
            {(entry.timestamp || entry.workingDirectory || entry.gitBranch) && (
              <span>{entry.timestamp || entry.workingDirectory || entry.gitBranch}</span>
            )}
          </div>
          <strong>{entry.title}</strong>
        </div>

        <div className="entry-tags">
          {entry.status && <span>{formatTokenLabel(entry.status)}</span>}
          {entry.toolName && <span>{entry.toolName}</span>}
          {entry.approvalState && <span>{formatTokenLabel(entry.approvalState)}</span>}
          {entry.exitCode !== null && entry.exitCode !== undefined && <span>exit {entry.exitCode}</span>}
        </div>
      </div>

      <div className="timeline-entry__body">{entry.body || 'No content.'}</div>

      {(entry.arguments || entry.sourcePath || entry.changedFiles.length > 0) && (
        <div className="entry-detail-grid">
          {entry.arguments && (
            <DetailBlock label={copy.detailsLabel} value={entry.arguments} />
          )}
          {entry.sourcePath && (
            <DetailBlock label={copy.sourceLabel} value={entry.sourcePath} />
          )}
          {entry.changedFiles.length > 0 && (
            <DetailBlock label={copy.changedFilesLabel} value={entry.changedFiles.join('\n')} />
          )}
        </div>
      )}

      {canApprove && (
        <div className="timeline-entry__actions">
          <button
            className="button button--primary"
            disabled={isApproving}
            onClick={() => onApprovePendingTool(entry.id)}
            type="button"
          >
            {isApproving ? copy.sending : copy.approveAndExecute}
          </button>
        </div>
      )}

      {canAnswer && (
        <PendingQuestionForm
          copy={copy}
          entry={entry}
          isSubmitting={isAnswering}
          onSubmit={(answers) => onAnswerPendingQuestion(entry.id, answers)}
        />
      )}
    </article>
  )
}

function PendingQuestionForm({
  copy,
  entry,
  isSubmitting,
  onSubmit,
}: {
  copy: UiCopy
  entry: DesktopSessionEntry
  isSubmitting: boolean
  onSubmit: (answers: DesktopQuestionAnswer[]) => void
}) {
  const [answersByIndex, setAnswersByIndex] = useState<Record<number, string>>(() =>
    Object.fromEntries(entry.answers.map((answer) => [answer.questionIndex, answer.value] as const)),
  )

  const setAnswer = (questionIndex: number, value: string) => {
    setAnswersByIndex((current) => ({
      ...current,
      [questionIndex]: value,
    }))
  }

  const toggleOption = (questionIndex: number, label: string, multiSelect: boolean) => {
    setAnswersByIndex((current) => {
      const currentValue = current[questionIndex] ?? ''
      if (!multiSelect) {
        return {
          ...current,
          [questionIndex]: label,
        }
      }

      const items = splitAnswerValues(currentValue)
      const nextItems = items.includes(label)
        ? items.filter((item) => item !== label)
        : [...items, label]

      return {
        ...current,
        [questionIndex]: nextItems.join(', '),
      }
    })
  }

  const preparedAnswers = entry.questions.map((question, questionIndex) => ({
    question,
    questionIndex,
    value: (answersByIndex[questionIndex] ?? '').trim(),
  }))
  const isReadyToSubmit = preparedAnswers.length > 0
    && preparedAnswers.every((item) => item.value.length > 0)

  return (
    <div className="question-form">
      {entry.questions.map((question, questionIndex) => {
        const currentValue = answersByIndex[questionIndex] ?? ''
        const selectedValues = splitAnswerValues(currentValue)

        return (
          <section className="question-form__item" key={`${entry.id}-${questionIndex}`}>
            <div className="question-form__header">
              <strong>{question.header || `Question ${questionIndex + 1}`}</strong>
              <p>{question.question}</p>
            </div>

            <div className="question-form__options">
              {question.options.map((option) => {
                const isSelected = question.multiSelect
                  ? selectedValues.includes(option.label)
                  : currentValue.trim() === option.label

                return (
                  <button
                    className={`question-option${isSelected ? ' is-selected' : ''}`}
                    key={`${entry.id}-${questionIndex}-${option.label}`}
                    onClick={() => toggleOption(questionIndex, option.label, question.multiSelect)}
                    type="button"
                  >
                    <strong>{option.label}</strong>
                    <span>{option.description}</span>
                  </button>
                )
              })}
            </div>

            <input
              className="question-form__input"
              onChange={(event) => setAnswer(questionIndex, event.target.value)}
              placeholder={copy.answerPlaceholder}
              value={currentValue}
            />
          </section>
        )
      })}

      <div className="timeline-entry__actions">
        <button
          className="button button--primary"
          disabled={isSubmitting || !isReadyToSubmit}
          onClick={() => onSubmit(
            preparedAnswers.map((item) => ({
              questionIndex: item.questionIndex,
              value: item.value,
            })),
          )}
          type="button"
        >
          {isSubmitting ? copy.sending : copy.answerQuestions}
        </button>
      </div>
    </div>
  )
}

function DetailBlock({ label, value }: { label: string; value: string }) {
  return (
    <article className="detail-block">
      <span>{label}</span>
      <pre>{value}</pre>
    </article>
  )
}

function buildPreviewDetail(result: DesktopSessionTurnResult): DesktopSessionDetail {
  return {
    session: result.session,
    transcriptPath: result.session.transcriptPath,
    entryCount: 2,
    windowOffset: 0,
    windowSize: 2,
    hasOlderEntries: false,
    hasNewerEntries: false,
    summary: {
      userCount: 1,
      assistantCount: 1,
      commandCount: result.resolvedCommand ? 1 : 0,
      toolCount: 0,
      pendingApprovalCount: 0,
      pendingQuestionCount: 0,
      completedToolCount: 0,
      failedToolCount: 0,
      lastTimestamp: '',
    },
    entries: [
      {
        id: `${result.session.sessionId}-user`,
        type: 'user',
        timestamp: '',
        workingDirectory: result.session.workingDirectory,
        gitBranch: result.session.gitBranch,
        title: 'User',
        body: result.session.title,
        status: '',
        approvalState: '',
        exitCode: null,
        arguments: '',
        scope: '',
        sourcePath: '',
        resolutionStatus: '',
        resolvedAt: '',
        changedFiles: [],
        toolName: '',
        questions: [],
        answers: [],
      },
      {
        id: `${result.session.sessionId}-assistant`,
        type: 'assistant',
        timestamp: '',
        workingDirectory: result.session.workingDirectory,
        gitBranch: result.session.gitBranch,
        title: 'Assistant',
        body: result.assistantSummary,
        status: '',
        approvalState: '',
        exitCode: null,
        arguments: '',
        scope: '',
        sourcePath: '',
        resolutionStatus: '',
        resolvedAt: '',
        changedFiles: [],
        toolName: '',
        questions: [],
        answers: [],
      },
    ],
  }
}

function splitAnswerValues(value: string) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

function getWorkspaceName(path: string) {
  const normalized = path.replace(/\\/g, '/').split('/').filter(Boolean)
  return normalized.at(-1) ?? path
}

function getEntryToneClass(type: string) {
  switch (type) {
    case 'user':
      return 'timeline-entry--user'
    case 'assistant':
      return 'timeline-entry--assistant'
    case 'tool':
      return 'timeline-entry--tool'
    case 'command':
      return 'timeline-entry--command'
    default:
      return 'timeline-entry--neutral'
  }
}

function groupSessionsByProject(sessions: SessionPreview[]) {
  const groups = new Map<string, { projectKey: string; projectName: string; sessions: SessionPreview[] }>()

  sessions.forEach((session) => {
    const projectKey = session.workingDirectory || 'workspace'
    const projectName = getWorkspaceName(projectKey)
    const current = groups.get(projectKey)

    if (current) {
      current.sessions.push(session)
      return
    }

    groups.set(projectKey, {
      projectKey,
      projectName,
      sessions: [session],
    })
  })

  return Array.from(groups.values())
}

function formatActivityLabel(value: string) {
  if (!value) {
    return ''
  }

  return value.replace(/^updated\s+/i, '')
}

function formatTokenLabel(value: string) {
  if (!value) {
    return ''
  }

  return value
    .replace(/[-_]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^\w/, (char) => char.toUpperCase())
}

function getUiCopy(locale: string): UiCopy {
  if (locale.startsWith('ru')) {
    return {
      newChat: 'Новая сессия',
      searchChats: 'Поиск по сессиям',
      allChats: 'Беседы',
      workspace: 'Workspace',
      environment: 'Среда',
      connected: 'Bridge подключён',
      localPreview: 'Локальный preview',
      activeTurns: 'активных turn',
      nativeTools: 'Нативные инструменты',
      focusLabel: 'Рабочая поверхность',
      heroEyebrow: 'Qwen-first desktop agent',
      heroTitle: 'Давайте построим',
      heroSubtitle: '',
      heroNote: '',
      composerPlaceholder: 'Что нужно сделать с кодом, архитектурой или runtime прямо сейчас?',
      continuePlaceholder: 'Продолжить эту сессию...',
      send: 'Отправить',
      sending: 'Отправка...',
      loadOlder: 'Старее',
      loadNewer: 'Новее',
      noSessions: 'Сессии пока не найдены.',
      emptySession: 'Выберите сессию слева, чтобы открыть transcript.',
      loading: 'Загружаю transcript...',
      pendingApprovals: 'Ожидают approval',
      pendingQuestions: 'Ожидают ответа',
      completedTools: 'Инструменты завершены',
      failedTools: 'Ошибки',
      commands: 'Команды',
      tools: 'Инструменты',
      users: 'Пользователь',
      assistant: 'Ассистент',
      recoverableTurns: 'Незавершённые turn',
      resumeInterrupted: 'Возобновить',
      dismissInterrupted: 'Скрыть',
      recoveryReason: 'Сессии, которые можно безопасно вернуть в работу.',
      liveTurn: 'Живой turn',
      cancelTurn: 'Остановить',
      approveAndExecute: 'Подтвердить и выполнить',
      answerQuestions: 'Отправить ответы',
      answerPlaceholder: 'Выберите вариант или напишите свой ответ',
      showing: 'Показано',
      of: 'из',
      suggestionsTitle: 'Быстрые сценарии',
      suggestions: [
        'Переработай renderer под Qwen-first desktop shell',
        'Собери typed IPC flow для новых UI-панелей',
        'Разберись с approval UX и live transcript',
        'Подготовь план миграции от текущего колхоза к системному интерфейсу',
      ],
      architectureTitle: 'Архитектурные треки',
      architectureSubtitle: 'Главные направления, которые сейчас формируют продукт.',
      toolPolicyTitle: 'Политика инструментов',
      toolPolicySubtitle: 'Как runtime, approvals и native host выглядят прямо сейчас.',
      projectMemoryTitle: 'Память проекта',
      projectMemoryEmpty: 'История проекта ещё не собрана. Здесь появится summary и текущий план.',
      surfacesTitle: 'Поверхности совместимости',
      sessionOverviewTitle: 'Обзор сессии',
      contextTitle: 'Контекст',
      timelineTitle: 'Timeline',
      transcriptTitle: 'Transcript',
      detailsLabel: 'Аргументы',
      sourceLabel: 'Исходный путь',
      changedFilesLabel: 'Изменённые файлы',
      branchLabel: 'Ветка',
      statusLabel: 'Статус',
      latestEventLabel: 'Последнее событие',
      readinessLabel: 'Готово',
      approvalsLabel: 'Approval',
      allowedLabel: 'Allow',
      askLabel: 'Ask',
      noPendingApprovals: 'Сейчас нет инструментов, ожидающих подтверждения.',
      noPendingQuestions: 'Сейчас нет вопросов, ожидающих ответа пользователя.',
      modeCodeLabel: 'Code',
      projectSummaryLabel: 'Текущая цель проекта',
      updatedNowLabel: 'Обновлено только что',
      newConversation: 'Новая беседа',
      skillsLabel: 'Навыки',
      authLabel: 'Auth',
      mcpLabel: 'MCP',
      toolsNavLabel: 'Инструменты',
      agentsLabel: 'Агенты',
      settingsLabel: 'Настройки',
      modePlan: 'Планировщик',
      modeDefault: 'По умолчанию',
      modeAutoEdit: 'Авто-редакт',
      modeYolo: 'YOLO',
      attachFileLabel: 'Прикрепить файл',
      mcpTitle: 'MCP серверы',
      mcpSubtitle: 'Управляйте qwen-compatible MCP конфигурацией прямо из desktop shell и сразу проверяйте доступность серверов.',
      mcpConnected: 'Подключены',
      mcpDisconnected: 'Отключены',
      mcpMissing: 'Не найдены',
      mcpTokens: 'Токены',
      mcpEmpty: 'MCP серверы пока не настроены.',
      mcpAddServer: 'Добавить сервер',
      mcpReconnect: 'Переподключить',
      mcpRemove: 'Удалить',
      mcpName: 'Имя',
      mcpScope: 'Scope',
      mcpTransport: 'Transport',
      mcpCommandOrUrl: 'Команда или URL',
      mcpDescription: 'Описание',
      mcpUserScope: 'User',
      mcpProjectScope: 'Project',
      mcpSave: 'Сохранить сервер',
      authTitle: 'Аутентификация',
      authSubtitle: 'Настраивайте qwen-oauth, Coding Plan и openai-compatible провайдеры прямо из desktop shell и сразу отдавайте их в native runtime.',
      authSelectedType: 'Тип',
      authSelectedScope: 'Scope',
      authStatus: 'Статус',
      authModel: 'Модель',
      authEndpoint: 'Endpoint',
      authApiKeyEnv: 'Переменная API key',
      authCredentials: 'Persisted credentials',
      authLastError: 'Последняя ошибка',
      authConnected: 'Подключено',
      authMissingCredentials: 'Нет credentials',
      authConfigureQwenOAuth: 'Подключить Qwen OAuth',
      authConfigureCodingPlan: 'Подключить Coding Plan',
      authConfigureOpenAi: 'Подключить OpenAI-compatible',
      authDisconnect: 'Отключить auth',
      authAccessToken: 'Access token',
      authRefreshToken: 'Refresh token',
      authApiKey: 'API key',
      authBaseUrl: 'Base URL',
      authModelName: 'Model',
      authScope: 'Scope',
      authRegion: 'Region',
      authScopeUser: 'User',
      authScopeProject: 'Project',
      authRegionChina: 'China',
      authRegionGlobal: 'Global',
      authSave: 'Сохранить auth',
      authClearCredentials: 'Очистить persisted credentials',
      authDisplayName: 'Провайдер',
      authCredentialPath: 'Credential path',
      authStartBrowserFlow: 'Запустить Qwen OAuth в браузере',
      authCancelFlow: 'Отменить flow',
      authFlowTitle: 'Qwen OAuth device flow',
      authFlowStatus: 'Flow status',
      authFlowUserCode: 'User code',
      authFlowUrl: 'Verification URL',
      authFlowExpires: 'Expires at',
      authFlowPending: 'Ожидает авторизации',
      authFlowSucceeded: 'Успешно завершен',
      authFlowCancelled: 'Отменен',
      authFlowTimedOut: 'Истек по таймауту',
      authFlowError: 'Ошибка',
    }
  }

  return {
    newChat: 'New session',
    searchChats: 'Search sessions',
    allChats: 'Conversations',
    workspace: 'Workspace',
    environment: 'Environment',
    connected: 'Bridge connected',
    localPreview: 'Local preview',
    activeTurns: 'active turns',
    nativeTools: 'Native tools',
    focusLabel: 'Focused workspace',
    heroEyebrow: 'Qwen-first desktop agent',
    heroTitle: "Let's build",
    heroSubtitle: '',
    heroNote: '',
    composerPlaceholder: 'What do you need done in code, architecture, or runtime right now?',
    continuePlaceholder: 'Continue this session...',
    send: 'Send',
    sending: 'Sending...',
    loadOlder: 'Older',
    loadNewer: 'Newer',
    noSessions: 'No sessions found yet.',
    emptySession: 'Select a session from the sidebar to open its transcript.',
    loading: 'Loading transcript...',
    pendingApprovals: 'Pending approvals',
    pendingQuestions: 'Pending questions',
    completedTools: 'Completed tools',
    failedTools: 'Failures',
    commands: 'Commands',
    tools: 'Tools',
    users: 'User',
    assistant: 'Assistant',
    recoverableTurns: 'Recoverable turns',
    resumeInterrupted: 'Resume',
    dismissInterrupted: 'Dismiss',
    recoveryReason: 'Sessions that can be safely brought back into the workspace.',
    liveTurn: 'Live turn',
    cancelTurn: 'Cancel turn',
    approveAndExecute: 'Approve and execute',
    answerQuestions: 'Send answers',
    answerPlaceholder: 'Choose an option or type your answer',
    showing: 'Showing',
    of: 'of',
    suggestionsTitle: 'Quick prompts',
    suggestions: [
      'Redesign the renderer into a Qwen-first desktop shell',
      'Build a typed IPC flow for the new workspace panels',
      'Improve approval UX and live transcript states',
      'Prepare a migration plan from the current UI to a system-grade interface',
    ],
    architectureTitle: 'Architecture tracks',
    architectureSubtitle: 'The product directions that matter right now.',
    toolPolicyTitle: 'Tool policy',
    toolPolicySubtitle: 'How runtime, approvals, and the native host are configured today.',
    projectMemoryTitle: 'Project memory',
    projectMemoryEmpty: 'No project summary yet. This surface will show the current goal and plan.',
    surfacesTitle: 'Compatibility surfaces',
    sessionOverviewTitle: 'Session overview',
    contextTitle: 'Context',
    timelineTitle: 'Timeline',
    transcriptTitle: 'Transcript',
    detailsLabel: 'Arguments',
    sourceLabel: 'Source path',
    changedFilesLabel: 'Changed files',
    branchLabel: 'Branch',
    statusLabel: 'Status',
    latestEventLabel: 'Latest event',
    readinessLabel: 'Ready',
    approvalsLabel: 'Approvals',
    allowedLabel: 'Allow',
    askLabel: 'Ask',
    noPendingApprovals: 'There are no tools waiting for approval right now.',
    noPendingQuestions: 'There are no pending questions right now.',
    modeCodeLabel: 'Code',
    projectSummaryLabel: 'Current project goal',
    updatedNowLabel: 'Updated just now',
    newConversation: 'New chat',
    skillsLabel: 'Skills',
    authLabel: 'Auth',
    mcpLabel: 'MCP',
    toolsNavLabel: 'Tools',
    agentsLabel: 'Agents',
    settingsLabel: 'Settings',
    modePlan: 'Planner',
    modeDefault: 'Default',
    modeAutoEdit: 'Auto-edit',
    modeYolo: 'YOLO',
    attachFileLabel: 'Attach file',
    mcpTitle: 'MCP servers',
    mcpSubtitle: 'Manage qwen-compatible MCP configuration directly from the desktop shell and validate server availability without leaving the workspace.',
    mcpConnected: 'Connected',
    mcpDisconnected: 'Disconnected',
    mcpMissing: 'Missing',
    mcpTokens: 'Tokens',
    mcpEmpty: 'No MCP servers are configured yet.',
    mcpAddServer: 'Add server',
    mcpReconnect: 'Reconnect',
    mcpRemove: 'Remove',
    mcpName: 'Name',
    mcpScope: 'Scope',
    mcpTransport: 'Transport',
    mcpCommandOrUrl: 'Command or URL',
    mcpDescription: 'Description',
    mcpUserScope: 'User',
    mcpProjectScope: 'Project',
    mcpSave: 'Save server',
    authTitle: 'Authentication',
    authSubtitle: 'Configure qwen-oauth, Coding Plan, and openai-compatible providers directly from the desktop shell and feed them into the native runtime.',
    authSelectedType: 'Type',
    authSelectedScope: 'Scope',
    authStatus: 'Status',
    authModel: 'Model',
    authEndpoint: 'Endpoint',
    authApiKeyEnv: 'API key env',
    authCredentials: 'Persisted credentials',
    authLastError: 'Last error',
    authConnected: 'Connected',
    authMissingCredentials: 'Missing credentials',
    authConfigureQwenOAuth: 'Connect Qwen OAuth',
    authConfigureCodingPlan: 'Connect Coding Plan',
    authConfigureOpenAi: 'Connect OpenAI-compatible',
    authDisconnect: 'Disconnect auth',
    authAccessToken: 'Access token',
    authRefreshToken: 'Refresh token',
    authApiKey: 'API key',
    authBaseUrl: 'Base URL',
    authModelName: 'Model',
    authScope: 'Scope',
    authRegion: 'Region',
    authScopeUser: 'User',
    authScopeProject: 'Project',
    authRegionChina: 'China',
    authRegionGlobal: 'Global',
    authSave: 'Save auth',
    authClearCredentials: 'Clear persisted credentials',
    authDisplayName: 'Provider',
    authCredentialPath: 'Credential path',
    authStartBrowserFlow: 'Start Qwen OAuth in browser',
    authCancelFlow: 'Cancel flow',
    authFlowTitle: 'Qwen OAuth device flow',
    authFlowStatus: 'Flow status',
    authFlowUserCode: 'User code',
    authFlowUrl: 'Verification URL',
    authFlowExpires: 'Expires at',
    authFlowPending: 'Pending authorization',
    authFlowSucceeded: 'Completed successfully',
    authFlowCancelled: 'Cancelled',
    authFlowTimedOut: 'Timed out',
    authFlowError: 'Error',
  }
}

export default App
