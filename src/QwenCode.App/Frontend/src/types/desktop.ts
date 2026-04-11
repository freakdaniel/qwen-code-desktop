import type {
  AppBootstrapPayload as GeneratedAppBootstrapPayload,
  DesktopQuestionAnswer,
  DesktopQuestionPrompt,
  DesktopStateChangedEvent,
  DesktopSessionEvent as GeneratedDesktopSessionEvent,
  DesktopSessionDetail as GeneratedDesktopSessionDetail,
  DesktopSessionEntry as GeneratedDesktopSessionEntry,
  QwenDesktopBridge,
  SessionPreview,
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
  DirectConnectServerState,
  DirectConnectSessionState,
  DisconnectAuthRequest,
  ExecuteNativeToolRequest,
  ExtensionDefinition,
  ExtensionSettingsSnapshot,
  ExtensionSettingValue,
  ExtensionSnapshot,
  GetChannelPairingRequest,
  GetDesktopSessionRequest,
  GetExtensionSettingsRequest,
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
  AuthStatusSnapshot,
  AvailableModel,
  RuntimeModelCapabilities,
  RuntimeModelSnapshot,
  SessionPreview,
  DesktopSessionTurnResult,
  WorkspaceSnapshot,
  GitRepositorySnapshot,
  GitWorktreeEntry,
  FileDiscoverySnapshot,
  GitCheckpointSnapshot,
  GitHistorySnapshot,
  QwenSurfaceDirectory,
  ResearchTrack,
  InstallExtensionRequest,
  RemoveExtensionRequest,
  SetExtensionEnabledRequest,
  SetExtensionSettingValueRequest,
  StartDesktopSessionTurnRequest,
} from './ipc.generated'

export type DesktopSessionEntry =
  Omit<GeneratedDesktopSessionEntry, 'thinkingDurationMs'> & {
    thinkingDurationMs?: number
  }

export type DesktopSessionDetail =
  Omit<GeneratedDesktopSessionDetail, 'session' | 'entries'> & {
    session: SessionPreview
    entries: DesktopSessionEntry[]
  }

export type DesktopSessionEvent =
  GeneratedDesktopSessionEvent & {
    toolOutput?: string
    approvalState?: string
    changedFiles?: string[]
    questions?: DesktopQuestionPrompt[]
    answers?: DesktopQuestionAnswer[]
  }

export type AppBootstrapPayload =
  GeneratedAppBootstrapPayload

export interface DesktopBridge extends Omit<QwenDesktopBridge, 'setLocale'> {
  setLocale: (locale: string) => Promise<DesktopStateChangedEvent>
  openExternalUrl?: (url: string) => Promise<boolean>
  minimizeWindow?: () => void
  maximizeWindow?: () => void
  closeWindow?: () => void
}

declare global {
  interface Window {
    qwenDesktop?: DesktopBridge
  }
}

export {}
