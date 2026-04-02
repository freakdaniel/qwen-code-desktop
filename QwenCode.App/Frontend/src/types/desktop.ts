import type {
  DesktopStateChangedEvent,
  DesktopSessionTurnResult as GeneratedDesktopSessionTurnResult,
  QwenDesktopBridge,
} from './ipc.generated'

export type {
  ActiveTurnState,
  AdoptionPattern,
  AppBootstrapPayload,
  CancelDesktopSessionTurnRequest,
  CancelDesktopSessionTurnResult,
  CapabilityLane,
  DesktopSessionDetail,
  DesktopSessionEvent,
  DesktopSessionEntry,
  DesktopMode,
  DesktopSessionEventKind,
  DesktopStateChangedEvent,
  ExecuteNativeToolRequest,
  GetDesktopSessionRequest,
  LocaleOption,
  ProjectSummarySnapshot,
  ApprovalProfile,
  QwenCommandSurface,
  QwenCompatibilityLayer,
  QwenCompatibilitySnapshot,
  NativeToolExecutionResult,
  NativeToolHostSnapshot,
  NativeToolRegistration,
  QwenRuntimeProfile,
  RecoverableTurnState,
  ResolvedCommand,
  QwenSkillSurface,
  ToolCatalogSnapshot,
  ToolDescriptor,
  QwenSurfaceDirectory,
  ResearchTrack,
  SessionPreview,
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
