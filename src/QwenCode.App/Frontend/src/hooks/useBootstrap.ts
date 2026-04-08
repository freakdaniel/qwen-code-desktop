import {
  createElement,
  createContext,
  startTransition,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { changeLanguage } from '@/i18n'
import { fallbackBootstrap } from '@/appData'
import type {
  ActiveTurnState,
  AppBootstrapPayload,
  AuthStatusSnapshot,
  DesktopSessionDetail,
  DesktopSessionEvent,
  McpSnapshot,
  SessionPreview,
} from '@/types/desktop'

export interface BootstrapState {
  bootstrap: AppBootstrapPayload
  authSnapshot: AuthStatusSnapshot
  mcpSnapshot: McpSnapshot
  activeTurnSessions: Record<string, true>
  streamingSnapshots: Record<string, string>
  reattachedSessionId: string
  isReady: boolean
  sessionCache: Record<string, DesktopSessionDetail>
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
  setSessionCache: React.Dispatch<React.SetStateAction<Record<string, DesktopSessionDetail>>>
  setAuthSnapshot: React.Dispatch<React.SetStateAction<AuthStatusSnapshot>>
  setMcpSnapshot: React.Dispatch<React.SetStateAction<McpSnapshot>>
  latestSessionEvent: DesktopSessionEvent | null
  setLatestSessionEvent: React.Dispatch<React.SetStateAction<DesktopSessionEvent | null>>
  updateAuthSnapshot: (snapshot: AuthStatusSnapshot) => void
  loadSessionDetail: (
    sessionId: string,
    options?: { force?: boolean; limit?: number },
  ) => Promise<DesktopSessionDetail | null>
}

const BootstrapContext = createContext<BootstrapState | null>(null)

function useBootstrapState(): BootstrapState {
  const [bootstrap, setBootstrap] = useState<AppBootstrapPayload>(fallbackBootstrap)
  const [authSnapshot, setAuthSnapshot] = useState<AuthStatusSnapshot>(fallbackBootstrap.qwenAuth)
  const [mcpSnapshot, setMcpSnapshot] = useState<McpSnapshot>(fallbackBootstrap.qwenMcp)
  const [activeTurnSessions, setActiveTurnSessions] = useState<Record<string, true>>({})
  const [streamingSnapshots, setStreamingSnapshots] = useState<Record<string, string>>({})
  const [reattachedSessionId, setReattachedSessionId] = useState('')
  const [latestSessionEvent, setLatestSessionEvent] = useState<DesktopSessionEvent | null>(null)
  const [isReady, setIsReady] = useState(false)
  const [sessionCache, setSessionCache] = useState<Record<string, DesktopSessionDetail>>({})
  const didHydrateRef = useRef(false)
  const selectedSessionIdRef = useRef('')
  const sessionCacheRef = useRef<Record<string, DesktopSessionDetail>>({})
  const inflightSessionLoadsRef = useRef<Record<string, Promise<DesktopSessionDetail | null>>>({})

  useEffect(() => {
    sessionCacheRef.current = sessionCache
  }, [sessionCache])

  const updateAuthSnapshot = (snapshot: AuthStatusSnapshot) => {
    setAuthSnapshot(snapshot)
    setBootstrap((current) => ({ ...current, qwenAuth: snapshot }))
  }

  const syncActiveTurns = (turns: ActiveTurnState[], preferredSessionId = '') => {
    setActiveTurnSessions(Object.fromEntries(turns.map((t) => [t.sessionId, true] as const)))
    setStreamingSnapshots(
      Object.fromEntries(
        turns.filter((t) => t.contentSnapshot).map((t) => [t.sessionId, t.contentSnapshot] as const),
      ),
    )

    if (turns.length === 0) {
      setReattachedSessionId('')
      return
    }

    const sorted = [...turns].sort(
      (a, b) => Date.parse(b.lastUpdatedAtUtc) - Date.parse(a.lastUpdatedAtUtc),
    )
    const targetId = preferredSessionId || selectedSessionIdRef.current || sorted[0]?.sessionId || ''
    if (!targetId) return

    const activeTurn = sorted.find((t) => t.sessionId === targetId)
    if (!activeTurn) return

    setReattachedSessionId(activeTurn.sessionId)
    setLatestSessionEvent({
      sessionId: activeTurn.sessionId,
      kind: 'turnReattached',
      timestampUtc: activeTurn.lastUpdatedAtUtc,
      message: activeTurn.contentSnapshot || `Reattached at ${activeTurn.stage}.`,
      agentName: '',
      workingDirectory: activeTurn.workingDirectory,
      gitBranch: activeTurn.gitBranch,
      commandName: '',
      toolName: activeTurn.toolName,
      status: activeTurn.status,
      contentDelta: '',
      contentSnapshot: activeTurn.contentSnapshot,
      title: '',
    })
  }

  const loadSessionDetail = useCallback(
    async (
      sessionId: string,
      options?: { force?: boolean; limit?: number },
    ): Promise<DesktopSessionDetail | null> => {
      if (!window.qwenDesktop || !sessionId) {
        return null
      }

      const force = options?.force ?? false
      const limit = options?.limit ?? 200

      if (!force && sessionCacheRef.current[sessionId]) {
        return sessionCacheRef.current[sessionId]
      }

      if (!force && inflightSessionLoadsRef.current[sessionId] !== undefined) {
        return inflightSessionLoadsRef.current[sessionId]
      }

      const request = window.qwenDesktop
        .getSession({
          sessionId,
          offset: null,
          limit,
        })
        .then((detail) => {
          if (detail) {
            setSessionCache((current) => ({
              ...current,
              [sessionId]: detail,
            }))
          }

          return detail ?? null
        })
        .finally(() => {
          delete inflightSessionLoadsRef.current[sessionId]
        })

      inflightSessionLoadsRef.current[sessionId] = request
      return request
    },
    [],
  )

  const preloadRecentSessions = useCallback(
    async (sessions: SessionPreview[]) => {
      if (sessions.length === 0) return

      await Promise.allSettled(
        sessions.map((session) =>
          loadSessionDetail(session.sessionId, { limit: 120 }),
        ),
      )
    },
    [loadSessionDetail],
  )

  useEffect(() => {
    if (didHydrateRef.current) return
    didHydrateRef.current = true
    const disposers: Array<() => void> = []

    const hydrate = async () => {
      if (!window.qwenDesktop) {
        setIsReady(true)
        return
      }

      const payload = await window.qwenDesktop.bootstrap()
      const normalizedPayload: AppBootstrapPayload = {
        ...payload,
        qwenAuth: payload.qwenAuth,
        qwenModels:
          'qwenModels' in payload && payload.qwenModels
            ? (payload.qwenModels as AppBootstrapPayload['qwenModels'])
            : fallbackBootstrap.qwenModels,
      }

      setBootstrap(normalizedPayload)
      setAuthSnapshot(normalizedPayload.qwenAuth)
      setMcpSnapshot(normalizedPayload.qwenMcp)
      syncActiveTurns(normalizedPayload.activeTurns)
      await changeLanguage(normalizedPayload.currentLocale)
      await preloadRecentSessions(normalizedPayload.recentSessions)

      disposers.push(
        window.qwenDesktop.subscribeStateChanged((event) => {
          setBootstrap((c) => ({ ...c, currentLocale: event.currentLocale }))
          startTransition(() => {
            void changeLanguage(event.currentLocale)
          })
        }),
      )

      disposers.push(
        window.qwenDesktop.subscribeAuthChanged((snapshot) => {
          updateAuthSnapshot(snapshot)
        }),
      )

      disposers.push(
        window.qwenDesktop.subscribeSessionEvents((event) => {
          if (event.kind === 'sessionTitleUpdated' && event.title) {
            setBootstrap((current) => ({
              ...current,
              recentSessions: current.recentSessions.map((s) =>
                s.sessionId === event.sessionId
                  ? { ...s, title: event.title }
                  : s,
              ),
            }))
            return
          }

          setLatestSessionEvent(event)

          setActiveTurnSessions((current) => {
            if (event.kind === 'turnStarted') {
              setReattachedSessionId('')
              return { ...current, [event.sessionId]: true }
            }
            if (event.kind === 'turnCompleted' || event.kind === 'turnCancelled') {
              setReattachedSessionId((r) => (r === event.sessionId ? '' : r))
              if (!(event.sessionId in current)) return current
              const next = { ...current }
              delete next[event.sessionId]
              return next
            }
            return current
          })

          setStreamingSnapshots((current) => {
            if (
              (event.kind === 'turnReattached' || event.kind === 'assistantStreaming') &&
              event.contentSnapshot
            ) {
              return { ...current, [event.sessionId]: event.contentSnapshot }
            }
            if (
              event.kind === 'assistantCompleted' ||
              event.kind === 'turnCompleted' ||
              event.kind === 'turnCancelled'
            ) {
              if (!(event.sessionId in current)) return current
              const next = { ...current }
              delete next[event.sessionId]
              return next
            }
            return current
          })

          if (
            event.kind === 'assistantCompleted' ||
            event.kind === 'turnCompleted' ||
            event.kind === 'turnCancelled' ||
            event.kind === 'turnReattached'
          ) {
            void loadSessionDetail(event.sessionId, { force: true, limit: 200 })
          }
        }),
      )

      setIsReady(true)
    }

    void hydrate()
    return () => disposers.forEach((d) => d())
  }, [loadSessionDetail, preloadRecentSessions])

  useEffect(() => {
    if (!window.qwenDesktop) return
    let disposed = false

    const resync = async () => {
      const turns = await window.qwenDesktop?.getActiveTurns()
      if (!disposed && turns) syncActiveTurns(turns)
    }

    const onVisibility = () => {
      if (document.visibilityState === 'visible') void resync()
    }

    window.addEventListener('focus', resync)
    document.addEventListener('visibilitychange', onVisibility)

    return () => {
      disposed = true
      window.removeEventListener('focus', resync)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [])

  useEffect(() => {
    document.documentElement.dir = bootstrap?.currentLocale === 'ar' ? 'rtl' : 'ltr'
  }, [bootstrap?.currentLocale])

  return useMemo(
    () => ({
      bootstrap,
      authSnapshot,
      mcpSnapshot,
      activeTurnSessions,
      streamingSnapshots,
      reattachedSessionId,
      isReady,
      sessionCache,
      latestSessionEvent,
      setBootstrap,
      setSessionCache,
      setAuthSnapshot,
      setMcpSnapshot,
      setLatestSessionEvent,
      updateAuthSnapshot,
      loadSessionDetail,
    }),
    [
      activeTurnSessions,
      authSnapshot,
      bootstrap,
      isReady,
      latestSessionEvent,
      loadSessionDetail,
      mcpSnapshot,
      reattachedSessionId,
      sessionCache,
      streamingSnapshots,
      setSessionCache,
    ],
  )
}

export function BootstrapProvider({ children }: { children: ReactNode }) {
  const state = useBootstrapState()
  return createElement(BootstrapContext.Provider, { value: state }, children)
}

export function useBootstrap(): BootstrapState {
  const context = useContext(BootstrapContext)

  if (!context) {
    throw new Error('useBootstrap must be used within BootstrapProvider')
  }

  return context
}
