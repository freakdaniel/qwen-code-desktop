import { useState } from 'react'
import type {
  AppBootstrapPayload,
  ExtensionSettingsSnapshot,
  ExtensionSnapshot,
} from '@/types/desktop'

interface UseExtensionsOptions {
  setBootstrap: React.Dispatch<React.SetStateAction<AppBootstrapPayload>>
}

export function useExtensions({ setBootstrap }: UseExtensionsOptions) {
  const [isInstallingExtension, setIsInstallingExtension] = useState(false)
  const [togglingExtensionName, setTogglingExtensionName] = useState('')
  const [removingExtensionName, setRemovingExtensionName] = useState('')
  const [loadingSettingsName, setLoadingSettingsName] = useState('')
  const [savingSettingKey, setSavingSettingKey] = useState('')
  const [settingsByExtension, setSettingsByExtension] = useState<Record<string, ExtensionSettingsSnapshot>>({})

  const applySnapshot = (snapshot: ExtensionSnapshot) => {
    setBootstrap((current) => ({ ...current, qwenExtensions: snapshot }))
  }

  const handleInstallExtension = async (request: {
    sourcePath: string
    installMode: 'link' | 'copy'
  }) => {
    if (!window.qwenDesktop || isInstallingExtension) return
    setIsInstallingExtension(true)
    try {
      const snapshot = await window.qwenDesktop.installExtension(request)
      applySnapshot(snapshot)
    } finally {
      setIsInstallingExtension(false)
    }
  }

  const handleSetExtensionEnabled = async (request: {
    name: string
    scope: 'user' | 'project'
    enabled: boolean
  }) => {
    if (!window.qwenDesktop || togglingExtensionName) return
    setTogglingExtensionName(`${request.name}:${request.scope}`)
    try {
      const snapshot = await window.qwenDesktop.setExtensionEnabled(request)
      applySnapshot(snapshot)
    } finally {
      setTogglingExtensionName('')
    }
  }

  const handleRemoveExtension = async (name: string) => {
    if (!window.qwenDesktop || removingExtensionName) return
    setRemovingExtensionName(name)
    try {
      const snapshot = await window.qwenDesktop.removeExtension({ name })
      applySnapshot(snapshot)
    } finally {
      setRemovingExtensionName('')
    }
  }

  const handleLoadExtensionSettings = async (name: string) => {
    if (!window.qwenDesktop || loadingSettingsName) return
    setLoadingSettingsName(name)
    try {
      const snapshot = await window.qwenDesktop.getExtensionSettings({ name })
      setSettingsByExtension((current) => ({ ...current, [name]: snapshot }))
    } finally {
      setLoadingSettingsName('')
    }
  }

  const handleSetExtensionSetting = async (request: {
    name: string
    setting: string
    scope: 'user' | 'project'
    value: string
  }) => {
    if (!window.qwenDesktop || savingSettingKey) return
    setSavingSettingKey(`${request.name}:${request.setting}:${request.scope}`)
    try {
      const snapshot = await window.qwenDesktop.setExtensionSetting(request)
      setSettingsByExtension((current) => ({ ...current, [request.name]: snapshot }))
    } finally {
      setSavingSettingKey('')
    }
  }

  return {
    isInstallingExtension,
    togglingExtensionName,
    removingExtensionName,
    loadingSettingsName,
    savingSettingKey,
    settingsByExtension,
    handleInstallExtension,
    handleSetExtensionEnabled,
    handleRemoveExtension,
    handleLoadExtensionSettings,
    handleSetExtensionSetting,
  }
}
