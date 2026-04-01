import type { ReactNode } from 'react'
import { startTransition, useDeferredValue, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import './App.css'
import { Icon } from './AppIcons'
import {
  fallbackBootstrap,
  formatSessionMode,
  getCopy,
} from './appData'
import type {
  LocaleCopy,
  NavItem,
} from './appData'
import type {
  CapabilityLane,
  DesktopSessionDetail,
  DesktopSessionEntry,
  DesktopSessionTurnResult,
  RuntimePortWorkItem,
  SessionPreview,
  SourceMirrorStatus,
} from './types/desktop'

function App() {
  const { i18n } = useTranslation()
  const [bootstrap, setBootstrap] = useState(fallbackBootstrap)
  const [query, setQuery] = useState('')
  const [connected, setConnected] = useState(false)
  const [activeView, setActiveView] = useState<NavItem['id']>('home')
  const [selectedPatternIndex, setSelectedPatternIndex] = useState(0)
  const [composerPrompt, setComposerPrompt] = useState('')
  const [isStartingTurn, setIsStartingTurn] = useState(false)
  const [latestTurn, setLatestTurn] = useState<DesktopSessionTurnResult | null>(null)
  const [selectedSessionId, setSelectedSessionId] = useState('')
  const [selectedSessionDetail, setSelectedSessionDetail] = useState<DesktopSessionDetail | null>(null)
  const [isLoadingSession, setIsLoadingSession] = useState(false)
  const [sessionReplyPrompt, setSessionReplyPrompt] = useState('')
  const [isContinuingSession, setIsContinuingSession] = useState(false)
  const [selectedSessionToolName, setSelectedSessionToolName] = useState('')
  const [selectedSessionToolArgs, setSelectedSessionToolArgs] = useState('{}')
  const [approveSessionToolExecution, setApproveSessionToolExecution] = useState(false)
  const [approvingSessionToolEntryId, setApprovingSessionToolEntryId] = useState('')
  const deferredQuery = useDeferredValue(query)
  const copy = getCopy(bootstrap.currentLocale)

  useEffect(() => {
    let dispose: (() => void) | undefined

    const hydrate = async () => {
      if (!window.qwenDesktop) {
        await i18n.changeLanguage(fallbackBootstrap.currentLocale)
        return
      }

      const payload = await window.qwenDesktop.bootstrap()
      setBootstrap(payload)
      setSelectedSessionId(payload.recentSessions[0]?.sessionId ?? '')
      setConnected(true)
      await i18n.changeLanguage(payload.currentLocale)

      dispose = window.qwenDesktop.subscribeStateChanged((event) => {
        setBootstrap((current) => ({
          ...current,
          currentLocale: event.currentLocale,
        }))

        startTransition(() => {
          void i18n.changeLanguage(event.currentLocale)
        })
      })
    }

    void hydrate()
    return () => dispose?.()
  }, [i18n])

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
        setSelectedSessionDetail({
          session: latestTurn.session,
          transcriptPath: latestTurn.session.transcriptPath,
          entryCount: 3,
          summary: {
            userCount: 1,
            assistantCount: 1,
            commandCount: latestTurn.resolvedCommand ? 1 : 0,
            toolCount: 0,
            pendingApprovalCount: 0,
            completedToolCount: 0,
            failedToolCount: 0,
            lastTimestamp: '',
          },
          entries: [
            {
              id: `${latestTurn.session.sessionId}-user`,
              type: 'user',
              timestamp: '',
              workingDirectory: latestTurn.session.workingDirectory,
              gitBranch: latestTurn.session.gitBranch,
              title: 'User',
              body: latestTurn.session.title,
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
            ...(latestTurn.resolvedCommand ? [{
              id: `${latestTurn.session.sessionId}-command`,
              type: 'command',
              timestamp: '',
              workingDirectory: latestTurn.session.workingDirectory,
              gitBranch: latestTurn.session.gitBranch,
              title: `/${latestTurn.resolvedCommand.name}`,
              body: latestTurn.resolvedCommand.resolvedPrompt,
              status: 'completed',
              approvalState: '',
              exitCode: null,
              arguments: latestTurn.resolvedCommand.arguments,
              scope: latestTurn.resolvedCommand.scope,
              sourcePath: latestTurn.resolvedCommand.sourcePath,
              resolutionStatus: '',
              resolvedAt: '',
              changedFiles: [],
              toolName: latestTurn.resolvedCommand.name,
            }] : []),
            {
              id: `${latestTurn.session.sessionId}-assistant`,
              type: 'assistant',
              timestamp: '',
              workingDirectory: latestTurn.session.workingDirectory,
              gitBranch: latestTurn.session.gitBranch,
              title: 'Assistant',
              body: latestTurn.assistantSummary,
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
        })
      }

      return
    }

    let cancelled = false

    const load = async () => {
      setIsLoadingSession(true)

      try {
        const detail = await window.qwenDesktop?.getSession({ sessionId: selectedSessionId })
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

  const visibleSessions = bootstrap.recentSessions.filter((item) => {
    const haystack = `${item.title} ${item.category} ${item.lastActivity}`.toLowerCase()
    return haystack.includes(deferredQuery.toLowerCase())
  })

  const visiblePatterns = bootstrap.adoptionPatterns.filter((item) => {
    const haystack = `${item.area} ${item.qwenSource} ${item.claudeReference} ${item.desktopDirection}`.toLowerCase()
    return haystack.includes(deferredQuery.toLowerCase())
  })

  const normalizedPatternIndex =
    visiblePatterns.length === 0 || selectedPatternIndex < visiblePatterns.length
      ? selectedPatternIndex
      : 0

  const selectedPattern =
    visiblePatterns[normalizedPatternIndex] ??
    bootstrap.adoptionPatterns[0] ??
    fallbackBootstrap.adoptionPatterns[0]
  const compatibilityPills = [
    ...bootstrap.qwenCompatibility.commands.slice(0, 3).map((item) => `/${item.name}`),
    ...bootstrap.qwenCompatibility.skills.slice(0, 3).map((item) => item.name),
  ]

  const primaryNav: NavItem[] = [
    { id: 'home', label: copy.newChat, icon: 'plus' },
    { id: 'search', label: copy.search, icon: 'search' },
    { id: 'customize', label: copy.customize, icon: 'customize' },
  ]

  const libraryNav: NavItem[] = [
    { id: 'chats', label: copy.chats, icon: 'chats' },
    { id: 'projects', label: copy.projects, icon: 'projects' },
    { id: 'artifacts', label: copy.artifacts, icon: 'artifacts' },
  ]

  const handleLocaleChange = (locale: string) => {
    startTransition(() => {
      void i18n.changeLanguage(locale)
      setBootstrap((current) => ({ ...current, currentLocale: locale }))
    })

    void window.qwenDesktop?.setLocale(locale)
  }

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

  const handleSelectSession = (sessionId: string) => {
    setSelectedSessionId(sessionId)
    setActiveView('chats')
  }

  const handleStartTurn = async () => {
    const prompt = composerPrompt.trim()
    if (!prompt || isStartingTurn) {
      return
    }

    setIsStartingTurn(true)

    try {
      if (!window.qwenDesktop) {
        const previewSession: SessionPreview = {
          sessionId: `preview-${Date.now()}`,
          title: prompt.length > 120 ? `${prompt.slice(0, 120)}...` : prompt,
          lastActivity: 'Updated just now',
          category: 'preview',
          mode: 'code',
          status: 'resume-ready',
          workingDirectory: bootstrap.sources.workspaceRoot,
          gitBranch: 'preview',
          messageCount: 2,
          transcriptPath: `${bootstrap.sources.workspaceRoot}\\.qwen\\chats\\preview.jsonl`,
        }

        applyTurnResult({
          session: previewSession,
          assistantSummary: 'Preview turn recorded without the native desktop bridge.',
          createdNewSession: true,
          resolvedCommand: null,
          toolExecution: {
            toolName: '',
            status: 'not-requested',
            approvalState: 'allow',
            workingDirectory: bootstrap.sources.workspaceRoot,
            output: '',
            errorMessage: '',
            exitCode: 0,
            changedFiles: [],
          },
        })
      } else {
        const result = await window.qwenDesktop.startSessionTurn({
          sessionId: '',
          prompt,
          workingDirectory: bootstrap.sources.workspaceRoot,
          toolName: '',
          toolArgumentsJson: '{}',
          approveToolExecution: false,
        })

        applyTurnResult(result)
      }

      setComposerPrompt('')
      setActiveView('chats')
    } finally {
      setIsStartingTurn(false)
    }
  }

  const handleContinueSession = async () => {
    const prompt = sessionReplyPrompt.trim()
    if (!prompt || !selectedSessionId || isContinuingSession) {
      return
    }

    setIsContinuingSession(true)

    try {
      if (!window.qwenDesktop) {
        if (selectedSessionDetail) {
          const continuedSession: SessionPreview = {
            ...selectedSessionDetail.session,
            title: selectedSessionDetail.session.title,
            lastActivity: 'Updated just now',
            messageCount: selectedSessionDetail.session.messageCount + (selectedSessionToolName ? 3 : 2),
          }

          applyTurnResult({
            session: continuedSession,
            assistantSummary: 'Preview turn appended to the selected session without the native desktop bridge.',
            createdNewSession: false,
            resolvedCommand: null,
            toolExecution: {
              toolName: selectedSessionToolName,
              status: selectedSessionToolName ? (approveSessionToolExecution ? 'completed' : 'approval-required') : 'not-requested',
              approvalState: selectedSessionToolName ? 'ask' : 'allow',
              workingDirectory: continuedSession.workingDirectory,
              output: selectedSessionToolName ? 'Preview tool execution was not sent to the native desktop bridge.' : '',
              errorMessage: '',
              exitCode: 0,
              changedFiles: [],
            },
          })
        }
      } else {
        const result = await window.qwenDesktop.startSessionTurn({
          sessionId: selectedSessionId,
          prompt,
          workingDirectory: selectedSessionDetail?.session.workingDirectory ?? bootstrap.sources.workspaceRoot,
          toolName: selectedSessionToolName,
          toolArgumentsJson: selectedSessionToolArgs.trim() || '{}',
          approveToolExecution: approveSessionToolExecution,
        })

        applyTurnResult(result)
      }

      setSessionReplyPrompt('')
      setSelectedSessionToolName('')
      setSelectedSessionToolArgs('{}')
      setApproveSessionToolExecution(false)
    } finally {
      setIsContinuingSession(false)
    }
  }

  const handleApprovePendingTool = async (entryId: string) => {
    if (!selectedSessionId || !entryId || approvingSessionToolEntryId) {
      return
    }

    setApprovingSessionToolEntryId(entryId)

    try {
      if (!window.qwenDesktop) {
        return
      }

      const result = await window.qwenDesktop.approvePendingTool({
        sessionId: selectedSessionId,
        entryId,
      })

      applyTurnResult(result)
    } finally {
      setApprovingSessionToolEntryId('')
    }
  }

  return (
    <div className="app-frame">
      <header className="window-chrome">
        <div className="chrome-actions">
          <button className="chrome-icon" type="button"><Icon name="menu" /></button>
          <button className="chrome-icon" type="button"><Icon name="split" /></button>
          <button className="chrome-icon" type="button"><Icon name="back" /></button>
          <button className="chrome-icon muted" type="button"><Icon name="forward" /></button>
        </div>

        <div className="chrome-status">
          <span className={`status-pill ${connected ? 'connected' : ''}`}>
            {connected ? copy.bridgeStatus.connected : copy.bridgeStatus.local}
          </span>
          <button className="ghost-button" type="button"><Icon name="ghost" /></button>
        </div>
      </header>

      <div className="shell-layout">
        <aside className="sidebar">
          <div className="sidebar-top">
            {primaryNav.map((item) => (
              <button
                className={`sidebar-action ${activeView === item.id ? 'active' : ''}`}
                key={item.id}
                onClick={() => setActiveView(item.id)}
                type="button"
              >
                <span className="action-icon"><Icon name={item.icon} /></span>
                <span>{item.label}</span>
              </button>
            ))}
          </div>

          <div className="sidebar-divider" />

          <nav className="sidebar-nav">
            {libraryNav.map((item) => (
              <button
                className={`nav-item ${activeView === item.id ? 'active' : ''}`}
                key={item.id}
                onClick={() => setActiveView(item.id)}
                type="button"
              >
                <span className="action-icon"><Icon name={item.icon} /></span>
                <span>{item.label}</span>
              </button>
            ))}
          </nav>

          <section className="recent-panel">
            <div className="sidebar-label">{copy.recents}</div>
            <div className="recent-list">
              {bootstrap.recentSessions.map((session) => (
                <button
                  className="recent-item"
                  key={session.sessionId}
                  onClick={() => handleSelectSession(session.sessionId)}
                  type="button"
                >
                  <span className="recent-title">{session.title}</span>
                  <span className="recent-meta">{session.lastActivity}</span>
                </button>
              ))}
            </div>
          </section>

          <footer className="profile-card">
            <div className="profile-avatar">D</div>
            <div className="profile-copy">
              <strong>Daniel</strong>
              <span>Pro plan</span>
            </div>
            <button className="profile-action" type="button"><Icon name="forward" /></button>
          </footer>
        </aside>

        <main className="workspace">
          {(activeView === 'home' || activeView === 'search') && (
            <HomeView
              bootstrap={bootstrap}
              copy={copy}
              composerPrompt={composerPrompt}
              isStartingTurn={isStartingTurn}
              latestTurn={latestTurn}
              onLocaleChange={handleLocaleChange}
              onPromptChange={setComposerPrompt}
              onStartTurn={handleStartTurn}
            />
          )}

          {activeView === 'customize' && (
            <CustomizeView
              copy={copy}
              compatibilityPills={compatibilityPills}
              selectedPattern={selectedPattern}
              selectedPatternIndex={normalizedPatternIndex}
              setSelectedPatternIndex={setSelectedPatternIndex}
              setActiveView={setActiveView}
              visiblePatterns={visiblePatterns}
              lanes={bootstrap.capabilityLanes}
            />
          )}

          {activeView === 'chats' && (
            <ChatsView
              copy={copy}
              query={query}
              setQuery={setQuery}
              sessions={visibleSessions}
              selectedSessionId={selectedSessionId}
              selectedSessionDetail={selectedSessionDetail}
              isLoadingSession={isLoadingSession}
              sessionReplyPrompt={sessionReplyPrompt}
              setSessionReplyPrompt={setSessionReplyPrompt}
              isContinuingSession={isContinuingSession}
              selectedSessionToolName={selectedSessionToolName}
              setSelectedSessionToolName={setSelectedSessionToolName}
              selectedSessionToolArgs={selectedSessionToolArgs}
              setSelectedSessionToolArgs={setSelectedSessionToolArgs}
              approveSessionToolExecution={approveSessionToolExecution}
              setApproveSessionToolExecution={setApproveSessionToolExecution}
              availableTools={bootstrap.qwenNativeHost.tools.map((tool) => tool.name)}
              approvingSessionToolEntryId={approvingSessionToolEntryId}
              onApprovePendingTool={handleApprovePendingTool}
              onContinueSession={handleContinueSession}
              onSelectSession={handleSelectSession}
              onNewChat={() => setActiveView('home')}
            />
          )}

          {activeView === 'projects' && (
            <LibraryView
              title={copy.projectsTitle}
              subtitle={copy.projectsSubtitle}
            >
              <div className="library-grid">
                {bootstrap.qwenNativeHost.tools.map((tool) => (
                  <NativeHostToolCard key={tool.name} tool={tool} />
                ))}
                {bootstrap.qwenTools.tools.map((tool) => (
                  <ToolCard key={tool.name} tool={tool} />
                ))}
                {bootstrap.runtimePortPlan.map((item) => (
                  <PortWorkItemCard key={item.id} item={item} />
                ))}
                {bootstrap.capabilityLanes.map((lane) => (
                  <LaneCard copy={copy} key={lane.title} lane={lane} />
                ))}
              </div>
            </LibraryView>
          )}

          {activeView === 'artifacts' && (
            <LibraryView
              title={copy.artifactsTitle}
              subtitle={copy.artifactsSubtitle}
            >
              <div className="library-grid">
                {bootstrap.tracks.map((track) => (
                  <article className="artifact-card" key={track.title}>
                    <span>{copy.deliveryState}</span>
                    <h2>{track.title}</h2>
                    <p>{track.summary}</p>
                  </article>
                ))}
              </div>
            </LibraryView>
          )}
        </main>
      </div>
    </div>
  )
}

function HomeView({
  bootstrap,
  copy,
  composerPrompt,
  isStartingTurn,
  latestTurn,
  onLocaleChange,
  onPromptChange,
  onStartTurn,
}: {
  bootstrap: typeof fallbackBootstrap
  copy: LocaleCopy
  composerPrompt: string
  isStartingTurn: boolean
  latestTurn: DesktopSessionTurnResult | null
  onLocaleChange: (locale: string) => void
  onPromptChange: (value: string) => void
  onStartTurn: () => void
}) {
  return (
    <>
      <section className="view-header">
        <div>
          <div className="eyebrow">{copy.appLabel}</div>
          <h1>{copy.rootViewTitle}</h1>
          <p>{copy.rootViewSubtitle}</p>
        </div>

        <label className="locale-picker">
          <span>{copy.currentLocale}</span>
          <select
            onChange={(event) => onLocaleChange(event.target.value)}
            value={bootstrap.currentLocale}
          >
            {bootstrap.locales.map((locale) => (
              <option key={locale.code} value={locale.code}>
                {locale.nativeName}
              </option>
            ))}
          </select>
        </label>
      </section>

      <section className="home-shell">
        <div className="greeting-panel">
          <div className="greeting-mark"><Icon name="spark" /></div>
          <div>
            <h2>{copy.homeGreeting}</h2>
            <p>{copy.homeLead}</p>
          </div>
        </div>

        <div className="composer-card">
          <textarea
            aria-label="Prompt composer"
            onChange={(event) => onPromptChange(event.target.value)}
            placeholder={copy.composerPlaceholder[bootstrap.currentMode]}
            rows={5}
            value={composerPrompt}
          />
          <div className="composer-meta">
            <button className="ghost-button align-left" type="button"><Icon name="plus" /></button>
            <div className="composer-model">
              <span>{copy.modelLabel}</span>
              <strong>{formatSessionMode(bootstrap.currentMode)}</strong>
            </div>
            <button className="primary-button" onClick={onStartTurn} type="button">
              <span>{isStartingTurn ? copy.sendingLabel : copy.sendLabel}</span>
            </button>
          </div>
        </div>

        <div className="quick-actions">
          {copy.quickActions.map((action) => (
            <button className="quick-action" key={action.label} type="button">
              <Icon name={action.icon} />
              <span>{action.label}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="overview-grid">
        <article className="panel">
          <div className="panel-heading">{copy.modeLabel}</div>
          <div className="mode-callout">
            <span className="mode-badge">{formatSessionMode(bootstrap.currentMode)}</span>
            <p>{copy.homeModeDescriptions[bootstrap.currentMode]}</p>
          </div>
        </article>

        <article className="panel">
          <div className="panel-heading">{copy.sessionHostLabel}</div>
          {latestTurn ? (
            <div className="source-list">
              <div className="source-item">
                <div className="source-topline">
                  <span>{latestTurn.createdNewSession ? copy.sessionCreatedLabel : copy.sessionUpdatedLabel}</span>
                  <strong className="mirror-status ready">{formatSessionMode(latestTurn.session.mode)}</strong>
                </div>
                <strong>{latestTurn.session.title}</strong>
                <p className="source-summary">{latestTurn.assistantSummary}</p>
                <div className="source-highlights">
                  <span className="source-pill">{latestTurn.session.workingDirectory}</span>
                  <span className="source-pill">{copy.transcriptLabel}: {latestTurn.session.transcriptPath}</span>
                  {latestTurn.resolvedCommand && (
                    <span className="source-pill">/{latestTurn.resolvedCommand.name}</span>
                  )}
                </div>
              </div>
            </div>
          ) : (
            <div className="mode-callout">
              <span className="mode-badge">.qwen</span>
              <p>GUI composer now starts real desktop turns through the native .NET session host.</p>
            </div>
          )}
        </article>

        <article className="panel">
          <div className="panel-heading">{copy.sourceMirrors}</div>
          <div className="source-list">
            {bootstrap.sourceStatuses.map((status) => (
              <MirrorStatusCard key={status.id} mirror={status} />
            ))}
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">{copy.compatibilityGoals}</div>
          <div className="goal-list">
            {bootstrap.compatibilityGoals.map((goal, index) => (
              <div className="goal-item" key={goal}>
                <span className="goal-index">{String(index + 1).padStart(2, '0')}</span>
                <p>{goal}</p>
              </div>
            ))}
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">{copy.settingsLayersLabel}</div>
          <div className="source-list">
            {bootstrap.qwenCompatibility.settingsLayers.map((layer) => (
              <div className="source-item" key={layer.id}>
                <div className="source-topline">
                  <span>{layer.title}</span>
                  <strong className={`mirror-status ${layer.exists ? 'ready' : 'missing'}`}>
                    P{layer.priority}
                  </strong>
                </div>
                <strong>{layer.path}</strong>
                <p className="source-summary">{layer.scope}</p>
                <div className="source-highlights">
                  {(layer.categories.length > 0 ? layer.categories : ['No categories detected']).map((item) => (
                    <span className="source-pill" key={item}>{item}</span>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">{copy.surfaceDirectoriesLabel}</div>
          <div className="source-list">
            {bootstrap.qwenCompatibility.surfaceDirectories.map((surface) => (
              <div className="source-item" key={surface.id}>
                <div className="source-topline">
                  <span>{surface.title}</span>
                  <strong className={`mirror-status ${surface.exists ? 'ready' : 'missing'}`}>
                    {surface.itemCount}
                  </strong>
                </div>
                <strong>{surface.path}</strong>
                <p className="source-summary">{surface.summary}</p>
              </div>
            ))}
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">Commands and skills</div>
          <div className="source-list">
            <div className="source-item">
              <div className="source-topline">
                <span>Slash commands</span>
                <strong className="mirror-status ready">{bootstrap.qwenCompatibility.commands.length}</strong>
              </div>
              <strong>{bootstrap.qwenCompatibility.commands.map((item) => item.name).slice(0, 3).join(' • ') || 'No commands discovered'}</strong>
              <p className="source-summary">qwen-compatible markdown commands discovered from project and user .qwen surfaces.</p>
              <div className="source-highlights">
                {bootstrap.qwenCompatibility.commands.slice(0, 6).map((command) => (
                  <span className="source-pill" key={command.id}>{command.scope}:{command.name}</span>
                ))}
              </div>
            </div>

            <div className="source-item">
              <div className="source-topline">
                <span>Skills</span>
                <strong className="mirror-status ready">{bootstrap.qwenCompatibility.skills.length}</strong>
              </div>
              <strong>{bootstrap.qwenCompatibility.skills.map((item) => item.name).slice(0, 3).join(' • ') || 'No skills discovered'}</strong>
              <p className="source-summary">Structured SKILL.md bundles are now native desktop surfaces instead of hidden compatibility folders.</p>
              <div className="source-highlights">
                {bootstrap.qwenCompatibility.skills.slice(0, 6).map((skill) => (
                  <span className="source-pill" key={skill.id}>{skill.scope}:{skill.name}</span>
                ))}
              </div>
            </div>
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">{copy.runtimeProfileLabel}</div>
          <div className="source-list">
            <div className="source-item">
              <div className="source-topline">
                <span>Runtime base</span>
                <strong className="mirror-status ready">{bootstrap.qwenRuntime.runtimeSource}</strong>
              </div>
              <strong>{bootstrap.qwenRuntime.runtimeBaseDirectory}</strong>
              <p className="source-summary">{bootstrap.qwenRuntime.projectDataDirectory}</p>
              <div className="source-highlights">
                <span className="source-pill">{bootstrap.qwenRuntime.chatsDirectory}</span>
                <span className="source-pill">{bootstrap.qwenRuntime.historyDirectory}</span>
              </div>
            </div>

            <div className="source-item">
              <div className="source-topline">
                <span>{copy.runtimeApprovalLabel}</span>
                <strong className="mirror-status ready">{bootstrap.qwenRuntime.approvalProfile.defaultMode}</strong>
              </div>
              <strong>{bootstrap.qwenRuntime.contextFileNames.join(', ')}</strong>
              <p className="source-summary">{bootstrap.qwenRuntime.contextFilePaths.join(' • ')}</p>
              <div className="source-highlights">
                {[
                  ...bootstrap.qwenRuntime.approvalProfile.allowRules.slice(0, 2),
                  ...bootstrap.qwenRuntime.approvalProfile.askRules.slice(0, 1),
                  ...bootstrap.qwenRuntime.approvalProfile.denyRules.slice(0, 1),
                ].map((rule) => (
                  <span className="source-pill" key={rule}>{rule}</span>
                ))}
              </div>
            </div>
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">{copy.toolCatalogLabel}</div>
          <div className="source-list">
            <div className="source-item">
              <div className="source-topline">
                <span>{bootstrap.qwenTools.sourceMode}</span>
                <strong className="mirror-status ready">{bootstrap.qwenTools.totalCount}</strong>
              </div>
              <strong>
                allow {bootstrap.qwenTools.allowedCount} • ask {bootstrap.qwenTools.askCount} • deny {bootstrap.qwenTools.denyCount}
              </strong>
              <p className="source-summary">qwen tool contracts and approval defaults interpreted by the .NET host.</p>
              <div className="source-highlights">
                {bootstrap.qwenTools.tools.slice(0, 6).map((tool) => (
                  <span className="source-pill" key={tool.name}>{tool.displayName}:{tool.approvalState}</span>
                ))}
              </div>
            </div>
          </div>
        </article>

        <article className="panel wide">
          <div className="panel-heading">{copy.nativeHostLabel}</div>
          <div className="source-list">
            <div className="source-item">
              <div className="source-topline">
                <span>.NET native tool host</span>
                <strong className="mirror-status ready">{bootstrap.qwenNativeHost.registeredCount}</strong>
              </div>
              <strong>
                ready {bootstrap.qwenNativeHost.readyCount} • ask {bootstrap.qwenNativeHost.approvalRequiredCount} • implemented {bootstrap.qwenNativeHost.implementedCount}
              </strong>
              <p className="source-summary">Native qwen-compatible tools executed inside the desktop backend rather than through CLI stdout.</p>
              <div className="source-highlights">
                {bootstrap.qwenNativeHost.tools.slice(0, 7).map((tool) => (
                  <span className="source-pill" key={tool.name}>{tool.displayName}:{tool.approvalState}</span>
                ))}
              </div>
            </div>
          </div>
        </article>
      </section>
    </>
  )
}

function CustomizeView({
  copy,
  compatibilityPills,
  selectedPattern,
  selectedPatternIndex,
  setSelectedPatternIndex,
  setActiveView,
  visiblePatterns,
  lanes,
}: {
  copy: LocaleCopy
  compatibilityPills: string[]
  selectedPattern: typeof fallbackBootstrap.adoptionPatterns[number]
  selectedPatternIndex: number
  setSelectedPatternIndex: (index: number) => void
  setActiveView: (view: NavItem['id']) => void
  visiblePatterns: typeof fallbackBootstrap.adoptionPatterns
  lanes: CapabilityLane[]
}) {
  return (
    <section className="split-view">
      <div className="split-pane nav-pane">
        <div className="split-title">
          <button className="ghost-button compact" onClick={() => setActiveView('home')} type="button">
            <Icon name="chevronLeft" />
          </button>
          <div>
            <h1>{copy.customizeTitle}</h1>
            <p>{copy.customizeSubtitle}</p>
          </div>
        </div>

        <div className="category-list">
          <button className="category-item active" type="button"><span className="action-icon"><Icon name="customize" /></span><span>Skills</span></button>
          <button className="category-item" type="button"><span className="action-icon"><Icon name="split" /></span><span>Connectors</span></button>
          <button className="category-item" type="button"><span className="action-icon"><Icon name="spark" /></span><span>Plugins</span></button>
        </div>
      </div>

      <div className="split-pane library-pane">
        <div className="pane-head">
          <div>
            <div className="sidebar-label">{copy.customizeLibraryTitle}</div>
            <h2>Adoption patterns</h2>
          </div>
          <button className="ghost-button compact" type="button"><Icon name="plus" /></button>
        </div>

        <div className="pattern-list">
          {visiblePatterns.map((pattern, index) => (
            <button
              className={`pattern-item ${selectedPatternIndex === index ? 'active' : ''}`}
              key={pattern.area}
              onClick={() => setSelectedPatternIndex(index)}
              type="button"
            >
              <span>{pattern.area}</span>
              <small>{pattern.deliveryState}</small>
            </button>
          ))}
        </div>
      </div>

      <div className="split-pane detail-pane">
        <div className="detail-header">
          <div>
            <h2>{selectedPattern.area}</h2>
            <p>{copy.customizeDetailTitle}</p>
          </div>
          <span className="toggle-indicator">{selectedPattern.deliveryState}</span>
        </div>

        <div className="detail-stack">
          <DetailCard body={selectedPattern.qwenSource} title={copy.referenceFromQwen} />
          <DetailCard body={selectedPattern.claudeReference} title={copy.referenceFromClaude} />
          <DetailCard body={selectedPattern.desktopDirection} title={copy.desktopDecision} />
          <article className="detail-card">
            <span>qwen compatibility surfaces</span>
            <div className="source-highlights">
              {compatibilityPills.map((item) => (
                <span className="source-pill" key={item}>{item}</span>
              ))}
            </div>
          </article>
          <article className="detail-card">
            <span>{copy.capabilityLanes}</span>
            <div className="lane-list">
              {lanes.map((lane) => (
                <LaneCard copy={copy} key={lane.title} lane={lane} />
              ))}
            </div>
          </article>
        </div>
      </div>
    </section>
  )
}

function ChatsView({
  copy,
  query,
  setQuery,
  sessions,
  selectedSessionId,
  selectedSessionDetail,
  isLoadingSession,
  sessionReplyPrompt,
  setSessionReplyPrompt,
  isContinuingSession,
  selectedSessionToolName,
  setSelectedSessionToolName,
  selectedSessionToolArgs,
  setSelectedSessionToolArgs,
  approveSessionToolExecution,
  setApproveSessionToolExecution,
  availableTools,
  approvingSessionToolEntryId,
  onApprovePendingTool,
  onContinueSession,
  onSelectSession,
  onNewChat,
}: {
  copy: LocaleCopy
  query: string
  setQuery: (value: string) => void
  sessions: SessionPreview[]
  selectedSessionId: string
  selectedSessionDetail: DesktopSessionDetail | null
  isLoadingSession: boolean
  sessionReplyPrompt: string
  setSessionReplyPrompt: (value: string) => void
  isContinuingSession: boolean
  selectedSessionToolName: string
  setSelectedSessionToolName: (value: string) => void
  selectedSessionToolArgs: string
  setSelectedSessionToolArgs: (value: string) => void
  approveSessionToolExecution: boolean
  setApproveSessionToolExecution: (value: boolean) => void
  availableTools: string[]
  approvingSessionToolEntryId: string
  onApprovePendingTool: (entryId: string) => void
  onContinueSession: () => void
  onSelectSession: (sessionId: string) => void
  onNewChat: () => void
}) {
  return (
    <section className="chats-view">
      <div className="view-header inline">
        <div>
          <h1>{copy.chatSurfaceTitle}</h1>
          <p>{copy.chatSurfaceSubtitle}</p>
        </div>
        <button className="primary-button" onClick={onNewChat} type="button">
          <Icon name="plus" />
          <span>{copy.newChat}</span>
        </button>
      </div>

      <label className="search-input">
        <span className="action-icon"><Icon name="search" /></span>
        <input
          onChange={(event) => setQuery(event.target.value)}
          placeholder={copy.searchPlaceholder}
          value={query}
        />
      </label>

      <div className="chat-list-header">
        <span>{copy.allConversations}</span>
        <button className="link-button" type="button">Select</button>
      </div>

      <div className="sessions-layout">
        <div className="chat-list">
          {sessions.length === 0 && <div className="empty-state">{copy.emptySearch}</div>}
          {sessions.map((session) => (
            <button
              className={`chat-row ${selectedSessionId === session.sessionId ? 'active' : ''}`}
              key={session.sessionId}
              onClick={() => onSelectSession(session.sessionId)}
              type="button"
            >
              <div>
                <strong>{session.title}</strong>
                <p>{session.lastActivity}</p>
              </div>
              <div className="chat-row-meta">
                <span>{session.category}</span>
                <span>{session.messageCount} msgs</span>
                <span>{copy.modeLabel}: {formatSessionMode(session.mode)}</span>
              </div>
            </button>
          ))}
        </div>

        <article className="session-detail">
          {!selectedSessionId && <div className="empty-state">Select a session to inspect its qwen transcript.</div>}
          {selectedSessionId && isLoadingSession && <div className="empty-state">Loading transcript...</div>}
          {selectedSessionId && !isLoadingSession && !selectedSessionDetail && (
            <div className="empty-state">Transcript detail is not available for this session yet.</div>
          )}
          {selectedSessionDetail && (
            <>
              <div className="session-detail-header">
                <div>
                  <h2>{selectedSessionDetail.session.title}</h2>
                  <p>{selectedSessionDetail.transcriptPath}</p>
                </div>
                <span className="source-pill">{selectedSessionDetail.entryCount} entries</span>
              </div>

              <div className="session-summary-grid">
                <span className="source-pill">users {selectedSessionDetail.summary.userCount}</span>
                <span className="source-pill">assistant {selectedSessionDetail.summary.assistantCount}</span>
                <span className="source-pill">commands {selectedSessionDetail.summary.commandCount}</span>
                <span className="source-pill">tools {selectedSessionDetail.summary.toolCount}</span>
                <span className="source-pill">pending approvals {selectedSessionDetail.summary.pendingApprovalCount}</span>
                <span className="source-pill">completed tools {selectedSessionDetail.summary.completedToolCount}</span>
                {selectedSessionDetail.summary.failedToolCount > 0 && (
                  <span className="source-pill">failed tools {selectedSessionDetail.summary.failedToolCount}</span>
                )}
                {selectedSessionDetail.summary.lastTimestamp && (
                  <span className="source-pill">{selectedSessionDetail.summary.lastTimestamp}</span>
                )}
              </div>

              <div className="session-entry-list">
                {selectedSessionDetail.entries.map((entry) => (
                  <SessionTimelineEntry
                    approvingEntryId={approvingSessionToolEntryId}
                    entry={entry}
                    key={entry.id}
                    onApprovePendingTool={onApprovePendingTool}
                  />
                ))}
              </div>

              <div className="session-reply">
                <textarea
                  aria-label="Session reply composer"
                  onChange={(event) => setSessionReplyPrompt(event.target.value)}
                  placeholder="Continue this code session..."
                  rows={4}
                  value={sessionReplyPrompt}
                />
                <div className="session-tool-config">
                  <label className="session-tool-field">
                    <span>Native tool</span>
                    <select
                      onChange={(event) => setSelectedSessionToolName(event.target.value)}
                      value={selectedSessionToolName}
                    >
                      <option value="">No tool</option>
                      {availableTools.map((tool) => (
                        <option key={tool} value={tool}>{tool}</option>
                      ))}
                    </select>
                  </label>

                  <label className="session-tool-field wide">
                    <span>Tool arguments JSON</span>
                    <textarea
                      onChange={(event) => setSelectedSessionToolArgs(event.target.value)}
                      rows={4}
                      value={selectedSessionToolArgs}
                    />
                  </label>

                  <label className="session-tool-toggle">
                    <input
                      checked={approveSessionToolExecution}
                      onChange={(event) => setApproveSessionToolExecution(event.target.checked)}
                      type="checkbox"
                    />
                    <span>Pre-approve tool execution for this turn</span>
                  </label>
                </div>
                <div className="session-reply-actions">
                  <span className="source-summary">{selectedSessionDetail.session.workingDirectory}</span>
                  <button className="primary-button" onClick={onContinueSession} type="button">
                    <span>{isContinuingSession ? copy.sendingLabel : copy.sendLabel}</span>
                  </button>
                </div>
              </div>
            </>
          )}
        </article>
      </div>
    </section>
  )
}

function SessionTimelineEntry({
  entry,
  approvingEntryId,
  onApprovePendingTool,
}: {
  entry: DesktopSessionEntry
  approvingEntryId: string
  onApprovePendingTool: (entryId: string) => void
}) {
  const canApprove = entry.type === 'tool' && entry.status === 'approval-required' && !entry.resolutionStatus
  const isApproving = approvingEntryId === entry.id

  return (
    <article className={`session-entry ${entry.type}`}>
      <div className="source-topline">
        <strong>{entry.title}</strong>
        <div className="source-highlights">
          {entry.status && <span className="source-pill">{entry.status}</span>}
          {entry.approvalState && <span className="source-pill">{entry.approvalState}</span>}
          {entry.resolutionStatus && <span className="source-pill">{entry.resolutionStatus}</span>}
          {entry.exitCode !== null && entry.exitCode !== undefined && <span className="source-pill">exit {entry.exitCode}</span>}
          {entry.gitBranch && <span className="source-pill">{entry.gitBranch}</span>}
        </div>
      </div>
      <p className="source-summary">{entry.body || 'No text payload.'}</p>
      <div className="source-highlights">
        {entry.type && <span className="source-pill">{entry.type}</span>}
        {entry.toolName && <span className="source-pill">{entry.toolName}</span>}
        {entry.scope && <span className="source-pill">{entry.scope}</span>}
        {entry.timestamp && <span className="source-pill">{entry.timestamp}</span>}
        {entry.resolvedAt && <span className="source-pill">{entry.resolvedAt}</span>}
      </div>
      {(entry.arguments || entry.sourcePath || entry.changedFiles.length > 0 || entry.workingDirectory) && (
        <div className="session-entry-meta">
          {entry.arguments && <div className="session-entry-block"><strong>Args</strong><pre>{entry.arguments}</pre></div>}
          {entry.sourcePath && <div className="session-entry-block"><strong>Source</strong><pre>{entry.sourcePath}</pre></div>}
          {entry.changedFiles.length > 0 && <div className="session-entry-block"><strong>Changed files</strong><pre>{entry.changedFiles.join('\n')}</pre></div>}
          {entry.workingDirectory && <div className="session-entry-block"><strong>Working directory</strong><pre>{entry.workingDirectory}</pre></div>}
        </div>
      )}
      {canApprove && (
        <div className="session-entry-actions">
          <button
            className="primary-button compact"
            disabled={isApproving}
            onClick={() => onApprovePendingTool(entry.id)}
            type="button"
          >
            <span>{isApproving ? 'Approving...' : 'Approve and execute'}</span>
          </button>
        </div>
      )}
    </article>
  )
}

function LibraryView({
  title,
  subtitle,
  children,
}: {
  title: string
  subtitle: string
  children: ReactNode
}) {
  return (
    <section className="library-view">
      <div className="view-header inline">
        <div>
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
      </div>
      {children}
    </section>
  )
}

function DetailCard({ title, body }: { title: string; body: string }) {
  return (
    <article className="detail-card">
      <span>{title}</span>
      <p>{body}</p>
    </article>
  )
}

function LaneCard({ lane, copy }: { lane: CapabilityLane; copy: LocaleCopy }) {
  return (
    <article className="lane-card">
      <span>{copy.capabilityLanes}</span>
      <h3>{lane.title}</h3>
      <p>{lane.summary}</p>
      <div className="lane-responsibilities">
        <strong>{copy.responsibilities}</strong>
        {lane.responsibilities.map((item) => (
          <div className="lane-pill" key={item}>{item}</div>
        ))}
      </div>
    </article>
  )
}

function MirrorStatusCard({ mirror }: { mirror: SourceMirrorStatus }) {
  const toneClass = `mirror-status ${mirror.status}`

  return (
    <div className="source-item">
      <div className="source-topline">
        <span>{mirror.title}</span>
        <strong className={toneClass}>{mirror.status}</strong>
      </div>
      <strong>{mirror.path}</strong>
      <p className="source-summary">{mirror.summary}</p>
      <div className="source-highlights">
        {mirror.highlights.map((item) => (
          <span className="source-pill" key={item}>{item}</span>
        ))}
      </div>
    </div>
  )
}

function PortWorkItemCard({ item }: { item: RuntimePortWorkItem }) {
  const toneClass = `mirror-status ${item.stage}`

  return (
    <article className="artifact-card port-work-item">
      <div className="source-topline">
        <span>{item.sourceSystem}</span>
        <strong className={toneClass}>{item.stage}</strong>
      </div>
      <h2>{item.title}</h2>
      <p>{item.summary}</p>
      <div className="port-meta">
        <span className="source-pill">{item.targetModule}</span>
      </div>
      <p className="source-summary">{item.compatibilityContract}</p>
      <div className="source-highlights">
        {item.evidencePaths.map((path) => (
          <span className="source-pill" key={path}>{path}</span>
        ))}
      </div>
    </article>
  )
}

function ToolCard({ tool }: { tool: typeof fallbackBootstrap.qwenTools.tools[number] }) {
  const toneClass = `mirror-status ${tool.approvalState}`

  return (
    <article className="artifact-card port-work-item">
      <div className="source-topline">
        <span>{tool.kind}</span>
        <strong className={toneClass}>{tool.approvalState}</strong>
      </div>
      <h2>{tool.displayName}</h2>
      <p>{tool.approvalReason}</p>
      <div className="port-meta">
        <span className="source-pill">{tool.name}</span>
      </div>
      <p className="source-summary">{tool.sourcePath}</p>
    </article>
  )
}

function NativeHostToolCard({ tool }: { tool: typeof fallbackBootstrap.qwenNativeHost.tools[number] }) {
  const toneClass = `mirror-status ${tool.approvalState}`

  return (
    <article className="artifact-card port-work-item">
      <div className="source-topline">
        <span>{tool.kind}</span>
        <strong className={toneClass}>{tool.approvalState}</strong>
      </div>
      <h2>{tool.displayName}</h2>
      <p>{tool.approvalReason}</p>
      <div className="port-meta">
        <span className="source-pill">{tool.name}</span>
        <span className="source-pill">{tool.isImplemented ? 'implemented' : 'planned'}</span>
      </div>
    </article>
  )
}

export default App
