import { useState } from 'react'
import { Circle, Loader2, Plus, RefreshCw, Trash2, Wrench } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import type { McpServerDefinition, NativeToolRegistration, QwenSkillSurface } from '@/types/desktop'

interface UtilitiesScreenProps {
  mcpServers: McpServerDefinition[]
  tools: NativeToolRegistration[]
  agents: QwenSkillSurface[]
  isSavingMcp: boolean
  reconnectingMcpName: string
  removingMcpName: string
  onReconnect: (name: string) => Promise<void> | void
  onRemove: (name: string, scope: string) => Promise<void> | void
  onAddServer: (request: {
    name: string
    scope: 'user' | 'project'
    transport: 'stdio' | 'http' | 'sse'
    commandOrUrl: string
    description: string
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

export function UtilitiesScreen({
  mcpServers,
  tools,
  agents,
  isSavingMcp,
  reconnectingMcpName,
  removingMcpName,
  onReconnect,
  onRemove,
  onAddServer,
}: UtilitiesScreenProps) {
  const [showAddForm, setShowAddForm] = useState(false)
  const [form, setForm] = useState<McpFormState>(DEFAULT_FORM)

  const connectedCount = mcpServers.filter((server) => server.status === 'connected').length

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

  return (
    <div className="h-full overflow-y-auto">
      <div className="mx-auto flex max-w-5xl flex-col gap-6 px-6 py-8">
        <div>
          <h1 className="text-xl font-semibold text-[--app-text]">Utilities</h1>
          <p className="mt-1 text-sm text-[--app-muted]">
            MCP servers, native tools and reusable agents exposed by the desktop runtime.
          </p>
        </div>

        <div className="grid gap-3 md:grid-cols-3">
          <Card className="border-[--app-border] bg-[--app-panel]">
            <CardHeader>
              <CardTitle>MCP servers</CardTitle>
              <CardDescription>{connectedCount} connected</CardDescription>
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
              <CardTitle>Tools</CardTitle>
              <CardDescription>{tools.length} native registrations</CardDescription>
            </CardHeader>
          </Card>
        </div>

        <Tabs defaultValue="mcp">
          <TabsList className="border border-[--app-border] bg-[--app-elevated]">
            <TabsTrigger value="mcp">MCP</TabsTrigger>
            <TabsTrigger value="agents">Agents</TabsTrigger>
            <TabsTrigger value="tools">Tools</TabsTrigger>
          </TabsList>

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
