import { useRef, useState } from 'react'
import './App.css'
import { ChatView } from '@/components/chat/ChatView'
import { AppShell } from '@/components/layout/AppShell'
import { Sidebar } from '@/components/layout/Sidebar'
import { HomeScreen } from '@/components/screens/HomeScreen'
import { SettingsScreen } from '@/components/screens/SettingsScreen'
import { UtilitiesScreen } from '@/components/screens/UtilitiesScreen'
import { useAuth } from '@/hooks/useAuth'
import { useBootstrap } from '@/hooks/useBootstrap'
import { useMcp } from '@/hooks/useMcp'
import { useSession } from '@/hooks/useSession'
import { useUtilities } from '@/hooks/useUtilities'
import type { DesktopSessionDetail } from '@/types/desktop'
import type { AgentMode, WorkspaceSurface } from '@/types/ui'

function getProjectName(workingDirectory: string): string {
  return workingDirectory.split(/[\\/]/).filter(Boolean).at(-1) ?? workingDirectory
}

function App() {
  const [surface, setSurface] = useState<WorkspaceSurface>('sessions')
  const [mode, setMode] = useState<AgentMode>('auto-edit')

  const selectedSessionIdRef = useRef('')
  const selectedSessionDetailRef = useRef<DesktopSessionDetail | null>(null)

  const bootstrap = useBootstrap()
  const session = useSession({
    bootstrap: bootstrap.bootstrap,
    latestSessionEvent: bootstrap.latestSessionEvent,
    setBootstrap: bootstrap.setBootstrap,
    selectedSessionIdRef,
    selectedSessionDetailRef,
  })
  const auth = useAuth({ updateAuthSnapshot: bootstrap.updateAuthSnapshot })
  const mcp = useMcp({
    setBootstrap: bootstrap.setBootstrap,
    setMcpSnapshot: bootstrap.setMcpSnapshot,
  })
  const utilities = useUtilities(bootstrap.bootstrap)

  const handleNewChat = () => {
    session.handleStartNewChat()
    setSurface('sessions')
  }

  const handleSelectSession = (sessionId: string) => {
    session.handleSelectSession(sessionId)
    setSurface('sessions')
  }

  const workspaceRoot = bootstrap.bootstrap.workspaceRoot
  const projectName = getProjectName(
    bootstrap.bootstrap.recentSessions[0]?.workingDirectory || workspaceRoot,
  )
  const isSessionActive = Boolean(
    session.selectedSessionId && bootstrap.activeTurnSessions[session.selectedSessionId],
  )
  const streamingText = session.selectedSessionId
    ? (bootstrap.streamingSnapshots[session.selectedSessionId] ?? '')
    : ''

  const renderMain = () => {
    if (surface === 'settings') {
      return (
        <SettingsScreen
          authSnapshot={bootstrap.authSnapshot}
          isSavingAuth={auth.isSavingAuth}
          isStartingOAuthFlow={auth.isStartingOAuthFlow}
          isCancellingOAuthFlow={auth.isCancellingOAuthFlow}
          onConfigureQwenOAuth={auth.handleConfigureQwenOAuth}
          onConfigureOpenAi={auth.handleConfigureOpenAiCompatible}
          onConfigureCodingPlan={auth.handleConfigureCodingPlan}
          onDisconnect={auth.handleDisconnectAuth}
          onStartOAuthFlow={auth.handleStartQwenOAuthDeviceFlow}
          onCancelOAuthFlow={auth.handleCancelQwenOAuthDeviceFlow}
        />
      )
    }

    if (surface === 'utilities') {
      return (
        <UtilitiesScreen
          mcpServers={utilities.mcpServers}
          tools={utilities.tools}
          agents={utilities.agents}
          isSavingMcp={mcp.isSavingMcp}
          reconnectingMcpName={mcp.reconnectingMcpName}
          removingMcpName={mcp.removingMcpName}
          onReconnect={mcp.handleReconnectMcpServer}
          onRemove={mcp.handleRemoveMcpServer}
          onAddServer={mcp.handleAddMcpServer}
        />
      )
    }

    if (session.selectedSessionDetail) {
      return (
        <ChatView
          detail={session.selectedSessionDetail}
          streamingText={streamingText}
          isActive={isSessionActive}
          isLoadingSession={session.isLoadingSession}
          isSubmittingPrompt={session.isSubmittingPrompt}
          approvingEntryId={session.approvingEntryId}
          answeringEntryId={session.answeringEntryId}
          latestSessionEvent={bootstrap.latestSessionEvent}
          mode={mode}
          onModeChange={setMode}
          onCancel={session.handleCancelTurn}
          onSubmit={session.handleSubmitNewTurn}
          onApprove={session.handleApprovePendingTool}
          onAnswer={session.handleAnswerPendingQuestion}
          onLoadOlder={session.handleLoadOlderEntries}
          onLoadNewer={session.handleLoadNewerEntries}
        />
      )
    }

    if (session.selectedSessionId && session.isLoadingSession) {
      return (
        <div className="flex h-full items-center justify-center text-sm text-[--app-muted]">
          Loading session...
        </div>
      )
    }

    return <HomeScreen projectName={projectName} />
  }

  return (
    <AppShell
      sidebar={
        <Sidebar
          surface={surface}
          sessions={bootstrap.bootstrap.recentSessions}
          selectedSessionId={session.selectedSessionId}
          activeTurnSessions={bootstrap.activeTurnSessions}
          onNewChat={handleNewChat}
          onSelectSession={handleSelectSession}
          onOpenSettings={() => setSurface('settings')}
          onOpenUtilities={() => setSurface('utilities')}
          onUpdate={() => window.location.reload()}
        />
      }
      main={renderMain()}
    />
  )
}

export default App
