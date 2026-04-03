// Frontend/src/hooks/useAuth.ts
import { useState } from 'react'
import type { AuthStatusSnapshot } from '@/types/desktop'

interface UseAuthOptions {
  updateAuthSnapshot: (snapshot: AuthStatusSnapshot) => void
}

export function useAuth({ updateAuthSnapshot }: UseAuthOptions) {
  const [isSavingAuth, setIsSavingAuth] = useState(false)
  const [isStartingOAuthFlow, setIsStartingOAuthFlow] = useState(false)
  const [isCancellingOAuthFlow, setIsCancellingOAuthFlow] = useState(false)

  const handleConfigureQwenOAuth = async (request: {
    scope: 'user' | 'project'
    accessToken: string
    refreshToken: string
  }) => {
    if (!window.qwenDesktop || isSavingAuth) return
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
    if (!window.qwenDesktop || isSavingAuth) return
    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.configureCodingPlanAuth(request)
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleConfigureOpenAiCompatible = async (request: {
    scope: 'user' | 'project'
    authType: string
    model: string
    baseUrl: string
    apiKey: string
    apiKeyEnvironmentVariable: string
  }) => {
    if (!window.qwenDesktop || isSavingAuth) return
    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.configureOpenAiCompatibleAuth(request)
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleDisconnectAuth = async (scope: 'user' | 'project', clearPersistedCredentials: boolean) => {
    if (!window.qwenDesktop || isSavingAuth) return
    setIsSavingAuth(true)
    try {
      const snapshot = await window.qwenDesktop.disconnectAuth({ scope, clearPersistedCredentials })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsSavingAuth(false)
    }
  }

  const handleStartQwenOAuthDeviceFlow = async (scope: 'user' | 'project') => {
    if (!window.qwenDesktop || isStartingOAuthFlow) return
    setIsStartingOAuthFlow(true)
    try {
      const snapshot = await window.qwenDesktop.startQwenOAuthDeviceFlow({ scope })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsStartingOAuthFlow(false)
    }
  }

  const handleCancelQwenOAuthDeviceFlow = async (flowId: string) => {
    if (!window.qwenDesktop || isCancellingOAuthFlow) return
    setIsCancellingOAuthFlow(true)
    try {
      const snapshot = await window.qwenDesktop.cancelQwenOAuthDeviceFlow({ flowId })
      updateAuthSnapshot(snapshot)
    } finally {
      setIsCancellingOAuthFlow(false)
    }
  }

  return {
    isSavingAuth,
    isStartingOAuthFlow,
    isCancellingOAuthFlow,
    handleConfigureQwenOAuth,
    handleConfigureCodingPlan,
    handleConfigureOpenAiCompatible,
    handleDisconnectAuth,
    handleStartQwenOAuthDeviceFlow,
    handleCancelQwenOAuthDeviceFlow,
  }
}
