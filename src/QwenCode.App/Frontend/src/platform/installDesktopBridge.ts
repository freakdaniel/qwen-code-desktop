import type { DesktopBridge } from '@/types/desktop'
import { qwenDesktopChannels } from '@/types/ipc.generated'

type HostWindow = Window & {
  chrome?: {
    webview?: {
      postMessage: (message: string) => void
      addEventListener: (type: 'message', handler: (event: { data: unknown }) => void) => void
    }
  }
  external?: {
    sendMessage?: (message: string) => void
    receiveMessage?: (message: unknown) => void
  }
}

type OutboundBridgeMessage =
  | {
      type: 'invoke'
      requestId: string
      channel: string
      payload?: unknown
    }
  | {
      type: 'command'
      requestId?: string
      command:
        | 'window:minimize'
        | 'window:toggle-maximize'
        | 'window:close'
        | 'window:begin-drag'
        | 'window:begin-resize'
        | 'external:open'
      url?: string
      edge?: string
    }

type HostCommand = Extract<OutboundBridgeMessage, { type: 'command' }>['command']

type InboundBridgeMessage =
  | {
      type: 'response'
      requestId: string
      channel: string
      payload?: unknown
      error?: string
    }
  | {
      type: 'event'
      channel: string
      payload?: unknown
    }

const hostWindow = window as HostWindow
const responseHandlers = new Map<string, { resolve: (value: unknown) => void; reject: (reason?: unknown) => void }>()
const eventHandlers = new Map<string, Set<(payload: unknown) => void>>()
const rawMessageListeners = new Set<(message: string) => void>()
const subscriptionMethods = new Set([
  'subscribeStateChanged',
  'subscribeAuthChanged',
  'subscribeSessionEvents',
  'subscribeArenaEvents',
])

let nextRequestId = 0
let receiverInstalled = false
let externalLinkInterceptionInstalled = false

function sendToHost(message: OutboundBridgeMessage) {
  const serialized = JSON.stringify(message)

  if (hostWindow.chrome?.webview) {
    hostWindow.chrome.webview.postMessage(serialized)
    return
  }

  if (hostWindow.external?.sendMessage) {
    hostWindow.external.sendMessage(serialized)
    return
  }

  console.warn('Desktop bridge transport is unavailable', message)
}

function installReceiver() {
  if (receiverInstalled) {
    return
  }

  receiverInstalled = true

  const dispatchRawMessage = (message: unknown) => {
    const normalized = typeof message === 'string' ? message : String(message ?? '')
    if (!normalized) {
      return
    }

    rawMessageListeners.forEach((listener) => listener(normalized))
  }

  if (hostWindow.chrome?.webview) {
    hostWindow.chrome.webview.addEventListener('message', (event) => {
      dispatchRawMessage(event.data)
    })
  }

  if (hostWindow.external) {
    const previousReceiveMessage = hostWindow.external.receiveMessage
    hostWindow.external.receiveMessage = (message) => {
      dispatchRawMessage(message)
      previousReceiveMessage?.(message)
    }
  }
}

function ensureExternalLinkInterception(bridge: DesktopBridge) {
  if (externalLinkInterceptionInstalled) {
    return
  }

  externalLinkInterceptionInstalled = true

  const isExternalUrl = (value: string) => {
    try {
      const url = new URL(value, window.location.href)
      return ['http:', 'https:', 'mailto:'].includes(url.protocol)
    } catch {
      return false
    }
  }

  const openExternal = (url: string) => {
    if (!isExternalUrl(url)) {
      return false
    }

    void bridge.openExternalUrl?.(url)
    return true
  }

  const handlePointerNavigation = (event: MouseEvent) => {
    const target = event.target
    const anchor = target && typeof (target as Element).closest === 'function'
      ? (target as Element).closest('a[href]')
      : null

    if (!anchor) {
      return
    }

    const href = anchor.getAttribute('href') || (anchor as HTMLAnchorElement).href
    if (!isExternalUrl(href)) {
      return
    }

    event.preventDefault()
    event.stopPropagation()
    openExternal(href)
  }

  window.addEventListener('click', handlePointerNavigation, true)
  window.addEventListener('auxclick', handlePointerNavigation, true)

  const originalOpen = window.open?.bind(window)
  window.open = (url, target, features) => {
    if (typeof url === 'string' && openExternal(url)) {
      return null
    }

    return originalOpen ? originalOpen(url, target, features) : null
  }
}

function invoke<T>(channel: string, payload: unknown = {}): Promise<T> {
  installReceiver()
  const requestId = `req-${++nextRequestId}`

  return new Promise<T>((resolve, reject) => {
    responseHandlers.set(requestId, {
      resolve: (value) => resolve(value as T),
      reject,
    })
    sendToHost({
      type: 'invoke',
      requestId,
      channel,
      payload,
    })
  })
}

function sendCommand<T>(command: HostCommand, extras?: { url?: string; edge?: string; expectResponse?: boolean }): Promise<T> {
  installReceiver()

  if (!extras?.expectResponse) {
    sendToHost({
      type: 'command',
      command,
      url: extras?.url,
      edge: extras?.edge,
    })

    return Promise.resolve(undefined as T)
  }

  const requestId = `req-${++nextRequestId}`

  return new Promise<T>((resolve, reject) => {
    responseHandlers.set(requestId, {
      resolve: (value) => resolve(value as T),
      reject,
    })
    sendToHost({
      type: 'command',
      requestId,
      command,
      url: extras?.url,
      edge: extras?.edge,
    })
  })
}

function subscribe<T>(channel: string, callback: (payload: T) => void) {
  installReceiver()
  const handlers = eventHandlers.get(channel) ?? new Set<(payload: unknown) => void>()
  handlers.add(callback as (payload: unknown) => void)
  eventHandlers.set(channel, handlers)

  return () => {
    handlers.delete(callback as (payload: unknown) => void)
    if (handlers.size === 0) {
      eventHandlers.delete(channel)
    }
  }
}

rawMessageListeners.add((message) => {
  let payload: InboundBridgeMessage

  try {
    payload = JSON.parse(message) as InboundBridgeMessage
  } catch {
    return
  }

  if (payload.type === 'response') {
    const pending = responseHandlers.get(payload.requestId)
    if (!pending) {
      return
    }

    responseHandlers.delete(payload.requestId)
    if (payload.error) {
      pending.reject(new Error(payload.error))
      return
    }

    pending.resolve(payload.payload)
    return
  }

  if (payload.type === 'event') {
    const handlers = eventHandlers.get(payload.channel)
    handlers?.forEach((handler) => handler(payload.payload))
  }
})

const bridgeEntries = Object.entries(qwenDesktopChannels).map(([methodName, channel]) => {
  if (subscriptionMethods.has(methodName)) {
    return [methodName, (callback: (payload: unknown) => void) => subscribe(channel, callback)]
  }

  return [methodName, (payload?: unknown) => invoke(channel, payload ?? {})]
})

const bridge = Object.fromEntries(bridgeEntries) as DesktopBridge
bridge.setLocale = (locale: string) => invoke(qwenDesktopChannels.setLocale, { locale })
bridge.openExternalUrl = (url: string) => sendCommand('external:open', { url, expectResponse: true }).then((result) => {
  if (!result || typeof result !== 'object') {
    return false
  }

  return Boolean((result as { opened?: boolean }).opened)
})
bridge.minimizeWindow = () => {
  void sendCommand('window:minimize')
}
bridge.maximizeWindow = () => {
  void sendCommand('window:toggle-maximize')
}
bridge.beginWindowDrag = () => {
  void sendCommand('window:begin-drag')
}
bridge.beginWindowResize = (edge: string) => {
  void sendCommand('window:begin-resize', { edge })
}
bridge.closeWindow = () => {
  void sendCommand('window:close')
}

window.qwenDesktop = bridge
ensureExternalLinkInterception(bridge)
