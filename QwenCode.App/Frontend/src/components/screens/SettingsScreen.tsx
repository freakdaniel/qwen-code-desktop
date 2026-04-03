import { useEffect, useState, type ReactNode } from 'react'
import { KeyRound, Link as LinkIcon, Loader2, ShieldCheck, ShieldOff } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import type { AuthStatusSnapshot } from '@/types/desktop'

interface SettingsScreenProps {
  authSnapshot: AuthStatusSnapshot
  isSavingAuth: boolean
  isStartingOAuthFlow: boolean
  isCancellingOAuthFlow: boolean
  onConfigureQwenOAuth: (request: {
    scope: 'user' | 'project'
    accessToken: string
    refreshToken: string
  }) => Promise<void> | void
  onConfigureOpenAi: (request: {
    scope: 'user' | 'project'
    authType: string
    model: string
    baseUrl: string
    apiKey: string
    apiKeyEnvironmentVariable: string
  }) => Promise<void> | void
  onConfigureCodingPlan: (request: {
    scope: 'user' | 'project'
    region: 'china' | 'global'
    apiKey: string
    model: string
  }) => Promise<void> | void
  onDisconnect: (scope: 'user' | 'project', clearPersistedCredentials: boolean) => Promise<void> | void
  onStartOAuthFlow: (scope: 'user' | 'project') => Promise<void> | void
  onCancelOAuthFlow: (flowId: string) => Promise<void> | void
}

function normalizeScope(value: string): 'user' | 'project' {
  return value === 'project' ? 'project' : 'user'
}

function normalizeRegion(value: string): 'china' | 'global' {
  return value === 'china' ? 'china' : 'global'
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5 text-xs text-[--app-muted]">
      <span>{label}</span>
      {children}
    </label>
  )
}

export function SettingsScreen({
  authSnapshot,
  isSavingAuth,
  isStartingOAuthFlow,
  isCancellingOAuthFlow,
  onConfigureQwenOAuth,
  onConfigureOpenAi,
  onConfigureCodingPlan,
  onDisconnect,
  onStartOAuthFlow,
  onCancelOAuthFlow,
}: SettingsScreenProps) {
  const [scope, setScope] = useState<'user' | 'project'>(normalizeScope(authSnapshot.selectedScope))
  const [oauthForm, setOauthForm] = useState({ accessToken: '', refreshToken: '' })
  const [codingPlanForm, setCodingPlanForm] = useState({
    region: 'global' as 'china' | 'global',
    apiKey: '',
    model: authSnapshot.model || 'qwen-coder-plus',
  })
  const [openAiForm, setOpenAiForm] = useState({
    baseUrl: authSnapshot.endpoint || '',
    apiKey: '',
    model: authSnapshot.model || 'qwen-coder-plus',
  })

  useEffect(() => {
    setScope(normalizeScope(authSnapshot.selectedScope))
    setCodingPlanForm((current) => ({
      ...current,
      model: authSnapshot.model || current.model,
    }))
    setOpenAiForm((current) => ({
      ...current,
      baseUrl: authSnapshot.endpoint || current.baseUrl,
      model: authSnapshot.model || current.model,
    }))
  }, [authSnapshot.endpoint, authSnapshot.model, authSnapshot.selectedScope])

  return (
    <div className="h-full overflow-y-auto">
      <div className="mx-auto flex max-w-4xl flex-col gap-6 px-6 py-8">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-[--app-text]">Settings</h1>
            <p className="mt-1 text-sm text-[--app-muted]">
              Configure authentication providers used by the desktop runtime.
            </p>
          </div>

          <div className="flex items-center gap-2 rounded-full border border-[--app-border] bg-[--app-panel] px-3 py-1.5 text-xs text-[--app-muted]">
            {authSnapshot.status === 'connected' ? <ShieldCheck size={14} /> : <ShieldOff size={14} />}
            <span>{authSnapshot.status || 'unknown'}</span>
          </div>
        </div>

        <Card className="border-[--app-border] bg-[--app-panel]">
          <CardHeader>
            <CardTitle>Current provider</CardTitle>
            <CardDescription>Desktop bridge snapshot from the native host.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-2">
            <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3">
              <p className="text-xs text-[--app-muted]">Provider</p>
              <p className="mt-1 text-sm text-[--app-text]">{authSnapshot.displayName || 'Not configured'}</p>
            </div>
            <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3">
              <p className="text-xs text-[--app-muted]">Scope</p>
              <p className="mt-1 text-sm text-[--app-text]">{scope}</p>
            </div>
            <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3">
              <p className="text-xs text-[--app-muted]">Model</p>
              <p className="mt-1 text-sm text-[--app-text]">{authSnapshot.model || 'Not set'}</p>
            </div>
            <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-3">
              <p className="text-xs text-[--app-muted]">Endpoint</p>
              <p className="mt-1 break-all text-sm text-[--app-text]">{authSnapshot.endpoint || 'Not set'}</p>
            </div>
          </CardContent>
        </Card>

        <div className="flex items-center gap-3">
          <span className="text-xs font-medium uppercase tracking-[0.18em] text-[--app-muted]">Scope</span>
          <div className="flex rounded-lg border border-[--app-border] bg-[--app-panel] p-1">
            {(['user', 'project'] as const).map((value) => (
              <button
                key={value}
                type="button"
                className={`rounded-md px-3 py-1.5 text-xs transition-colors ${
                  scope === value
                    ? 'bg-[--app-elevated] text-[--app-text]'
                    : 'text-[--app-muted] hover:text-[--app-text]'
                }`}
                onClick={() => setScope(value)}
              >
                {value}
              </button>
            ))}
          </div>
        </div>

        <Card className="border-[--app-border] bg-[--app-panel]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck size={16} />
              Qwen OAuth
            </CardTitle>
            <CardDescription>Manual tokens or browser device flow.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Access token">
                <Input
                  type="password"
                  value={oauthForm.accessToken}
                  onChange={(event) =>
                    setOauthForm((current) => ({ ...current, accessToken: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
              <Field label="Refresh token">
                <Input
                  type="password"
                  value={oauthForm.refreshToken}
                  onChange={(event) =>
                    setOauthForm((current) => ({ ...current, refreshToken: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button
                className="bg-orange-500 text-white hover:bg-orange-600"
                disabled={isSavingAuth || !oauthForm.accessToken.trim()}
                onClick={() => onConfigureQwenOAuth({ scope, ...oauthForm })}
              >
                {isSavingAuth && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                Save OAuth tokens
              </Button>
              <Button
                variant="outline"
                className="border-[--app-border] bg-transparent text-[--app-text] hover:bg-[--app-elevated]"
                disabled={isStartingOAuthFlow}
                onClick={() => onStartOAuthFlow(scope)}
              >
                {isStartingOAuthFlow && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                Start browser flow
              </Button>
            </div>

            {authSnapshot.deviceFlow && (
              <div className="rounded-lg border border-[--app-border] bg-[--app-bg] p-4 text-sm">
                <div className="flex items-start justify-between gap-4">
                  <div className="space-y-1">
                    <p className="font-medium text-[--app-text]">Device flow</p>
                    <p className="text-[--app-muted]">Status: {authSnapshot.deviceFlow.status}</p>
                    <p className="text-[--app-muted]">Code: {authSnapshot.deviceFlow.userCode}</p>
                    <p className="break-all text-[--app-muted]">
                      URL: {authSnapshot.deviceFlow.verificationUriComplete || authSnapshot.deviceFlow.verificationUri}
                    </p>
                  </div>
                  <Button
                    variant="ghost"
                    className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                    disabled={isCancellingOAuthFlow}
                    onClick={() => onCancelOAuthFlow(authSnapshot.deviceFlow?.flowId ?? '')}
                  >
                    {isCancellingOAuthFlow && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                    Cancel
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <Separator className="bg-[--app-border]" />

        <Card className="border-[--app-border] bg-[--app-panel]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <KeyRound size={16} />
              Coding Plan
            </CardTitle>
            <CardDescription>Direct API-key configuration for Coding Plan auth.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="grid gap-3 md:grid-cols-3">
              <Field label="Region">
                <select
                  value={codingPlanForm.region}
                  onChange={(event) =>
                    setCodingPlanForm((current) => ({
                      ...current,
                      region: normalizeRegion(event.target.value),
                    }))
                  }
                  className="h-9 rounded-md border border-[--app-border] bg-[--app-bg] px-3 text-sm text-[--app-text]"
                >
                  <option value="global">global</option>
                  <option value="china">china</option>
                </select>
              </Field>
              <Field label="Model">
                <Input
                  value={codingPlanForm.model}
                  onChange={(event) =>
                    setCodingPlanForm((current) => ({ ...current, model: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
              <Field label="API key">
                <Input
                  type="password"
                  value={codingPlanForm.apiKey}
                  onChange={(event) =>
                    setCodingPlanForm((current) => ({ ...current, apiKey: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button
                className="bg-orange-500 text-white hover:bg-orange-600"
                disabled={isSavingAuth || !codingPlanForm.apiKey.trim()}
                onClick={() => onConfigureCodingPlan({ scope, ...codingPlanForm })}
              >
                {isSavingAuth && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                Save Coding Plan
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card className="border-[--app-border] bg-[--app-panel]">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <LinkIcon size={16} />
              OpenAI-compatible
            </CardTitle>
            <CardDescription>Endpoint, model and API key for compatible providers.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="grid gap-3 md:grid-cols-3">
              <Field label="Endpoint">
                <Input
                  value={openAiForm.baseUrl}
                  onChange={(event) =>
                    setOpenAiForm((current) => ({ ...current, baseUrl: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
              <Field label="Model">
                <Input
                  value={openAiForm.model}
                  onChange={(event) =>
                    setOpenAiForm((current) => ({ ...current, model: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
              <Field label="API key">
                <Input
                  type="password"
                  value={openAiForm.apiKey}
                  onChange={(event) =>
                    setOpenAiForm((current) => ({ ...current, apiKey: event.target.value }))
                  }
                  className="border-[--app-border] bg-[--app-bg] text-[--app-text]"
                />
              </Field>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button
                className="bg-orange-500 text-white hover:bg-orange-600"
                disabled={isSavingAuth || !openAiForm.baseUrl.trim()}
                onClick={() =>
                  onConfigureOpenAi({
                    scope,
                    authType: 'api-key',
                    model: openAiForm.model,
                    baseUrl: openAiForm.baseUrl,
                    apiKey: openAiForm.apiKey,
                    apiKeyEnvironmentVariable: '',
                  })
                }
              >
                {isSavingAuth && <Loader2 size={14} className="mr-1.5 animate-spin" />}
                Save provider
              </Button>
              <Button
                variant="outline"
                className="border-[--app-border] bg-transparent text-[--app-text] hover:bg-[--app-elevated]"
                disabled={isSavingAuth}
                onClick={() => onDisconnect(scope, false)}
              >
                Disconnect
              </Button>
              <Button
                variant="ghost"
                className="text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text]"
                disabled={isSavingAuth}
                onClick={() => onDisconnect(scope, true)}
              >
                Clear persisted credentials
              </Button>
            </div>

            {authSnapshot.lastError && (
              <p className="text-xs text-orange-300">{authSnapshot.lastError}</p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
