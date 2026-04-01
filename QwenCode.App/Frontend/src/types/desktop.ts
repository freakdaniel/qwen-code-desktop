import type {
  DesktopStateChangedEvent,
  DesktopSessionTurnResult as GeneratedDesktopSessionTurnResult,
  QwenDesktopBridge,
} from './ipc.generated'

export type {
  AdoptionPattern,
  AppBootstrapPayload,
  CapabilityLane,
  DesktopSessionDetail,
  DesktopSessionEntry,
  DesktopMode,
  DesktopStateChangedEvent,
  ExecuteNativeToolRequest,
  GetDesktopSessionRequest,
  LocaleOption,
  QwenApprovalProfile,
  QwenCommandSurface,
  QwenCompatibilityLayer,
  QwenCompatibilitySnapshot,
  QwenNativeToolExecutionResult,
  QwenNativeToolHostSnapshot,
  QwenNativeToolRegistration,
  QwenRuntimeProfile,
  QwenResolvedCommand,
  QwenSkillSurface,
  QwenToolCatalogSnapshot,
  QwenToolDescriptor,
  QwenSurfaceDirectory,
  ResearchTrack,
  RuntimePortWorkItem,
  SessionPreview,
  SourceMirrorPaths,
  SourceMirrorStatus,
  StartDesktopSessionTurnRequest,
} from './ipc.generated'

export type DesktopSessionTurnResult =
  Omit<GeneratedDesktopSessionTurnResult, 'resolvedCommand'> & {
    resolvedCommand: GeneratedDesktopSessionTurnResult['resolvedCommand'] | null
  }

export interface DesktopBridge extends Omit<QwenDesktopBridge, 'setLocale'> {
  setLocale: (locale: string) => Promise<DesktopStateChangedEvent>
}

declare global {
  interface Window {
    qwenDesktop?: DesktopBridge
  }
}

export {}
