import { useState } from 'react'
import type { AppBootstrapPayload, ChannelPairingSnapshot, ChannelSnapshot } from '@/types/desktop'

interface UseChannelsOptions {
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
}

export function useChannels({ setBootstrap }: UseChannelsOptions) {
  const [loadingPairingsName, setLoadingPairingsName] = useState('')
  const [approvingPairingKey, setApprovingPairingKey] = useState('')
  const [pairingsByChannel, setPairingsByChannel] = useState<Record<string, ChannelPairingSnapshot>>({})

  const applySnapshot = (snapshot: ChannelSnapshot) => {
    setBootstrap((current) => ({ ...current, qwenChannels: snapshot }))
  }

  const handleLoadChannelPairings = async (name: string) => {
    if (!window.qwenDesktop || loadingPairingsName) return
    setLoadingPairingsName(name)
    try {
      const snapshot = await window.qwenDesktop.getChannelPairings({ name })
      setPairingsByChannel((current) => ({ ...current, [name]: snapshot }))
    } finally {
      setLoadingPairingsName('')
    }
  }

  const handleApproveChannelPairing = async (name: string, code: string) => {
    if (!window.qwenDesktop || approvingPairingKey) return
    setApprovingPairingKey(`${name}:${code}`)
    try {
      const pairingSnapshot = await window.qwenDesktop.approveChannelPairing({ name, code })
      setPairingsByChannel((current) => ({ ...current, [name]: pairingSnapshot }))
      const bootstrap = await window.qwenDesktop.bootstrap()
      applySnapshot(bootstrap.qwenChannels)
    } finally {
      setApprovingPairingKey('')
    }
  }

  return {
    loadingPairingsName,
    approvingPairingKey,
    pairingsByChannel,
    handleLoadChannelPairings,
    handleApproveChannelPairing,
  }
}
