import type {
  AppBootstrapPayload,
  DesktopMode,
} from './types/desktop'

export type ViewId = 'home' | 'search' | 'customize' | 'chats' | 'projects' | 'artifacts'

export type IconName =
  | 'menu'
  | 'split'
  | 'back'
  | 'forward'
  | 'plus'
  | 'search'
  | 'customize'
  | 'chats'
  | 'projects'
  | 'folder'
  | 'artifacts'
  | 'write'
  | 'learn'
  | 'code'
  | 'spark'
  | 'ghost'
  | 'chevronLeft'
  | 'paperclip'
  | 'settings'
  | 'wand'
  | 'cpu'

export type NavItem = {
  id: ViewId
  label: string
  icon: IconName
}

export type QuickAction = {
  label: string
  icon: IconName
}

export type LocaleCopy = {
  appLabel: string
  newChat: string
  search: string
  customize: string
  chats: string
  projects: string
  artifacts: string
  recents: string
  allConversations: string
  rootViewTitle: string
  rootViewSubtitle: string
  homeGreeting: string
  homeLead: string
  homeModeDescriptions: Record<DesktopMode, string>
  composerPlaceholder: Record<DesktopMode, string>
  modelLabel: string
  sendLabel: string
  sendingLabel: string
  sessionHostLabel: string
  sessionCreatedLabel: string
  sessionUpdatedLabel: string
  transcriptLabel: string
  quickActions: QuickAction[]
  bridgeStatus: { connected: string; local: string }
  chatSurfaceTitle: string
  chatSurfaceSubtitle: string
  emptySearch: string
  customizeTitle: string
  customizeSubtitle: string
  customizeLibraryTitle: string
  customizeDetailTitle: string
  referenceFromQwen: string
  referenceFromClaude: string
  desktopDecision: string
  deliveryState: string
  capabilityLanes: string
  responsibilities: string
  projectsTitle: string
  projectsSubtitle: string
  artifactsTitle: string
  artifactsSubtitle: string
  compatibilityGoals: string
  runtimeProfileLabel: string
  runtimeApprovalLabel: string
  toolCatalogLabel: string
  nativeHostLabel: string
  currentLocale: string
  workspaceTag: string
  modeLabel: string
  searchPlaceholder: string
  settingsLayersLabel: string
  surfaceDirectoriesLabel: string
}

const fallbackPaths = {
  workspaceRoot: '[workspace-root]',
  userQwenRoot: '[user-home]/.qwen',
  programDataRoot: '[program-data]/qwen-code',
  projectHash: '[project-hash]',
} as const

export const fallbackBootstrap: AppBootstrapPayload = {
  productName: 'Qwen Code Desktop',
  currentMode: 'code',
  currentLocale: 'en',
  locales: [
    { code: 'en', name: 'English', nativeName: 'English' },
    { code: 'ru', name: 'Russian', nativeName: 'Русский' },
  ],
  workspaceRoot: fallbackPaths.workspaceRoot,
  tracks: [],
  compatibilityGoals: [],
  capabilityLanes: [],
  adoptionPatterns: [],
  recentSessions: [],
  activeTurns: [],
  activeArenaSessions: [],
  recoverableTurns: [],
  projectSummary: {
    hasHistory: false,
    filePath: '',
    content: '',
    timestampText: '',
    timeAgo: '',
    overallGoal: '',
    currentPlan: '',
    totalTasks: 0,
    doneCount: 0,
    inProgressCount: 0,
    todoCount: 0,
    pendingTasks: [],
    timestampUtc: '0001-01-01T00:00:00Z',
  },
  qwenCompatibility: {
    projectRoot: fallbackPaths.workspaceRoot,
    defaultContextFileName: 'QWEN.md',
    settingsLayers: [],
    surfaceDirectories: [],
    commands: [],
    skills: [],
  },
  qwenRuntime: {
    projectRoot: fallbackPaths.workspaceRoot,
    globalQwenDirectory: fallbackPaths.userQwenRoot,
    runtimeBaseDirectory: fallbackPaths.userQwenRoot,
    runtimeSource: 'default-home',
    projectDataDirectory: `${fallbackPaths.userQwenRoot}/projects/${fallbackPaths.projectHash}`,
    chatsDirectory: `${fallbackPaths.userQwenRoot}/projects/${fallbackPaths.projectHash}/chats`,
    historyDirectory: `${fallbackPaths.userQwenRoot}/history/${fallbackPaths.projectHash}`,
    contextFileNames: ['QWEN.md'],
    contextFilePaths: [`${fallbackPaths.workspaceRoot}/QWEN.md`],
    folderTrustEnabled: false,
    isWorkspaceTrusted: true,
    workspaceTrustSource: '',
    approvalProfile: {
      defaultMode: 'default',
      confirmShellCommands: false,
      confirmFileEdits: false,
      allowRules: [],
      askRules: [],
      denyRules: [],
    },
    modelName: '',
    embeddingModel: '',
    chatCompression: {
      contextPercentageThreshold: null,
    },
    telemetry: {
      enabled: false,
      target: 'none',
      otlpEndpoint: '',
      otlpProtocol: '',
      logPrompts: false,
      outfile: '',
      useCollector: false,
    },
    checkpointing: false,
  },
  qwenTools: {
    sourceMode: 'native-contracts',
    totalCount: 0,
    allowedCount: 0,
    askCount: 0,
    denyCount: 0,
    tools: [],
  },
  qwenNativeHost: {
    registeredCount: 0,
    implementedCount: 0,
    readyCount: 0,
    approvalRequiredCount: 0,
    tools: [],
  },
  qwenAuth: {
    selectedType: 'openai',
    selectedScope: 'user',
    displayName: 'OpenAI-compatible',
    status: 'missing-credentials',
    model: '',
    endpoint: '',
    apiKeyEnvironmentVariable: 'OPENAI_API_KEY',
    hasApiKey: false,
    hasQwenOAuthCredentials: false,
    hasRefreshToken: false,
    credentialPath: '',
    lastError: '',
    lastAuthenticatedAtUtc: null,
    deviceFlow: null,
  },
  qwenMcp: {
    totalCount: 0,
    connectedCount: 0,
    disconnectedCount: 0,
    missingCount: 0,
    tokenCount: 0,
    servers: [],
  },
  qwenChannels: {
    isServiceRunning: false,
    serviceProcessId: null,
    serviceStartedAtUtc: '',
    serviceUptimeText: '',
    supportedTypes: [],
    channels: [],
  },
  qwenExtensions: {
    totalCount: 0,
    activeCount: 0,
    linkedCount: 0,
    missingCount: 0,
    extensions: [],
  },
  qwenWorkspace: {
    git: {
      isGitAvailable: false,
      isRepository: false,
      worktreeSupported: false,
      repositoryRoot: '',
      currentBranch: '',
      currentCommit: '',
      gitVersion: '',
      managedSessionCount: 0,
      managedWorktreesRoot: '',
      worktrees: [],
      history: {
        isInitialized: false,
        historyDirectory: '',
        checkpointCount: 0,
        currentCheckpoint: '',
        recentCheckpoints: [],
      },
    },
    discovery: {
      gitAware: false,
      hasQwenIgnore: false,
      candidateFileCount: 0,
      visibleFileCount: 0,
      gitIgnoredCount: 0,
      qwenIgnoredCount: 0,
      qwenIgnorePatternCount: 0,
      contextFiles: [],
      sampleVisibleFiles: [],
      sampleGitIgnoredFiles: [],
      sampleQwenIgnoredFiles: [],
    },
  },
}
