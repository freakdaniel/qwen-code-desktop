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
  AnswerDesktopSessionQuestionRequest,
  ApprovalProfile,
  ApproveChannelPairingRequest,
  CleanupManagedWorktreeSessionRequest,
  ConfigureCodingPlanAuthRequest,
  ConfigureOpenAiCompatibleAuthRequest,
  ConfigureQwenOAuthRequest,
  CreateGitCheckpointRequest,
  CancelDesktopSessionTurnRequest,
  CancelDesktopSessionTurnResult,
  CapabilityLane,
  ChannelDefinition,
  ChannelPairingRequest,
  ChannelPairingSnapshot,
  ChannelSnapshot,
  CreateManagedWorktreeRequest,
  DesktopQuestionAnswer,
  DesktopQuestionOption,
  DesktopQuestionPrompt,
  DisconnectAuthRequest,
  ExecuteNativeToolRequest,
  ExtensionDefinition,
  ExtensionSettingsSnapshot,
  ExtensionSettingValue,
  ExtensionSnapshot,
  GetChannelPairingRequest,
  GetDesktopSessionRequest,
  GetExtensionSettingsRequest,
  DesktopSessionDetail,
  DesktopSessionEvent,
  DesktopSessionEntry,
  DesktopMode,
  DesktopSessionEventKind,
  DesktopStateChangedEvent,
  LocaleOption,
  McpServerDefinition,
  McpServerRegistrationRequest,
  McpSnapshot,
  ProjectSummarySnapshot,
  RemoveMcpServerRequest,
  ReconnectMcpServerRequest,
  QwenCommandSurface,
  QwenCompatibilityLayer,
  QwenCompatibilitySnapshot,
  NativeToolExecutionResult,
  NativeToolHostSnapshot,
  NativeToolRegistration,
  RemoveDesktopSessionRequest,
  RemoveDesktopSessionResult,
  QwenRuntimeProfile,
  RecoverableTurnState,
  ResolvedCommand,
  RestoreGitCheckpointRequest,
  QwenSkillSurface,
  ToolCatalogSnapshot,
  ToolDescriptor,
  WorkspaceSnapshot,
  GitRepositorySnapshot,
  GitWorktreeEntry,
  FileDiscoverySnapshot,
  GitCheckpointSnapshot,
  GitHistorySnapshot,
  QwenSurfaceDirectory,
  ResearchTrack,
  SessionPreview,
  InstallExtensionRequest,
  RemoveExtensionRequest,
  SetExtensionEnabledRequest,
  SetExtensionSettingValueRequest,
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
