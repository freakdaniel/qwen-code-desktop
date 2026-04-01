import type {
  DesktopMode,
  DesktopStateChangedEvent,
  QwenDesktopBridge,
} from './ipc.generated'

export type {
  AppBootstrapPayload,
  DesktopMode,
  DesktopStateChangedEvent,
  LocaleOption,
  ResearchTrack,
  SourceMirrorPaths,
} from './ipc.generated'

export interface DesktopBridge extends Omit<QwenDesktopBridge, 'setMode' | 'setLocale'> {
  setMode: (mode: DesktopMode) => Promise<DesktopStateChangedEvent>
  setLocale: (locale: string) => Promise<DesktopStateChangedEvent>
}

declare global {
  interface Window {
    qwenDesktop?: DesktopBridge
  }
}

export {}
