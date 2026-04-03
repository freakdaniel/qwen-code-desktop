import { useState } from 'react'
import { Circle, Loader2, Plus, RefreshCw, Trash2, Wrench } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import type {
  ChannelDefinition,
  ChannelPairingSnapshot,
  ChannelSnapshot,
  ExtensionDefinition,
  ExtensionSettingsSnapshot,
  McpServerDefinition,
  NativeToolRegistration,
  QwenSkillSurface,
  WorkspaceSnapshot,
} from '@/types/desktop'

interface UtilitiesScreenProps {
  workspaceSnapshot: WorkspaceSnapshot
  mcpServers: McpServerDefinition[]
  channels: ChannelDefinition[]
  channelSnapshot: ChannelSnapshot
  extensions: ExtensionDefinition[]
  tools: NativeToolRegistration[]
  agents: QwenSkillSurface[]
  isSavingMcp: boolean
  isInstallingExtension: boolean
  loadingPairingsName: string
  approvingPairingKey: string
  reconnectingMcpName: string
  removingMcpName: string
  togglingExtensionName: string
  removingExtensionName: string
  loadingSettingsName: string
  savingSettingKey: string
  isCreatingManagedWorktree: boolean
  cleaningManagedSessionId: string
  pairingsByChannel: Record<string, ChannelPairingSnapshot>
  settingsByExtension: Record<string, ExtensionSettingsSnapshot>
  onCreateManagedWorktree: (request: {
    sessionId: string
    name: string
    baseBranch?: string
  }) => Promise<void> | void
  onCleanupManagedSession: (sessionId: string) => Promise<void> | void
  onLoadChannelPairings: (name: string) => Promise<void> | void
  onApproveChannelPairing: (name: string, code: string) => Promise<void> | void
  onReconnect: (name: string) => Promise<void> | void
  onRemove: (name: string, scope: string) => Promise<void> | void
  onAddServer: (request: {
    name: string
    scope: 'user' | 'project'
    transport: 'stdio' | 'http' | 'sse'
    commandOrUrl: string
    description: string
  }) => Promise<void> | void
  onInstallExtension: (request: {
    sourcePath: string
    installMode: 'link' | 'copy'
  }) => Promise<void> | void
  onSetExtensionEnabled: (request: {
    name: string
    scope: 'user' | 'project'
    enabled: boolean
  }) => Promise<void> | void
  onRemoveExtension: (name: string) => Promise<void> | void
  onLoadExtensionSettings: (name: string) => Promise<void> | void
  onSetExtensionSetting: (request: {
    name: string
    setting: string
    scope: 'user' | 'project'
    value: string
  }) => Promise<void> | void
}

interface McpFormState {
  name: string
  scope: 'user' | 'project'
  transport: 'stdio' | 'http' | 'sse'
  commandOrUrl: string
  description: string
}

const DEFAULT_FORM: McpFormState = {
  name: '',
  scope: 'project',
  transport: 'stdio',
  commandOrUrl: '',
  description: '',
}

interface ExtensionFormState {
  sourcePath: string
  installMode: 'link' | 'copy'
}

interface WorktreeFormState {
  sessionId: string
  name: string
  baseBranch: string
}

const DEFAULT_EXTENSION_FORM: ExtensionFormState = {
  sourcePath: '',
  installMode: 'link',
}

const DEFAULT_WORKTREE_FORM: WorktreeFormState = {
  sessionId: '',
  name: '',
  baseBranch: '',
}

function statusTone(status: string) {
  if (status === 'connected') return 'text-emerald-400'
  if (status === 'disconnected') return 'text-orange-400'
  return 'text-[--app-muted]'
}

function approvalTone(value: string) {
  if (value === 'allow') return 'border-emerald-500/40 text-emerald-400'
  if (value === 'ask') return 'border-orange-500/40 text-orange-400'
  return 'border-red-500/40 text-red-400'
}

function extensionStatusTone(value: string) {
  if (value === 'active') return 'border-emerald-500/40 text-emerald-400'
  if (value === 'disabled') return 'border-orange-500/40 text-orange-400'
  if (value === 'workspace-untrusted') return 'border-sky-500/40 text-sky-400'
  return 'border-red-500/40 text-red-400'
}

function channelStatusTone(value: string) {
  if (value === 'running') return 'border-emerald-500/40 text-emerald-400'
  if (value === 'configured') return 'border-sky-500/40 text-sky-400'
  return 'border-orange-500/40 text-orange-400'
}

export function UtilitiesScreen({
  workspaceSnapshot,
  mcpServers,
  channels,
  channelSnapshot,
  extensions,
  tools,
  agents,
  isSavingMcp,
  isInstallingExtension,
  loadingPairingsName,
  approvingPairingKey,
  reconnectingMcpName,
  removingMcpName,
  togglingExtensionName,
  removingExtensionName,
  loadingSettingsName,
  savingSettingKey,
  isCreatingManagedWorktree,
  cleaningManagedSessionId,
  pairingsByChannel,
  settingsByExtension,
  onCreateManagedWorktree,
  onCleanupManagedSession,
  onLoadChannelPairings,
  onApproveChannelPairing,
  onReconnect,
  onRemove,
  onAddServer,
  onInstallExtension,
  onSetExtensionEnabled,
  onRemoveExtension,
  onLoadExtensionSettings,
  onSetExtensionSetting,
}: UtilitiesScreenProps) {
  const [showAddForm, setShowAddForm] = useState(false)
  const [form, setForm] = useState<McpFormState>(DEFAULT_FORM)
  const [showAddExtensionForm, setShowAddExtensionForm] = useState(false)
  const [extensionForm, setExtensionForm] = useState<ExtensionFormState>(DEFAULT_EXTENSION_FORM)
  const [showWorktreeForm, setShowWorktreeForm] = useState(false)
  const [worktreeForm, setWorktreeForm] = useState<WorktreeFormState>(DEFAULT_WORKTREE_FORM)
  const [settingDrafts, setSettingDrafts] = useState<Record<string, string>>({})

  const connectedCount = mcpServers.filter((server) => server.status === 'connected').length
  const activeExtensionCount = extensions.filter((extension) => extension.isActive).length
  const runningChannelCount = channels.filter((channel) => channel.status === 'running').length
  const managedWorktreeCount = workspaceSnapshot.git.worktrees.filter((item) => item.isManaged).length
  const managedSessionIds = Array.from(
    new Set(
      workspaceSnapshot.git.worktrees
        .filter((item) => item.isManaged && item.sessionId)
        .map((item) => item.sessionId),
    ),
  ).sort((left, right) => left.localeCompare(right))

  const handleSubmit = async () => {
    if (!form.name.trim() || !form.commandOrUrl.trim() || isSavingMcp) {
      return
    }

    await onAddServer({
      name: form.name.trim(),
      scope: form.scope,
      transport: form.transport,
      commandOrUrl: form.commandOrUrl.trim(),
      description: form.description.trim(),
    })

    setForm(DEFAULT_FORM)
    setShowAddForm(false)
  }

  const handleInstallSubmit = async () => {
    if (!extensionForm.sourcePath.trim() || isInstallingExtension) {
      return
    }

    await onInstallExtension({
      sourcePath: extensionForm.sourcePath.trim(),
      installMode: extensionForm.installMode,
    })

    setExtensionForm(DEFAULT_EXTENSION_FORM)
    setShowAddExtensionForm(false)
  }

  const handleCreateWorktreeSubmit = async () => {
    if (!worktreeForm.sessionId.trim() || !worktreeForm.name.trim() || isCreatingManagedWorktree) {
      return
    }

    await onCreateManagedWorktree({
      sessionId: worktreeForm.sessionId.trim(),
      name: worktreeForm.name.trim(),
      baseBranch: worktreeForm.baseBranch.trim(),
    })

    setWorktreeForm(DEFAULT_WORKTREE_FORM)
    setShowWorktreeForm(false)
  }

  const getDraftValue = (
    extensionName: string,
    environmentVariable: string,
    scope: 'user' | 'project',
    fallbackValue: string,
  ) => {
    const key = `${extensionName}:${environmentVariable}:${scope}`
    return settingDrafts[key] ?? fallbackValue
  }

  const setDraftValue = (
    extensionName: string,
    environmentVariable: string,
    scope: 'user' | 'project',
    value: string,
  ) => {
    const key = `${extensionName}:${environmentVariable}:${scope}`
    setSettingDrafts((current) => ({ ...current, [key]: value }))
  }

  const getSettingSaveKey = (
    extensionName: string,
    environmentVariable: string,
    scope: 'user' | 'project',
  ) => `${extensionName}:${environmentVariable}:${scope}`

  return (
    <div className="h-full overflow-y-auto">
      <div className="mx-auto flex max-w-5xl flex-col gap-6 px-6 py-8">
        <div>
          <h1 className="text-xl font-semibold text-[--app-text]">Utilities</h1>
          <p className="mt-1 text-sm text-[--app-muted]">
            MCP servers, native tools and reusable agents exposed by the desktop runtime.
          </p>
        </div>

        <div className="grid gap-3 md:grid-cols-6">
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>MCP servers</CardTitle>
              <CardDescription>{connectedCount} connected</CardDescription>
            </CardHeader>
          </Card>
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>Channels</CardTitle>
              <CardDescription>{runningChannelCount} running</CardDescription>
            </CardHeader>
          </Card>
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>Agents</CardTitle>
              <CardDescription>{agents.length} available skills</CardDescription>
            </CardHeader>
          </Card>
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>Extensions</CardTitle>
              <CardDescription>{activeExtensionCount} active in workspace</CardDescription>
            </CardHeader>
          </Card>
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>Tools</CardTitle>
              <CardDescription>{tools.length} native registrations</CardDescription>
            </CardHeader>
          </Card>
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>Workspace</CardTitle>
              <CardDescription>{workspaceSnapshot.discovery.visibleFileCount} visible files</CardDescription>
            </CardHeader>
          </Card>
        </div>

        <Tabs defaultValue="workspace">
          <TabsList className="border border-[--app-border] bg-[--app-elevated]">
            <TabsTrigger value="workspace">Workspace</TabsTrigger>
            <TabsTrigger value="mcp">MCP</TabsTrigger>
            <TabsTrigger value="channels">Channels</TabsTrigger>
            <TabsTrigger value="extensions">Extensions</TabsTrigger>
            <TabsTrigger value="agents">Agents</TabsTrigger>
            <TabsTrigger value="tools">Tools</TabsTrigger>
          </TabsList>

          <TabsContent value="workspace" className="mt-4 flex flex-col gap-4">
            <Card className="border-[--app-border] bg-[--app-panel]">
              <CardHeader>
                <CardTitle>Repository and worktrees</CardTitle>
                <CardDescription>
                  {workspaceSnapshot.git.isRepository
                    ? `Branch ${workspaceSnapshot.git.currentBranch || 'detached'} at ${workspaceSnapshot.git.currentCommit.slice(0, 8) || 'unknown'}`
                    : workspaceSnapshot.git.isGitAvailable
                      ? 'Workspace is not inside a git repository'
                      : 'Git is not available in the desktop runtime'}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-2 md:grid-cols-4">
                <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                  <p className="text-xs text-[--app-muted]">Git status</p>
                  <p className="mt-1 text-[--app-text]">
                    {workspaceSnapshot.git.isGitAvailable
                      ? workspaceSnapshot.git.isRepository
                        ? 'repository detected'
                        : 'git available'
                      : 'git unavailable'}
                  </p>
                </div>
                <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                  <p className="text-xs text-[--app-muted]">Known worktrees</p>
                  <p className="mt-1 text-[--app-text]">{workspaceSnapshot.git.worktrees.length}</p>
                </div>
                <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                  <p className="text-xs text-[--app-muted]">Managed sessions</p>
                  <p className="mt-1 text-[--app-text]">{workspaceSnapshot.git.managedSessionCount}</p>
                </div>
                <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                  <p className="text-xs text-[--app-muted]">Managed worktrees</p>
                  <p className="mt-1 text-[--app-text]">{managedWorktreeCount}</p>
                </div>
              </CardContent>
            </Card>

            <div className="flex flex-wrap gap-2">
              <Button
                className="bg-orange-500 text-white hover:bg-orange-600"
                onClick={() => setShowWorktreeForm((value) => !value)}
              >
                <Plus size={14} className="mr-1.5" />
                {showWorktreeForm ? 'Close worktree form' : 'Create managed worktree'}
              </Button>
            </div>

            {showWorktreeForm && (
              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <CardTitle>Create managed worktree</CardTitle>
                  <CardDescription>
                    Creates a qwen-managed worktree under <code>.qwen/worktrees/&lt;session&gt;/worktrees</code>.
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-3 md:grid-cols-3">
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Session id</span>
                    <Input
                      value={worktreeForm.sessionId}
                      onChange={(event) =>
                        setWorktreeForm((current) => ({ ...current, sessionId: event.target.value }))
                      }
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Worktree name</span>
                    <Input
                      value={worktreeForm.name}
                      onChange={(event) =>
                        setWorktreeForm((current) => ({ ...current, name: event.target.value }))
                      }
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Base branch</span>
                    <Input
                      value={worktreeForm.baseBranch}
                      onChange={(event) =>
                        setWorktreeForm((current) => ({ ...current, baseBranch: event.target.value }))
                      }
                      placeholder={workspaceSnapshot.git.currentBranch || 'current branch'}
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <div className="md:col-span-3">
                    <Button
                      className="bg-orange-500 text-white hover:bg-orange-600"
                      disabled={
                        isCreatingManagedWorktree ||
                        !worktreeForm.sessionId.trim() ||
                        !worktreeForm.name.trim()
                      }
                      onClick={handleCreateWorktreeSubmit}
                    >
                      {isCreatingManagedWorktree && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                      Create worktree
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}

            <div className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <CardTitle>Worktree inventory</CardTitle>
                  <CardDescription>
                    Managed qwen worktrees are recognized by the standard <code>.qwen/worktrees</code> layout.
                  </CardDescription>
                </CardHeader>
                <CardContent className="flex flex-col gap-3">
                  {workspaceSnapshot.git.worktrees.length === 0 && (
                    <div className="rounded-lg border border-dashed border-[--app-border] bg-[--app-bg] p-4 text-sm text-[--app-muted]">
                      No worktrees discovered for this repository yet.
                    </div>
                  )}

                  {workspaceSnapshot.git.worktrees.map((worktree) => (
                    <div
                      key={worktree.path}
                      className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm"
                    >
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium text-[--app-text]">{worktree.name}</span>
                        <Badge variant="outline" className="border-[--app-border] text-[--app-muted]">
                          {worktree.branch || 'detached'}
                        </Badge>
                        {worktree.isCurrent && (
                          <Badge variant="outline" className="border-sky-500/40 text-sky-400">
                            current
                          </Badge>
                        )}
                        {worktree.isManaged && (
                          <Badge variant="outline" className="border-emerald-500/40 text-emerald-400">
                            managed
                          </Badge>
                        )}
                      </div>
                      <p className="mt-2 break-all text-xs text-[--app-muted]">{worktree.path}</p>
                      {worktree.sessionId && (
                        <p className="mt-1 text-xs text-[--app-muted]">Session {worktree.sessionId}</p>
                      )}
                    </div>
                  ))}
                </CardContent>
              </Card>

              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <CardTitle>File discovery</CardTitle>
                  <CardDescription>
                    Read-only snapshot of the current discoverable workspace after git and qwen ignore rules.
                  </CardDescription>
                </CardHeader>
                <CardContent className="flex flex-col gap-3">
                  <div className="grid gap-2 md:grid-cols-2">
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Candidate files</p>
                      <p className="mt-1 text-[--app-text]">{workspaceSnapshot.discovery.candidateFileCount}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Visible files</p>
                      <p className="mt-1 text-[--app-text]">{workspaceSnapshot.discovery.visibleFileCount}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Git ignored</p>
                      <p className="mt-1 text-[--app-text]">{workspaceSnapshot.discovery.gitIgnoredCount}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Qwen ignored</p>
                      <p className="mt-1 text-[--app-text]">{workspaceSnapshot.discovery.qwenIgnoredCount}</p>
                    </div>
                  </div>

                  <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                    <p className="text-xs text-[--app-muted]">Context files</p>
                    <p className="mt-1 text-[--app-text]">
                      {workspaceSnapshot.discovery.contextFiles.length > 0
                        ? workspaceSnapshot.discovery.contextFiles.join(', ')
                        : 'No context files discovered'}
                    </p>
                  </div>

                  <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                    <p className="text-xs text-[--app-muted]">Visible sample</p>
                    <p className="mt-1 break-all text-[--app-text]">
                      {workspaceSnapshot.discovery.sampleVisibleFiles.length > 0
                        ? workspaceSnapshot.discovery.sampleVisibleFiles.join(', ')
                        : 'Nothing discovered yet'}
                    </p>
                  </div>

                  {workspaceSnapshot.discovery.sampleQwenIgnoredFiles.length > 0 && (
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Qwen ignored sample</p>
                      <p className="mt-1 break-all text-[--app-text]">
                        {workspaceSnapshot.discovery.sampleQwenIgnoredFiles.join(', ')}
                      </p>
                    </div>
                  )}
                </CardContent>
              </Card>
            </div>

            <Card className="border-[--app-border] bg-[--app-panel]">
              <CardHeader>
                <CardTitle>Managed sessions</CardTitle>
                <CardDescription>
                  Cleanup removes all managed worktrees for the selected qwen session and prunes git references.
                </CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                {managedSessionIds.length === 0 && (
                  <div className="rounded-lg border border-dashed border-[--app-border] bg-[--app-bg] p-4 text-sm text-[--app-muted]">
                    No managed sessions found under the qwen worktree layout.
                  </div>
                )}

                {managedSessionIds.map((sessionId) => (
                  <div
                    key={sessionId}
                    className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm"
                  >
                    <div>
                      <p className="font-medium text-[--app-text]">{sessionId}</p>
                      <p className="mt-1 text-xs text-[--app-muted]">
                        {
                          workspaceSnapshot.git.worktrees.filter((item) => item.sessionId === sessionId).length
                        } managed worktrees
                      </p>
                    </div>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-red-400 hover:bg-red-500/10 hover:text-red-300"
                      disabled={Boolean(cleaningManagedSessionId)}
                      onClick={() => onCleanupManagedSession(sessionId)}
                    >
                      {cleaningManagedSessionId === sessionId
                        ? <Loader2 size={14} className="animate-spin" />
                        : 'Cleanup session'}
                    </Button>
                  </div>
                ))}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="mcp" className="mt-4 flex flex-col gap-4">
            <div className="flex flex-wrap gap-2">
              <Button
                className="bg-orange-500 text-white hover:bg-orange-600"
                onClick={() => setShowAddForm((value) => !value)}
              >
                <Plus size={14} className="mr-1.5" />
                {showAddForm ? 'Close add form' : 'Add server'}
              </Button>
            </div>

            {showAddForm && (
              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <CardTitle>Add MCP server</CardTitle>
                  <CardDescription>Inline setup for the most common server fields.</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-3 md:grid-cols-2">
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Name</span>
                    <Input
                      value={form.name}
                      onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Command or URL</span>
                    <Input
                      value={form.commandOrUrl}
                      onChange={(event) =>
                        setForm((current) => ({ ...current, commandOrUrl: event.target.value }))
                      }
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Scope</span>
                    <select
                      value={form.scope}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          scope: event.target.value === 'user' ? 'user' : 'project',
                        }))
                      }
                      className="h-9 rounded-md border border-[--app-border] bg-[--app-bg] px-3 text-sm text-[--app-text]"
                    >
                      <option value="project">project</option>
                      <option value="user">user</option>
                    </select>
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Transport</span>
                    <select
                      value={form.transport}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          transport: event.target.value as McpFormState['transport'],
                        }))
                      }
                      className="h-9 rounded-md border border-[--app-border] bg-[--app-bg] px-3 text-sm text-[--app-text]"
                    >
                      <option value="stdio">stdio</option>
                      <option value="http">http</option>
                      <option value="sse">sse</option>
                    </select>
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted] md:col-span-2">
                    <span>Description</span>
                    <Input
                      value={form.description}
                      onChange={(event) =>
                        setForm((current) => ({ ...current, description: event.target.value }))
                      }
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <div className="md:col-span-2">
                    <Button
                      className="bg-orange-500 text-white hover:bg-orange-600"
                      disabled={isSavingMcp || !form.name.trim() || !form.commandOrUrl.trim()}
                      onClick={handleSubmit}
                    >
                      {isSavingMcp && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                      Save server
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}

            <div className="flex flex-col gap-3">
              {mcpServers.length === 0 && (
                <Card className="border-[--app-border] bg-[--app-panel]">
                  <CardContent className="py-6 text-sm text-[--app-muted]">
                    No MCP servers configured yet.
                  </CardContent>
                </Card>
              )}

              {mcpServers.map((server) => (
                <Card key={server.name} className="border-[--app-border] bg-[--app-panel]">
                  <CardHeader className="gap-3">
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex items-center gap-2">
                        <Circle size={10} className={`fill-current ${statusTone(server.status)}`} />
                        <div>
                          <CardTitle className="text-sm">{server.name}</CardTitle>
                          <CardDescription>{server.description || server.commandOrUrl}</CardDescription>
                        </div>
                      </div>
                      <div className="flex items-center gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                          disabled={Boolean(reconnectingMcpName)}
                          onClick={() => onReconnect(server.name)}
                        >
                          {reconnectingMcpName === server.name
                            ? <Loader2 size={14} className="animate-spin" />
                            : <RefreshCw size={14} />
                          }
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-[--app-muted] hover:bg-red-500/10 hover:text-red-400"
                          disabled={Boolean(removingMcpName)}
                          onClick={() => onRemove(server.name, server.scope)}
                        >
                          {removingMcpName === server.name
                            ? <Loader2 size={14} className="animate-spin" />
                            : <Trash2 size={14} />
                          }
                        </Button>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="grid gap-2 md:grid-cols-2">
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Scope</p>
                      <p className="mt-1 text-[--app-text]">{server.scope}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Transport</p>
                      <p className="mt-1 text-[--app-text]">{server.transport}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm md:col-span-2">
                      <p className="text-xs text-[--app-muted]">Command or URL</p>
                      <p className="mt-1 break-all text-[--app-text]">{server.commandOrUrl}</p>
                    </div>
                    {server.lastError && (
                      <div className="rounded-lg border border-orange-500/30 bg-orange-500/10 p-3 text-sm text-orange-200 md:col-span-2">
                        {server.lastError}
                      </div>
                    )}
                  </CardContent>
                </Card>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="channels" className="mt-4 flex flex-col gap-4">
            <Card className="border-[--app-border] bg-[--app-panel]">
              <CardHeader>
                <CardTitle>Channel service</CardTitle>
                <CardDescription>
                  {channelSnapshot.isServiceRunning
                    ? `Running as PID ${channelSnapshot.serviceProcessId} for ${channelSnapshot.serviceUptimeText || 'a short while'}`
                    : 'No shared channel service is running right now'}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-2 md:grid-cols-2">
                <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                  <p className="text-xs text-[--app-muted]">Supported channel types</p>
                  <p className="mt-1 text-[--app-text]">
                    {channelSnapshot.supportedTypes.length > 0
                      ? channelSnapshot.supportedTypes.join(', ')
                      : 'None discovered yet'}
                  </p>
                </div>
                <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                  <p className="text-xs text-[--app-muted]">Configured channels</p>
                  <p className="mt-1 text-[--app-text]">{channels.length}</p>
                </div>
              </CardContent>
            </Card>

            <div className="flex flex-col gap-3">
              {channels.length === 0 && (
                <Card className="border-[--app-border] bg-[--app-panel]">
                  <CardContent className="py-6 text-sm text-[--app-muted]">
                    No channels found in merged qwen settings.
                  </CardContent>
                </Card>
              )}

              {channels.map((channel) => (
                <Card key={channel.name} className="border-[--app-border] bg-[--app-panel]">
                  <CardHeader className="gap-3">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2">
                          <CardTitle className="text-sm">{channel.name}</CardTitle>
                          <Badge variant="outline" className="border-[--app-border] text-[--app-muted]">
                            {channel.type}
                          </Badge>
                          <Badge variant="outline" className={channelStatusTone(channel.status)}>
                            {channel.status}
                          </Badge>
                        </div>
                        <CardDescription className="mt-1">
                          {channel.description || channel.workingDirectory}
                        </CardDescription>
                      </div>
                      {channel.supportsPairing && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                          disabled={Boolean(loadingPairingsName)}
                          onClick={() => onLoadChannelPairings(channel.name)}
                        >
                          {loadingPairingsName === channel.name
                            ? <Loader2 size={14} className="animate-spin" />
                            : 'Load pairings'}
                        </Button>
                      )}
                    </div>
                  </CardHeader>
                  <CardContent className="grid gap-2 md:grid-cols-2">
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Scope</p>
                      <p className="mt-1 text-[--app-text]">{channel.scope}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Sender policy</p>
                      <p className="mt-1 text-[--app-text]">{channel.senderPolicy}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Session scope</p>
                      <p className="mt-1 text-[--app-text]">{channel.sessionScope}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Activity</p>
                      <p className="mt-1 text-[--app-text]">
                        {channel.sessionCount} sessions · {channel.pendingPairingCount} pending pairings
                      </p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm md:col-span-2">
                      <p className="text-xs text-[--app-muted]">Working directory</p>
                      <p className="mt-1 break-all text-[--app-text]">{channel.workingDirectory}</p>
                    </div>

                    {pairingsByChannel[channel.name] && (
                      <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm md:col-span-2">
                        <div className="mb-3 flex items-center justify-between gap-3">
                          <div>
                            <p className="text-xs text-[--app-muted]">Pending pairing requests</p>
                            <p className="mt-1 text-[--app-text]">
                              {pairingsByChannel[channel.name].pendingCount} pending ·{' '}
                              {pairingsByChannel[channel.name].allowlistCount} approved
                            </p>
                          </div>
                        </div>

                        <div className="flex flex-col gap-3">
                          {pairingsByChannel[channel.name].pendingRequests.length === 0 && (
                            <div className="rounded-lg border border-[--app-border] bg-[--app-panel] p-3 text-sm text-[--app-muted]">
                              No pending pairing requests right now.
                            </div>
                          )}

                          {pairingsByChannel[channel.name].pendingRequests.map((request) => (
                            <div
                              key={`${channel.name}:${request.code}`}
                              className="flex items-center justify-between gap-3 rounded-lg border border-[--app-border] bg-[--app-panel] p-3"
                            >
                              <div>
                                <p className="font-medium text-[--app-text]">
                                  {request.senderName} ({request.senderId})
                                </p>
                                <p className="mt-1 text-xs text-[--app-muted]">
                                  Code {request.code} · {request.minutesAgo}m ago
                                </p>
                              </div>
                              <Button
                                className="bg-orange-500 text-white hover:bg-orange-600"
                                disabled={Boolean(approvingPairingKey)}
                                onClick={() => onApproveChannelPairing(channel.name, request.code)}
                              >
                                {approvingPairingKey === `${channel.name}:${request.code}`
                                  ? <Loader2 size={14} className="animate-spin" />
                                  : 'Approve'}
                              </Button>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="extensions" className="mt-4 flex flex-col gap-4">
            <div className="flex flex-wrap gap-2">
              <Button
                className="bg-orange-500 text-white hover:bg-orange-600"
                onClick={() => setShowAddExtensionForm((value) => !value)}
              >
                <Plus size={14} className="mr-1.5" />
                {showAddExtensionForm ? 'Close add form' : 'Install extension'}
              </Button>
            </div>

            {showAddExtensionForm && (
              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <CardTitle>Add extension</CardTitle>
                  <CardDescription>Install from a local path in link or copy mode.</CardDescription>
                </CardHeader>
                <CardContent className="grid gap-3 md:grid-cols-2">
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted] md:col-span-2">
                    <span>Source path</span>
                    <Input
                      value={extensionForm.sourcePath}
                      onChange={(event) =>
                        setExtensionForm((current) => ({ ...current, sourcePath: event.target.value }))
                      }
                      className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                    />
                  </label>
                  <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
                    <span>Install mode</span>
                    <select
                      value={extensionForm.installMode}
                      onChange={(event) =>
                        setExtensionForm((current) => ({
                          ...current,
                          installMode: event.target.value === 'copy' ? 'copy' : 'link',
                        }))
                      }
                      className="h-9 rounded-md border border-[--app-border] bg-[--app-bg] px-3 text-sm text-[--app-text]"
                    >
                      <option value="link">link</option>
                      <option value="copy">copy</option>
                    </select>
                  </label>
                  <div className="flex items-end">
                    <Button
                      className="bg-orange-500 text-white hover:bg-orange-600"
                      disabled={isInstallingExtension || !extensionForm.sourcePath.trim()}
                      onClick={handleInstallSubmit}
                    >
                      {isInstallingExtension && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                      Install extension
                    </Button>
                  </div>
                </CardContent>
              </Card>
            )}

            <div className="flex flex-col gap-3">
              {extensions.length === 0 && (
                <Card className="border-[--app-border] bg-[--app-panel]">
                  <CardContent className="py-6 text-sm text-[--app-muted]">
                    No extensions installed yet.
                  </CardContent>
                </Card>
              )}

              {extensions.map((extension) => (
                <Card key={extension.wrapperPath} className="border-[--app-border] bg-[--app-panel]">
                  <CardHeader className="gap-3">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2">
                          <CardTitle className="text-sm">{extension.name}</CardTitle>
                          <Badge variant="outline" className={extensionStatusTone(extension.status)}>
                            {extension.status}
                          </Badge>
                          <Badge variant="outline" className="border-[--app-border] text-[--app-muted]">
                            {extension.installType}
                          </Badge>
                        </div>
                        <CardDescription className="mt-1">
                          {extension.description || extension.path}
                        </CardDescription>
                      </div>
                      <div className="flex items-center gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                          disabled={Boolean(loadingSettingsName)}
                          onClick={() => onLoadExtensionSettings(extension.name)}
                        >
                          {loadingSettingsName === extension.name
                            ? <Loader2 size={14} className="animate-spin" />
                            : 'Load settings'}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                          disabled={Boolean(togglingExtensionName)}
                          onClick={() =>
                            onSetExtensionEnabled({
                              name: extension.name,
                              scope: 'project',
                              enabled: !extension.workspaceEnabled,
                            })}
                        >
                          {togglingExtensionName === `${extension.name}:project`
                            ? <Loader2 size={14} className="animate-spin" />
                            : extension.workspaceEnabled ? 'Disable project' : 'Enable project'}
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                          disabled={Boolean(togglingExtensionName)}
                          onClick={() =>
                            onSetExtensionEnabled({
                              name: extension.name,
                              scope: 'user',
                              enabled: !extension.userEnabled,
                            })}
                        >
                          {togglingExtensionName === `${extension.name}:user`
                            ? <Loader2 size={14} className="animate-spin" />
                            : extension.userEnabled ? 'Disable user' : 'Enable user'}
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-[--app-muted] hover:bg-red-500/10 hover:text-red-400"
                          disabled={Boolean(removingExtensionName)}
                          onClick={() => onRemoveExtension(extension.name)}
                        >
                          {removingExtensionName === extension.name
                            ? <Loader2 size={14} className="animate-spin" />
                            : <Trash2 size={14} />
                          }
                        </Button>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="grid gap-2 md:grid-cols-2">
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Version</p>
                      <p className="mt-1 text-[--app-text]">{extension.version}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm">
                      <p className="text-xs text-[--app-muted]">Enablement</p>
                      <p className="mt-1 text-[--app-text]">
                        user {String(extension.userEnabled)} · workspace {String(extension.workspaceEnabled)}
                      </p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm md:col-span-2">
                      <p className="text-xs text-[--app-muted]">Source</p>
                      <p className="mt-1 break-all text-[--app-text]">{extension.source}</p>
                    </div>
                    <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm md:col-span-2">
                      <p className="text-xs text-[--app-muted]">Surfaces</p>
                      <p className="mt-1 text-[--app-text]">
                        {extension.commands.length} commands · {extension.skills.length} skills · {extension.agents.length} agents · {extension.mcpServers.length} MCP servers
                      </p>
                    </div>
                    {extension.lastError && (
                      <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-200 md:col-span-2">
                        {extension.lastError}
                      </div>
                    )}
                    {settingsByExtension[extension.name] && (
                      <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3 text-sm md:col-span-2">
                        <div className="mb-3 flex items-center justify-between gap-3">
                          <div>
                            <p className="text-xs text-[--app-muted]">Settings</p>
                            <p className="mt-1 text-[--app-text]">
                              {settingsByExtension[extension.name].settings.length} declared setting
                              {settingsByExtension[extension.name].settings.length === 1 ? '' : 's'}
                            </p>
                          </div>
                        </div>

                        <div className="flex flex-col gap-3">
                          {settingsByExtension[extension.name].settings.length === 0 && (
                            <div className="rounded-lg border border-[--app-border] bg-[--app-panel] p-3 text-sm text-[--app-muted]">
                              This extension does not declare configurable settings.
                            </div>
                          )}

                          {settingsByExtension[extension.name].settings.map((setting) => {
                            const userSaveKey = getSettingSaveKey(
                              extension.name,
                              setting.environmentVariable,
                              'user',
                            )
                            const projectSaveKey = getSettingSaveKey(
                              extension.name,
                              setting.environmentVariable,
                              'project',
                            )

                            return (
                              <div
                                key={`${extension.name}:${setting.environmentVariable}`}
                                className="rounded-lg border border-[--app-border] bg-[--app-panel] p-3"
                              >
                                <div className="mb-3">
                                  <div className="flex items-center gap-2">
                                    <p className="font-medium text-[--app-text]">{setting.name}</p>
                                    {setting.sensitive && (
                                      <Badge
                                        variant="outline"
                                        className="border-[--app-border] text-[--app-muted]"
                                      >
                                        secret
                                      </Badge>
                                    )}
                                  </div>
                                  <p className="mt-1 text-xs text-[--app-muted]">
                                    {setting.description || 'No description provided.'}
                                  </p>
                                  <p className="mt-1 text-xs text-[--app-muted]">
                                    Env var: {setting.environmentVariable}
                                  </p>
                                  <p className="mt-1 text-xs text-[--app-muted]">
                                    Effective value source:{' '}
                                    {setting.hasWorkspaceValue
                                      ? 'workspace'
                                      : setting.hasUserValue
                                        ? 'user'
                                        : 'unset'}
                                  </p>
                                </div>

                                <div className="grid gap-3 md:grid-cols-2">
                                  <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3">
                                    <p className="mb-2 text-xs text-[--app-muted]">User scope</p>
                                    <div className="flex gap-2">
                                      <Input
                                        type={setting.sensitive ? 'password' : 'text'}
                                        value={getDraftValue(
                                          extension.name,
                                          setting.environmentVariable,
                                          'user',
                                          setting.userValue,
                                        )}
                                        onChange={(event) =>
                                          setDraftValue(
                                            extension.name,
                                            setting.environmentVariable,
                                            'user',
                                            event.target.value,
                                          )}
                                        className="border-[--app-border] bg-[--app-panel] text-[--app-text]"
                                      />
                                      <Button
                                        className="bg-orange-500 text-white hover:bg-orange-600"
                                        disabled={Boolean(savingSettingKey)}
                                        onClick={() =>
                                          onSetExtensionSetting({
                                            name: extension.name,
                                            setting: setting.environmentVariable,
                                            scope: 'user',
                                            value: getDraftValue(
                                              extension.name,
                                              setting.environmentVariable,
                                              'user',
                                              setting.userValue,
                                            ),
                                          })}
                                      >
                                        {savingSettingKey === userSaveKey
                                          ? <Loader2 size={14} className="animate-spin" />
                                          : 'Save'}
                                      </Button>
                                    </div>
                                  </div>

                                  <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3">
                                    <p className="mb-2 text-xs text-[--app-muted]">Workspace scope</p>
                                    <div className="flex gap-2">
                                      <Input
                                        type={setting.sensitive ? 'password' : 'text'}
                                        value={getDraftValue(
                                          extension.name,
                                          setting.environmentVariable,
                                          'project',
                                          setting.workspaceValue,
                                        )}
                                        onChange={(event) =>
                                          setDraftValue(
                                            extension.name,
                                            setting.environmentVariable,
                                            'project',
                                            event.target.value,
                                          )}
                                        className="border-[--app-border] bg-[--app-panel] text-[--app-text]"
                                      />
                                      <Button
                                        className="bg-orange-500 text-white hover:bg-orange-600"
                                        disabled={Boolean(savingSettingKey)}
                                        onClick={() =>
                                          onSetExtensionSetting({
                                            name: extension.name,
                                            setting: setting.environmentVariable,
                                            scope: 'project',
                                            value: getDraftValue(
                                              extension.name,
                                              setting.environmentVariable,
                                              'project',
                                              setting.workspaceValue,
                                            ),
                                          })}
                                      >
                                        {savingSettingKey === projectSaveKey
                                          ? <Loader2 size={14} className="animate-spin" />
                                          : 'Save'}
                                      </Button>
                                    </div>
                                  </div>
                                </div>
                              </div>
                            )
                          })}
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>
              ))}
            </div>
          </TabsContent>

          <TabsContent value="agents" className="mt-4 flex flex-col gap-3">
            {agents.map((agent) => (
              <Card key={agent.id} className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <CardTitle className="text-sm">{agent.name}</CardTitle>
                  <CardDescription>{agent.description || agent.path}</CardDescription>
                </CardHeader>
                <CardContent className="flex flex-wrap gap-2">
                  <Badge variant="outline" className="border-[--app-border] text-[--app-muted]">
                    {agent.scope}
                  </Badge>
                  <Badge variant="outline" className="border-[--app-border] text-[--app-muted]">
                    {agent.allowedTools.length} tools
                  </Badge>
                </CardContent>
              </Card>
            ))}

            {agents.length === 0 && (
              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardContent className="py-6 text-sm text-[--app-muted]">
                  No skill surfaces found.
                </CardContent>
              </Card>
            )}
          </TabsContent>

          <TabsContent value="tools" className="mt-4 flex flex-col gap-3">
            {tools.map((tool) => (
              <Card key={tool.name} className="border-[--app-border] bg-[--app-panel]">
                <CardHeader>
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <CardTitle className="flex items-center gap-2 text-sm">
                        <Wrench size={14} />
                        {tool.displayName || tool.name}
                      </CardTitle>
                      <CardDescription>{tool.kind}</CardDescription>
                    </div>
                    <Badge variant="outline" className={approvalTone(tool.approvalState)}>
                      {tool.approvalState}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="text-sm text-[--app-muted]">
                  {tool.approvalReason || 'No approval note provided.'}
                </CardContent>
              </Card>
            ))}

            {tools.length === 0 && (
              <Card className="border-[--app-border] bg-[--app-panel]">
                <CardContent className="py-6 text-sm text-[--app-muted]">
                  No native tools registered.
                </CardContent>
              </Card>
            )}
          </TabsContent>
        </Tabs>
      </div>
    </div>
  )
}
