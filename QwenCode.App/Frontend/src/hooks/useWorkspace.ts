import { useState } from 'react'
import type { AppBootstrapPayload, WorkspaceSnapshot } from '@/types/desktop'

interface UseWorkspaceOptions {
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
}

export function useWorkspace({ setBootstrap }: UseWorkspaceOptions) {
  const [isCreatingManagedWorktree, setIsCreatingManagedWorktree] = useState(false)
  const [cleaningManagedSessionId, setCleaningManagedSessionId] = useState('')
  const [isCreatingGitCheckpoint, setIsCreatingGitCheckpoint] = useState(false)
  const [restoringGitCheckpointHash, setRestoringGitCheckpointHash] = useState('')

  const applySnapshot = (snapshot: WorkspaceSnapshot) => {
    setBootstrap((current) => ({ ...current, qwenWorkspace: snapshot }))
  }

  const handleCreateManagedWorktree = async (request: {
    sessionId: string
    name: string
    baseBranch?: string
  }) => {
    if (!window.qwenDesktop || isCreatingManagedWorktree) return
    setIsCreatingManagedWorktree(true)
    try {
      const snapshot = await window.qwenDesktop.createManagedWorktree({
        sessionId: request.sessionId,
        name: request.name,
        baseBranch: request.baseBranch ?? '',
      })
      applySnapshot(snapshot)
    } finally {
      setIsCreatingManagedWorktree(false)
    }
  }

  const handleCleanupManagedSession = async (sessionId: string) => {
    if (!window.qwenDesktop || cleaningManagedSessionId) return
    setCleaningManagedSessionId(sessionId)
    try {
      const snapshot = await window.qwenDesktop.cleanupManagedSession({ sessionId })
      applySnapshot(snapshot)
    } finally {
      setCleaningManagedSessionId('')
    }
  }

  const handleCreateGitCheckpoint = async (message: string) => {
    if (!window.qwenDesktop || isCreatingGitCheckpoint) return
    setIsCreatingGitCheckpoint(true)
    try {
      const snapshot = await window.qwenDesktop.createGitCheckpoint({
        message: message.trim(),
      })
      applySnapshot(snapshot)
    } finally {
      setIsCreatingGitCheckpoint(false)
    }
  }

  const handleRestoreGitCheckpoint = async (commitHash: string) => {
    if (!window.qwenDesktop || restoringGitCheckpointHash) return
    setRestoringGitCheckpointHash(commitHash)
    try {
      const snapshot = await window.qwenDesktop.restoreGitCheckpoint({
        commitHash,
      })
      applySnapshot(snapshot)
    } finally {
      setRestoringGitCheckpointHash('')
    }
  }

  return {
    isCreatingManagedWorktree,
    cleaningManagedSessionId,
    isCreatingGitCheckpoint,
    restoringGitCheckpointHash,
    handleCreateManagedWorktree,
    handleCleanupManagedSession,
    handleCreateGitCheckpoint,
    handleRestoreGitCheckpoint,
  }
}
