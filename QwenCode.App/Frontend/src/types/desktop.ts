import type {
  AppBootstrapPayload as GeneratedAppBootstrapPayload,
  AuthStatusSnapshot as GeneratedAuthStatusSnapshot,
  DesktopStateChangedEvent,
  DesktopSessionTurnResult as GeneratedDesktopSessionTurnResult,
  QwenDesktopBridge,
  QwenOAuthDeviceFlowSnapshot,
} from './ipc.generated'

export type {
  ActiveTurnState,
  AdoptionPattern,
  McpServerDefinition,
  McpServerRegistrationRequest,
  McpSnapshot,
  RemoveMcpServerRequest,
  ReconnectMcpServerRequest,
  AnswerDesktopSessionQuestionRequest,
  CancelDesktopSessionTurnRequest,
  CancelDesktopSessionTurnResult,
  CapabilityLane,
  ConfigureCodingPlanAuthRequest,
  ConfigureOpenAiCompatibleAuthRequest,
  ConfigureQwenOAuthRequest,
  DesktopQuestionAnswer,
  DesktopQuestionOption,
  DesktopQuestionPrompt,
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
  DisconnectAuthRequest,
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

export type AuthStatusSnapshot =
  Omit<GeneratedAuthStatusSnapshot, 'deviceFlow'> & {
    deviceFlow: QwenOAuthDeviceFlowSnapshot | null
  }

export type AppBootstrapPayload =
  Omit<GeneratedAppBootstrapPayload, 'qwenAuth'> & {
    qwenAuth: AuthStatusSnapshot
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
