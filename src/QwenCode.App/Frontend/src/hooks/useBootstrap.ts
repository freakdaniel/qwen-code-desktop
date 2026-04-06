// Frontend/src/hooks/useBootstrap.ts
import { startTransition, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fallbackBootstrap } from '@/appData'
import type {
  ActiveTurnState,
  AppBootstrapPayload,
  AuthStatusSnapshot,
  DesktopSessionEvent,
  McpSnapshot,
} from '@/types/desktop'

export interface BootstrapState {
  bootstrap: AppBootstrapPayload
  authSnapshot: AuthStatusSnapshot
  mcpSnapshot: McpSnapshot
  activeTurnSessions: Record<string, true>
  streamingSnapshots: Record<string, string>
  reattachedSessionId: string
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
  setAuthSnapshot: React.Dispatch<React.SetStateAction<AuthStatusSnapshot>>
  setMcpSnapshot: React.Dispatch<React.SetStateAction<McpSnapshot>>
  latestSessionEvent: DesktopSessionEvent | null
  setLatestSessionEvent: React.Dispatch<React.SetStateAction<DesktopSessionEvent | null>>
  updateAuthSnapshot: (snapshot: AuthStatusSnapshot) => void
}

export function useBootstrap(): BootstrapState {
  const { i18n } = useTranslation()
  const [bootstrap, setBootstrap] = useState<AppBootstrapPayload>(fallbackBootstrap)
  const [authSnapshot, setAuthSnapshot] = useState<AuthStatusSnapshot>(fallbackBootstrap.qwenAuth)
  const [mcpSnapshot, setMcpSnapshot] = useState<McpSnapshot>(fallbackBootstrap.qwenMcp)
  const [activeTurnSessions, setActiveTurnSessions] = useState<Record<string, true>>({})
  const [streamingSnapshots, setStreamingSnapshots] = useState<Record<string, string>>({})
  const [reattachedSessionId, setReattachedSessionId] = useState('')
  const [latestSessionEvent, setLatestSessionEvent] = useState<DesktopSessionEvent | null>(null)
  const didHydrateRef = useRef(false)
  const selectedSessionIdRef = useRef('')

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
    })
  }

  useEffect(() => {
    if (didHydrateRef.current) return
    didHydrateRef.current = true
    const disposers: Array<() => void> = []

    const hydrate = async () => {
      if (!window.qwenDesktop) {
        // Use already-detected language from i18n init, not fallbackBootstrap
        return
      }

      const payload = await window.qwenDesktop.bootstrap()
      setBootstrap(payload)
      setAuthSnapshot(payload.qwenAuth)
      setMcpSnapshot(payload.qwenMcp)
      syncActiveTurns(payload.activeTurns)
      await i18n.changeLanguage(payload.currentLocale)

      disposers.push(
        window.qwenDesktop.subscribeStateChanged((event) => {
          setBootstrap((c) => ({ ...c, currentLocale: event.currentLocale }))
          startTransition(() => { void i18n.changeLanguage(event.currentLocale) })
        }),
      )

      disposers.push(
        window.qwenDesktop.subscribeAuthChanged((snapshot) => {
          updateAuthSnapshot(snapshot)
        }),
      )

      disposers.push(
        window.qwenDesktop.subscribeSessionEvents((event) => {
          setLatestSessionEvent(event)

          setActiveTurnSessions((current) => {
            if (event.kind === 'turnStarted') {
              setReattachedSessionId('')
              return { ...current, [event.sessionId]: true }
            }
            if (event.kind === 'turnCompleted' || event.kind === 'turnCancelled') {
              setReattachedSessionId((r) => r === event.sessionId ? '' : r)
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
        }),
      )
    }

    void hydrate()
    return () => disposers.forEach((d) => d())
  }, [i18n])

  useEffect(() => {
    if (!window.qwenDesktop) return
    let disposed = false

    const resync = async () => {
      const turns = await window.qwenDesktop?.getActiveTurns()
      if (!disposed && turns) syncActiveTurns(turns)
    }

    const onVisibility = () => { if (document.visibilityState === 'visible') void resync() }

    window.addEventListener('focus', resync)
    document.addEventListener('visibilitychange', onVisibility)

    return () => {
      disposed = true
      window.removeEventListener('focus', resync)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [])

  useEffect(() => {
    document.documentElement.dir = i18n.language === 'ar' ? 'rtl' : 'ltr'
  }, [i18n.language])

  return {
    bootstrap,
    authSnapshot,
    mcpSnapshot,
    activeTurnSessions,
    streamingSnapshots,
    reattachedSessionId,
    latestSessionEvent,
    setBootstrap,
    setAuthSnapshot,
    setMcpSnapshot,
    setLatestSessionEvent,
    updateAuthSnapshot,
  }
}
