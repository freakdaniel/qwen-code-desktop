// Frontend/src/hooks/useMcp.ts
import { useState } from 'react'
import type { AppBootstrapPayload, McpSnapshot } from '@/types/desktop'

interface UseMcpOptions {
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
  setMcpSnapshot: React.Dispatch<React.SetStateAction<McpSnapshot>>
}

export function useMcp({ setBootstrap, setMcpSnapshot }: UseMcpOptions) {
  const [isSavingMcp, setIsSavingMcp] = useState(false)
  const [reconnectingMcpName, setReconnectingMcpName] = useState('')
  const [removingMcpName, setRemovingMcpName] = useState('')

  const applySnapshot = (snapshot: McpSnapshot) => {
    setMcpSnapshot(snapshot)
    setBootstrap((c) => ({ ...c, qwenMcp: snapshot }))
  }

  const handleAddMcpServer = async (request: {
    name: string
    scope: 'user' | 'project'
    transport: 'stdio' | 'http' | 'sse'
    commandOrUrl: string
    description: string
  }) => {
    if (!window.qwenDesktop || isSavingMcp) return
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
      applySnapshot(snapshot)
    } finally {
      setIsSavingMcp(false)
    }
  }

  const handleReconnectMcpServer = async (name: string) => {
    if (!window.qwenDesktop || reconnectingMcpName) return
    setReconnectingMcpName(name)
    try {
      const snapshot = await window.qwenDesktop.reconnectMcpServer({ name })
      applySnapshot(snapshot)
    } finally {
      setReconnectingMcpName('')
    }
  }

  const handleRemoveMcpServer = async (name: string, scope: string) => {
    if (!window.qwenDesktop || removingMcpName) return
    setRemovingMcpName(name)
    try {
      const snapshot = await window.qwenDesktop.removeMcpServer({ name, scope })
      applySnapshot(snapshot)
    } finally {
      setRemovingMcpName('')
    }
  }

  return {
    isSavingMcp,
    reconnectingMcpName,
    removingMcpName,
    handleAddMcpServer,
    handleReconnectMcpServer,
    handleRemoveMcpServer,
  }
}
