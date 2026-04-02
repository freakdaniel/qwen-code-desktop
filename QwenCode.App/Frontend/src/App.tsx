import { startTransition, useDeferredValue, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import './App.css'
import { Icon } from './AppIcons'
import { fallbackBootstrap } from './appData'
import qwenLogo from './assets/qwen-logo.svg'
import type {
  ActiveTurnState,
  AppBootstrapPayload,
  DesktopSessionDetail,
  DesktopSessionEntry,
  DesktopSessionEvent,
  DesktopSessionTurnResult,
  SessionPreview,
} from './types/desktop'

const SESSION_PAGE_SIZE = 120

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
  modeCodeLabel: string
  projectSummaryLabel: string
  updatedNowLabel: string
  newConversation: string
  skillsLabel: string
  toolsNavLabel: string
  agentsLabel: string
  settingsLabel: string
  modePlan: string
  modeDefault: string
  modeAutoEdit: string
  modeYolo: string
  attachFileLabel: string
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
  const [recoveringSessionId, setRecoveringSessionId] = useState('')
  const [dismissingSessionId, setDismissingSessionId] = useState('')
  const [selectedMode, setSelectedMode] = useState<'plan' | 'default' | 'auto-edit' | 'yolo'>('default')
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
    setSelectedSessionId('')
    setSelectedSessionDetail(null)
    setSessionPrompt('')
    setHomePrompt('')
  }

  const handleSelectSession = (sessionId: string) => {
    setSelectedSessionId(sessionId)
    setSelectedSessionDetail(null)
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
          <button className="sidebar-action-btn" type="button">
            <Icon name="wand" />
            <span>{copy.skillsLabel}</span>
          </button>
          <button className="sidebar-action-btn" type="button">
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

          {!selectedSessionId && (
            <HomeWorkspace
              copy={copy}
              heroVisible={heroVisible}
              isSubmittingPrompt={isSubmittingPrompt}
              projectName={latestProjectName}
              wittyPhrase={WITTY_LOADING_PHRASES[wittyPhraseIndex]}
            />
          )}

          {selectedSessionId && (
            <SessionWorkspace
              approvingEntryId={approvingEntryId}
              bootstrap={bootstrap}
              copy={copy}
              isLoadingSession={isLoadingSession}
              latestSessionEvent={latestSessionEvent}
              onApprovePendingTool={handleApprovePendingTool}
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

function SessionWorkspace({
  approvingEntryId,
  bootstrap,
  copy,
  isLoadingSession,
  latestSessionEvent,
  onApprovePendingTool,
  onCancelTurn,
  onLoadNewerEntries,
  onLoadOlderEntries,
  selectedSessionDetail,
  selectedSessionIsActive,
  selectedSessionStreamingText,
  selectedSessionWasReattached,
}: {
  approvingEntryId: string
  bootstrap: AppBootstrapPayload
  copy: UiCopy
  isLoadingSession: boolean
  latestSessionEvent: DesktopSessionEvent | null
  onApprovePendingTool: (entryId: string) => void
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
                  copy={copy}
                  entry={entry}
                  key={entry.id}
                  onApprovePendingTool={onApprovePendingTool}
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
  copy,
  entry,
  onApprovePendingTool,
}: {
  approvingEntryId: string
  copy: UiCopy
  entry: DesktopSessionEntry
  onApprovePendingTool: (entryId: string) => void
}) {
  const canApprove = entry.type === 'tool' && entry.status === 'approval-required' && !entry.resolutionStatus
  const isApproving = approvingEntryId === entry.id
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
    </article>
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
      },
    ],
  }
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
      modeCodeLabel: 'Code',
      projectSummaryLabel: 'Текущая цель проекта',
      updatedNowLabel: 'Обновлено только что',
      newConversation: 'Новая беседа',
      skillsLabel: 'Навыки',
      toolsNavLabel: 'Инструменты',
      agentsLabel: 'Агенты',
      settingsLabel: 'Настройки',
      modePlan: 'Планировщик',
      modeDefault: 'По умолчанию',
      modeAutoEdit: 'Авто-редакт',
      modeYolo: 'YOLO',
      attachFileLabel: 'Прикрепить файл',
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
    modeCodeLabel: 'Code',
    projectSummaryLabel: 'Current project goal',
    updatedNowLabel: 'Updated just now',
    newConversation: 'New chat',
    skillsLabel: 'Skills',
    toolsNavLabel: 'Tools',
    agentsLabel: 'Agents',
    settingsLabel: 'Settings',
    modePlan: 'Planner',
    modeDefault: 'Default',
    modeAutoEdit: 'Auto-edit',
    modeYolo: 'YOLO',
    attachFileLabel: 'Attach file',
  }
}

export default App
