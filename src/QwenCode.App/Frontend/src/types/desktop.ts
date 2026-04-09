import type {
  AppBootstrapPayload as GeneratedAppBootstrapPayload,
  AuthStatusSnapshot as GeneratedAuthStatusSnapshot,
  DesktopQuestionAnswer,
  DesktopQuestionPrompt,
  DesktopStateChangedEvent,
  DesktopSessionEvent as GeneratedDesktopSessionEvent,
  DesktopSessionEntry as GeneratedDesktopSessionEntry,
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

export interface RuntimeModelCapabilities {
  supportsEmbeddings: boolean
  supportsJsonOutput: boolean
  supportsStreaming: boolean
  supportsToolCalls: boolean
  supportsReasoning: boolean
  maxOutputTokens?: number | null
}

export interface AvailableModel {
  id: string
  authType: string
  baseUrl: string
  apiKeyEnvironmentVariable: string
  source: string
  contextWindowSize: number
  maxOutputTokens: number
  isDefaultModel: boolean
  isEmbeddingModel: boolean
  capabilities: RuntimeModelCapabilities
}

export interface RuntimeModelSnapshot {
  defaultModelId: string
  embeddingModelId: string
  selectedAuthType: string
  availableModels: AvailableModel[]
}

export type DesktopSessionEntry = GeneratedDesktopSessionEntry & {
  thinkingDurationMs?: number
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
  Omit<GeneratedAppBootstrapPayload, 'qwenAuth'> & {
    qwenAuth: AuthStatusSnapshot
    qwenModels: RuntimeModelSnapshot
  }

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
