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
  currentLocale: 'ru',
  locales: [
    { code: 'en', name: 'English', nativeName: 'English' },
    { code: 'ru', name: 'Russian', nativeName: 'Р В РЎС“РЎРѓРЎРѓР С”Р С‘Р в„–' },
    { code: 'zh-CN', name: 'Chinese', nativeName: 'Р·В®Р‚РґР…вЂњРґС‘В­Р¶вЂ“вЂЎ' },
    { code: 'de', name: 'German', nativeName: 'Deutsch' },
    { code: 'fr', name: 'French', nativeName: 'Francais' },
    { code: 'es', name: 'Spanish', nativeName: 'Espanol' },
    { code: 'ja', name: 'Japanese', nativeName: 'Р¶вЂ”ТђР¶СљВ¬РёР„С›' },
    { code: 'ko', name: 'Korean', nativeName: 'РЅвЂўСљРєВµВ­РјвЂ“Т‘' },
    { code: 'pt-BR', name: 'Portuguese (Brazil)', nativeName: 'PortuguР“Р„s (Brasil)' },
    { code: 'tr', name: 'Turkish', nativeName: 'TР“СrkР“В§e' },
    { code: 'ar', name: 'Arabic', nativeName: 'РЁВ§Р©вЂћРЁв„–РЁВ±РЁРЃР©Р‰РЁВ©' },
  ],
  workspaceRoot: fallbackPaths.workspaceRoot,
  tracks: [
    {
      title: 'Lift qwen core behind a native session host',
      summary:
        'The desktop backend should own orchestration, but the model loop, tools, history, and policy logic must stay source-compatible with qwen.',
    },
    {
      title: 'Drive sessions through native desktop ergonomics',
      summary:
        'Desktop workspaces should expose sessions, approvals, and activity as first-class GUI concepts instead of terminal-only state.',
    },
    {
      title: 'Keep the Electron bridge narrow and typed',
      summary:
        'Typed preload contracts let the shell evolve without leaking native concerns into React components.',
    },
  ],
  compatibilityGoals: [
    'Do not shell out to qwen CLI for core execution paths.',
    'Keep .qwen-compatible settings, memory, session, and tool semantics.',
    'Keep every production runtime contract inside this codebase.',
    'Make desktop-specific concerns explicit: windows, trays, approvals, attachments, and session chrome.',
  ],
  capabilityLanes: [
    {
      title: 'Qwen runtime lane',
      summary:
        'Owns prompt construction, tool registry, session state, settings, and compatibility with qwen workflows.',
      responsibilities: [
        'Model API integration and turn execution',
        'Tool calling, sandbox policy, and approvals',
        'History, memory, slash commands, and settings compatibility',
      ],
    },
    {
      title: 'Desktop bridge lane',
      summary:
        'Owns native session hosting, IPC, attachment plumbing, notifications, and cross-window coordination.',
      responsibilities: [
        'Session bootstrap and reconnect semantics',
        'Typed IPC between .NET host and renderer',
        'Native file pickers, desktop prompts, and platform integrations',
      ],
    },
    {
      title: 'Renderer workspace lane',
      summary:
        'Owns the high-level interaction model: sidebar navigation, code-first desktop surfaces, approvals, and task visibility.',
      responsibilities: [
        'Home, sessions, customize, projects, and artifacts surfaces',
        'Single code-mode composer and conversation chrome',
        'Tool timeline, approval panels, and architecture guidance',
      ],
    },
  ],
  adoptionPatterns: [
    {
      area: 'Execution engine',
      qwenSource:
        'Native runtime foundations already own slash commands, session writes, and qwen-compatible policy handling.',
      claudeReference:
        'The remaining gap is a provider-backed model loop with token streaming and tool orchestration.',
      desktopDirection:
        'Build a native host around qwen core primitives, not a wrapper around qwen CLI stdout.',
      deliveryState: 'Foundation',
    },
    {
      area: 'Session lifecycle',
      qwenSource:
        'Transcript persistence, approvals, and resume flows already live inside the native session engine.',
      claudeReference:
        'The current focus is turning those flows into a richer live desktop workspace with reconnect and activity surfaces.',
      desktopDirection:
        'Promote sessions to first-class desktop objects with reconnect, activity, and branch/worktree awareness.',
      deliveryState: 'High priority',
    },
    {
      area: 'Approvals and tools',
      qwenSource:
        'Approval modes and sandbox policies already exist in qwen and should be preserved.',
      claudeReference:
        'Permission requests, task state, and tool activity should stay visible in dedicated desktop surfaces.',
      desktopDirection:
        'Move approvals into explicit desktop panels while keeping qwen policy logic intact.',
      deliveryState: 'High priority',
    },
    {
      area: 'Context surfaces',
      qwenSource:
        'Settings, memory, slash commands, and project context are already well-defined.',
      claudeReference:
        'The desktop shell still needs more discoverable surfaces for connectors, scheduled work, and project context.',
      desktopDirection:
        'Expose qwen capabilities as browseable desktop surfaces rather than hidden CLI-only concepts.',
      deliveryState: 'In design',
    },
    {
      area: 'IPC discipline',
      qwenSource:
        'Core/CLI separation means renderer-specific contracts should stay outside the engine.',
      claudeReference:
        'Typed bridge contracts keep desktop traffic structured and evolvable as the runtime grows.',
      desktopDirection:
        'Keep Electron preload thin and strongly typed so backend evolution does not leak into UI code.',
      deliveryState: 'Implemented',
    },
  ],
  recentSessions: [
    {
      sessionId: 'desktop-parity-audit',
      title: 'Desktop parity audit',
      lastActivity: 'Updated 18 minutes ago',
      category: 'Architecture',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: fallbackPaths.workspaceRoot,
      gitBranch: 'main',
      messageCount: 14,
      transcriptPath: `${fallbackPaths.workspaceRoot}/.qwen/chats/desktop-parity-audit.jsonl`,
    },
    {
      sessionId: 'claude-session-bridge-mapping',
      title: 'Claude session bridge mapping',
      lastActivity: 'Updated 44 minutes ago',
      category: 'Customize',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: fallbackPaths.workspaceRoot,
      gitBranch: 'main',
      messageCount: 11,
      transcriptPath: `${fallbackPaths.workspaceRoot}/.qwen/chats/claude-session-bridge-mapping.jsonl`,
    },
    {
      sessionId: 'qwen-core-host-extraction',
      title: 'qwen core host extraction',
      lastActivity: 'Updated 2 hours ago',
      category: 'Code',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: fallbackPaths.workspaceRoot,
      gitBranch: 'main',
      messageCount: 22,
      transcriptPath: `${fallbackPaths.workspaceRoot}/.qwen/chats/qwen-core-host-extraction.jsonl`,
    },
    {
      sessionId: 'approval-panel-behaviors',
      title: 'Approval panel behaviors',
      lastActivity: 'Updated yesterday',
      category: 'UX',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: fallbackPaths.workspaceRoot,
      gitBranch: 'main',
      messageCount: 8,
      transcriptPath: `${fallbackPaths.workspaceRoot}/.qwen/chats/approval-panel-behaviors.jsonl`,
    },
    {
      sessionId: 'workspace-source-mirror-review',
      title: 'Workspace compatibility review',
      lastActivity: 'Updated 2 days ago',
      category: 'Research',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: fallbackPaths.workspaceRoot,
      gitBranch: 'main',
      messageCount: 5,
      transcriptPath: `${fallbackPaths.workspaceRoot}/.qwen/chats/workspace-source-mirror-review.jsonl`,
    },
  ],
  activeTurns: [],
  activeArenaSessions: [],
  recoverableTurns: [],
  projectSummary: {
    hasHistory: false,
    filePath: `${fallbackPaths.workspaceRoot}/.qwen/PROJECT_SUMMARY.md`,
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
    settingsLayers: [
      {
        id: 'system-defaults',
        title: 'System defaults',
        scope: 'system defaults',
        priority: 2,
        path: `${fallbackPaths.programDataRoot}/system-defaults.json`,
        exists: false,
        categories: [],
      },
      {
        id: 'user-settings',
        title: 'User settings',
        scope: 'user',
        priority: 3,
        path: `${fallbackPaths.userQwenRoot}/settings.json`,
        exists: false,
        categories: [],
      },
      {
        id: 'project-settings',
        title: 'Project settings',
        scope: 'project',
        priority: 4,
        path: `${fallbackPaths.workspaceRoot}/.qwen/settings.json`,
        exists: false,
        categories: [],
      },
      {
        id: 'system-settings',
        title: 'System settings',
        scope: 'system override',
        priority: 5,
        path: `${fallbackPaths.programDataRoot}/settings.json`,
        exists: false,
        categories: [],
      },
    ],
    surfaceDirectories: [
      {
        id: 'project-commands',
        title: 'Project commands',
        path: `${fallbackPaths.workspaceRoot}/.qwen/commands`,
        exists: false,
        itemCount: 0,
        summary: 'Slash-command markdown and command surfaces. Not found yet.',
      },
      {
        id: 'project-skills',
        title: 'Project skills',
        path: `${fallbackPaths.workspaceRoot}/.qwen/skills`,
        exists: false,
        itemCount: 0,
        summary: 'Project-local skills stored as directories with SKILL.md. Not found yet.',
      },
      {
        id: 'user-skills',
        title: 'User skills',
        path: `${fallbackPaths.userQwenRoot}/skills`,
        exists: false,
        itemCount: 0,
        summary: 'User-level skill surface shared across projects. Not found yet.',
      },
      {
        id: 'context-root',
        title: 'Workspace context file',
        path: `${fallbackPaths.workspaceRoot}/QWEN.md`,
        exists: false,
        itemCount: 0,
        summary: 'Default project instruction context file. Not found yet.',
      },
    ],
    commands: [
      {
        id: 'project:qc/code-review.md',
        name: 'qc/code-review',
        scope: 'project',
        path: `${fallbackPaths.workspaceRoot}/.qwen/commands/qc/code-review.md`,
        description: 'Review a pull request with qwen-native guidance.',
        group: 'qc',
      },
      {
        id: 'user:team/release.md',
        name: 'team/release',
        scope: 'user',
        path: `${fallbackPaths.userQwenRoot}/commands/team/release.md`,
        description: 'Prepare a release note draft.',
        group: 'team',
      },
    ],
    skills: [
      {
        id: 'project:project-review',
        name: 'project-review',
        scope: 'project',
        path: `${fallbackPaths.workspaceRoot}/.qwen/skills/project-review/SKILL.md`,
        description: 'Review project changes with local context.',
        allowedTools: ['read_file', 'grep_search'],
      },
      {
        id: 'user:user-skill',
        name: 'user-skill',
        scope: 'user',
        path: `${fallbackPaths.userQwenRoot}/skills/user-skill/SKILL.md`,
        description: 'A user-level reusable skill.',
        allowedTools: [],
      },
    ],
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
      confirmShellCommands: true,
      confirmFileEdits: true,
      allowRules: ['Bash(git *)', 'Read'],
      askRules: ['Edit'],
      denyRules: ['Read(.env)'],
    },
  },
  qwenTools: {
    sourceMode: 'native-contracts',
    totalCount: 4,
    allowedCount: 2,
    askCount: 2,
    denyCount: 0,
    tools: [
      {
        name: 'read_file',
        displayName: 'ReadFile',
        kind: 'read',
        sourcePath: 'native://tools/read_file',
        approvalState: 'allow',
        approvalReason: 'Allowed by explicit compatibility rule.',
      },
      {
        name: 'edit',
        displayName: 'Edit',
        kind: 'modify',
        sourcePath: 'native://tools/edit',
        approvalState: 'ask',
        approvalReason: 'Requires confirmation due to explicit ask rule.',
      },
      {
        name: 'run_shell_command',
        displayName: 'Shell',
        kind: 'execute',
        sourcePath: 'native://tools/run_shell_command',
        approvalState: 'ask',
        approvalReason: 'Requires confirmation in default mode.',
      },
      {
        name: 'agent',
        displayName: 'Agent',
        kind: 'coordination',
        sourcePath: 'native://tools/agent',
        approvalState: 'ask',
        approvalReason: 'Requires confirmation in default mode.',
      },
    ],
  },
  qwenNativeHost: {
    registeredCount: 7,
    implementedCount: 7,
    readyCount: 4,
    approvalRequiredCount: 3,
    tools: [
      {
        name: 'edit',
        displayName: 'Edit',
        kind: 'modify',
        isImplemented: true,
        approvalState: 'ask',
        approvalReason: 'Requires confirmation due to explicit ask rule.',
      },
      {
        name: 'glob',
        displayName: 'Glob',
        kind: 'read',
        isImplemented: true,
        approvalState: 'allow',
        approvalReason: 'Allowed by explicit compatibility rule.',
      },
      {
        name: 'grep_search',
        displayName: 'Grep',
        kind: 'read',
        isImplemented: true,
        approvalState: 'allow',
        approvalReason: 'Allowed by explicit compatibility rule.',
      },
      {
        name: 'list_directory',
        displayName: 'ListFiles',
        kind: 'read',
        isImplemented: true,
        approvalState: 'allow',
        approvalReason: 'Allowed by explicit compatibility rule.',
      },
      {
        name: 'read_file',
        displayName: 'ReadFile',
        kind: 'read',
        isImplemented: true,
        approvalState: 'allow',
        approvalReason: 'Allowed by explicit compatibility rule.',
      },
      {
        name: 'run_shell_command',
        displayName: 'Shell',
        kind: 'execute',
        isImplemented: true,
        approvalState: 'ask',
        approvalReason: 'Requires confirmation in default mode.',
      },
      {
        name: 'write_file',
        displayName: 'WriteFile',
        kind: 'modify',
        isImplemented: true,
        approvalState: 'ask',
        approvalReason: 'Requires confirmation due to explicit ask rule.',
      },
    ],
  },
  qwenAuth: {
    selectedType: 'openai',
    selectedScope: 'user',
    displayName: 'OpenAI-compatible',
    status: 'missing-credentials',
    model: 'qwen3-coder-plus',
    endpoint: 'https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions',
    apiKeyEnvironmentVariable: 'OPENAI_API_KEY',
    hasApiKey: false,
    hasQwenOAuthCredentials: false,
    hasRefreshToken: false,
    credentialPath: `${fallbackPaths.userQwenRoot}/oauth_creds.json`,
    lastError: 'No auth has been configured yet.',
    lastAuthenticatedAtUtc: null,
    deviceFlow: null,
  },
  qwenMcp: {
    totalCount: 2,
    connectedCount: 1,
    disconnectedCount: 1,
    missingCount: 0,
    tokenCount: 1,
    servers: [
      {
        name: 'docs',
        scope: 'user',
        transport: 'stdio',
        commandOrUrl: 'node docs-mcp.js',
        arguments: [],
        environmentVariables: {},
        headers: {},
        timeoutMs: 2000,
        trust: false,
        description: 'Local documentation helper',
        includeTools: ['fetch-docs'],
        excludeTools: [],
        settingsPath: `${fallbackPaths.userQwenRoot}/settings.json`,
        status: 'connected',
        lastReconnectAttemptUtc: '2026-04-02T09:15:00Z',
        lastError: '',
        hasPersistedToken: false,
        discoveredToolsCount: 3,
        discoveredPromptsCount: 1,
        supportsPrompts: true,
        supportsResources: true,
        lastDiscoveryUtc: '2026-04-02T09:15:05Z',
      },
      {
        name: 'search',
        scope: 'project',
        transport: 'http',
        commandOrUrl: 'https://example.com/mcp',
        arguments: [],
        environmentVariables: {},
        headers: {},
        timeoutMs: 2000,
        trust: false,
        description: 'Project search endpoint',
        includeTools: [],
        excludeTools: [],
        settingsPath: `${fallbackPaths.workspaceRoot}/.qwen/settings.json`,
        status: 'disconnected',
        lastReconnectAttemptUtc: '2026-04-02T09:12:00Z',
        lastError: 'Connection refused',
        hasPersistedToken: true,
        discoveredToolsCount: 0,
        discoveredPromptsCount: 0,
        supportsPrompts: false,
        supportsResources: false,
        lastDiscoveryUtc: null,
      },
    ],
  },
  qwenChannels: {
    isServiceRunning: false,
    serviceProcessId: null,
    serviceStartedAtUtc: '',
    serviceUptimeText: '',
    supportedTypes: ['telegram', 'weixin', 'dingtalk'],
    channels: [
      {
        name: 'team-telegram',
        type: 'telegram',
        scope: 'project',
        description: 'Incoming engineering support channel',
        senderPolicy: 'pairing',
        sessionScope: 'thread',
        workingDirectory: fallbackPaths.workspaceRoot,
        approvalMode: 'plan',
        model: 'qwen-max',
        status: 'configured',
        supportsPairing: true,
        sessionCount: 0,
        pendingPairingCount: 1,
        allowlistCount: 2,
      },
    ],
  },
  qwenExtensions: {
    totalCount: 2,
    activeCount: 1,
    linkedCount: 1,
    missingCount: 0,
    extensions: [
      {
        name: 'workspace-toolbelt',
        version: '1.0.0',
        path: `${fallbackPaths.userQwenRoot}/extensions/workspace-toolbelt`,
        wrapperPath: `${fallbackPaths.userQwenRoot}/extensions/workspace-toolbelt`,
        status: 'active',
        installType: 'local',
        source: `${fallbackPaths.userQwenRoot}/extensions/workspace-toolbelt`,
        userEnabled: true,
        workspaceEnabled: true,
        isActive: true,
        description: 'Workspace-local command and utility bundle',
        contextFiles: ['QWEN.md'],
        commands: ['review:changes', 'plan:next-step'],
        skills: ['workspace-review'],
        agents: ['code-worker'],
        mcpServers: ['workspace-files'],
        channels: [],
        settingsCount: 1,
        hookEventCount: 2,
        lastError: '',
      },
      {
        name: 'linked-debug-kit',
        version: '0.4.0',
        path: `${fallbackPaths.workspaceRoot}/extensions/linked-debug-kit`,
        wrapperPath: `${fallbackPaths.userQwenRoot}/extensions/linked-debug-kit`,
        status: 'disabled',
        installType: 'link',
        source: `${fallbackPaths.workspaceRoot}/extensions/linked-debug-kit`,
        userEnabled: true,
        workspaceEnabled: false,
        isActive: false,
        description: 'Linked extension for debugging workflows',
        contextFiles: ['AGENTS.md'],
        commands: ['debug:triage'],
        skills: ['debugging-playbook'],
        agents: [],
        mcpServers: [],
        channels: ['telegram'],
        settingsCount: 0,
        hookEventCount: 1,
        lastError: '',
      },
    ],
  },
  qwenWorkspace: {
    git: {
      isGitAvailable: true,
      isRepository: true,
      worktreeSupported: true,
      repositoryRoot: fallbackPaths.workspaceRoot,
      currentBranch: 'main',
      currentCommit: 'f1a2b3c4d5e6f7a8b9c0',
      gitVersion: 'git version 2.53.0.windows.1',
      managedSessionCount: 1,
      managedWorktreesRoot: `${fallbackPaths.userQwenRoot}/worktrees`,
      worktrees: [
        {
          path: `${fallbackPaths.userQwenRoot}/worktrees/session-123/worktrees/code-review`,
          branch: 'main-session-123-code-review',
          head: 'f1a2b3c4d5e6f7a8b9c0',
          name: 'code-review',
          sessionId: 'session-123',
          isCurrent: false,
          isManaged: true,
        },
        {
          path: fallbackPaths.workspaceRoot,
          branch: 'main',
          head: 'f1a2b3c4d5e6f7a8b9c0',
          name: 'qwen-code-desktop',
          sessionId: '',
          isCurrent: true,
          isManaged: false,
        },
      ],
    },
    discovery: {
      gitAware: true,
      hasQwenIgnore: true,
      candidateFileCount: 184,
      visibleFileCount: 142,
      gitIgnoredCount: 31,
      qwenIgnoredCount: 11,
      qwenIgnorePatternCount: 4,
      contextFiles: ['QWEN.md', 'AGENTS.md'],
      sampleVisibleFiles: [
        'QwenCode.App/Program.cs',
        'QwenCode.App/AppHost/Bootstrapper.cs',
        'QwenCode.App/Runtime/Orchestration/AssistantTurnRuntime.cs',
      ],
      sampleGitIgnoredFiles: ['QwenCode.App/bin/Debug/net10.0/win-x64/QwenCode.App.dll'],
      sampleQwenIgnoredFiles: ['docs/private-notes.md'],
    },
  },
}

const copyByLanguage: Record<'en' | 'ru', LocaleCopy> = {
  en: {
    appLabel: 'Desktop shell',
    newChat: 'New session',
    search: 'Search',
    customize: 'Customize',
    chats: 'Sessions',
    projects: 'Projects',
    artifacts: 'Artifacts',
    recents: 'Recents',
    allConversations: 'Your code sessions with Qwen',
    rootViewTitle: 'Qwen-first desktop workspace',
    rootViewSubtitle:
      'A native desktop shell for Qwen built around a fully local .NET runtime and a typed React workspace.',
    homeGreeting: 'Good evening, Daniel',
    homeLead: 'How can I help you move the current runtime and GUI toward a fully native desktop product?',
    homeModeDescriptions: {
      code: 'Shape backend extraction, IPC contracts, renderer behavior, and session hosting.',
    },
    composerPlaceholder: {
      code: 'Ask for backend extraction, IPC work, renderer implementation, or qwen compatibility hardening.',
    },
    modelLabel: 'Qwen Max Preview',
    sendLabel: 'Send',
    sendingLabel: 'Starting turn...',
    sessionHostLabel: 'Native session host',
    sessionCreatedLabel: 'Session created',
    sessionUpdatedLabel: 'Session updated',
    transcriptLabel: 'Transcript',
    quickActions: [
      { label: 'Write', icon: 'write' },
      { label: 'Learn', icon: 'learn' },
      { label: 'Code', icon: 'code' },
      { label: 'Runtime plan', icon: 'spark' },
    ],
    bridgeStatus: { connected: 'IPC attached', local: 'Local preview' },
    chatSurfaceTitle: 'Sessions',
    chatSurfaceSubtitle:
      'Keep implementation work, approvals, and native runtime sessions visible in one desktop index.',
    emptySearch: 'Nothing matched this search yet.',
    customizeTitle: 'Customize',
    customizeSubtitle:
      'Use internal module diagnostics and adoption patterns to decide what belongs in the native runtime, host, and renderer.',
    customizeLibraryTitle: 'Architecture library',
    customizeDetailTitle: 'Pattern details',
    referenceFromQwen: 'Native baseline',
    referenceFromClaude: 'Decision rationale',
    desktopDecision: 'Desktop direction',
    deliveryState: 'Delivery state',
    capabilityLanes: 'Delivery lanes',
    responsibilities: 'Responsibilities',
    projectsTitle: 'Projects',
    projectsSubtitle:
      'Group the runtime, native host, and renderer as separate but coordinated workstreams.',
    artifactsTitle: 'Artifacts',
    artifactsSubtitle:
      'Turn architecture decisions into visible desktop deliverables instead of terminal-only conventions.',
    compatibilityGoals: 'Compatibility goals',
    runtimeProfileLabel: 'Runtime profile',
    runtimeApprovalLabel: 'Approval profile',
    toolCatalogLabel: 'Tool catalog',
    nativeHostLabel: 'Native host',
    currentLocale: 'Locale',
    workspaceTag: 'Workspace',
    modeLabel: 'Surface',
    searchPlaceholder: 'Search sessions, patterns, and compatibility surfaces...',
    settingsLayersLabel: 'Settings layers',
    surfaceDirectoriesLabel: 'Compatibility surfaces',
  },
  ru: {
    appLabel: 'Desktop shell',
    newChat: 'Р СњР С•Р Р†Р В°РЎРЏ РЎРѓР ВµРЎРѓРЎРѓР С‘РЎРЏ',
    search: 'Р СџР С•Р С‘РЎРѓР С”',
    customize: 'Customize',
    chats: 'Р РЋР ВµРЎРѓРЎРѓР С‘Р С‘',
    projects: 'Р СџРЎР‚Р С•Р ВµР С”РЎвЂљРЎвЂ№',
    artifacts: 'Р С’РЎР‚РЎвЂљР ВµРЎвЂћР В°Р С”РЎвЂљРЎвЂ№',
    recents: 'Р СњР ВµР Т‘Р В°Р Р†Р Р…Р С‘Р Вµ',
    allConversations: 'Р вЂ™Р В°РЎв‚¬Р С‘ Р С”Р С•Р Т‘Р С•Р Р†РЎвЂ№Р Вµ РЎРѓР ВµРЎРѓРЎРѓР С‘Р С‘ РЎРѓ Qwen',
    rootViewTitle: 'Qwen-first desktop workspace',
    rootViewSubtitle:
      'Р СњР В°РЎвЂљР С‘Р Р†Р Р…Р В°РЎРЏ Р С•Р В±Р С•Р В»Р С•РЎвЂЎР С”Р В° Р Р…Р В°Р Т‘ qwen-code, Р С”Р С•РЎвЂљР С•РЎР‚Р В°РЎРЏ Р В·Р В°Р С‘Р СРЎРѓРЎвЂљР Р†РЎС“Р ВµРЎвЂљ РЎРѓР С‘Р В»РЎРЉР Р…РЎвЂ№Р Вµ desktop-Р С—Р В°РЎвЂљРЎвЂљР ВµРЎР‚Р Р…РЎвЂ№ Claude Р В±Р ВµР В· Р В·Р В°Р Р†Р С‘РЎРѓР С‘Р СР С•РЎРѓРЎвЂљР С‘ Р С•РЎвЂљ Р С‘РЎвЂ¦ CLI.',
    homeGreeting: 'Р вЂќР С•Р В±РЎР‚РЎвЂ№Р в„– Р Р†Р ВµРЎвЂЎР ВµРЎР‚, Daniel',
    homeLead: 'Р В§Р ВµР С Р С—Р С•Р СР С•РЎвЂЎРЎРЉ РЎРѓ Р С—Р ВµРЎР‚Р ВµР Р…Р С•РЎРѓР С•Р С qwen-code Р С‘Р В· terminal shell Р Р† Р С—Р С•Р В»Р Р…Р С•РЎвЂ Р ВµР Р…Р Р…РЎвЂ№Р в„– desktop-Р С—РЎР‚Р С•Р Т‘РЎС“Р С”РЎвЂљ?',
    homeModeDescriptions: {
      code: 'Р В Р ВµР В°Р В»Р С‘Р В·Р В°РЎвЂ Р С‘РЎРЏ backend extraction, IPC-Р С”Р С•Р Р…РЎвЂљРЎР‚Р В°Р С”РЎвЂљР С•Р Р†, renderer-Р С—Р С•Р Р†Р ВµР Т‘Р ВµР Р…Р С‘РЎРЏ Р С‘ session hosting.',
    },
    composerPlaceholder: {
      code: 'Р СџР С•Р С—РЎР‚Р С•РЎРѓР С‘ РЎР‚Р ВµР В°Р В»Р С‘Р В·Р В°РЎвЂ Р С‘РЎР‹ backend extraction, IPC, renderer Р С‘Р В»Р С‘ compatibility hardening Р С—Р С•Р Р†Р ВµРЎР‚РЎвЂ¦ qwen.',
    },
    modelLabel: 'Qwen Max Preview',
    sendLabel: 'Р С›РЎвЂљР С—РЎР‚Р В°Р Р†Р С‘РЎвЂљРЎРЉ',
    sendingLabel: 'Р вЂ”Р В°Р С—РЎС“РЎРѓР С”Р В°РЎР‹ turn...',
    sessionHostLabel: 'Р СњР В°РЎвЂљР С‘Р Р†Р Р…РЎвЂ№Р в„– session host',
    sessionCreatedLabel: 'Р РЋР ВµРЎРѓРЎРѓР С‘РЎРЏ РЎРѓР С•Р В·Р Т‘Р В°Р Р…Р В°',
    sessionUpdatedLabel: 'Р РЋР ВµРЎРѓРЎРѓР С‘РЎРЏ Р С•Р В±Р Р…Р С•Р Р†Р В»Р ВµР Р…Р В°',
    transcriptLabel: 'Р СћРЎР‚Р В°Р Р…РЎРѓР С”РЎР‚Р С‘Р С—РЎвЂљ',
    quickActions: [
      { label: 'Р СџР С‘РЎРѓР В°РЎвЂљРЎРЉ', icon: 'write' },
      { label: 'Р Р€РЎвЂЎР С‘РЎвЂљРЎРЉРЎРѓРЎРЏ', icon: 'learn' },
      { label: 'Р С™Р С•Р Т‘', icon: 'code' },
      { label: 'Р СџР В°РЎвЂљРЎвЂљР ВµРЎР‚Р Р…РЎвЂ№ Claude', icon: 'spark' },
    ],
    bridgeStatus: { connected: 'IPC Р С—Р С•Р Т‘Р С”Р В»РЎР‹РЎвЂЎР ВµР Р…', local: 'Р вЂєР С•Р С”Р В°Р В»РЎРЉР Р…РЎвЂ№Р в„– Р С—РЎР‚Р ВµР Р†РЎРЉРЎР‹-РЎР‚Р ВµР В¶Р С‘Р С' },
    chatSurfaceTitle: 'Р РЋР ВµРЎРѓРЎРѓР С‘Р С‘',
    chatSurfaceSubtitle:
      'Р вЂќР ВµРЎР‚Р В¶Р С‘РЎвЂљР Вµ implementation-Р В·Р В°Р Т‘Р В°РЎвЂЎР С‘, approvals Р С‘ Р Р…Р В°РЎвЂљР С‘Р Р†Р Р…РЎвЂ№Р Вµ runtime-РЎРѓР ВµРЎРѓРЎРѓР С‘Р С‘ Р Р† Р С•Р Т‘Р Р…Р С•Р С desktop-Р С‘Р Р…Р Т‘Р ВµР С”РЎРѓР Вµ.',
    emptySearch: 'Р СџР С• РЎРЊРЎвЂљР С•Р СРЎС“ Р В·Р В°Р С—РЎР‚Р С•РЎРѓРЎС“ Р С—Р С•Р С”Р В° Р Р…Р С‘РЎвЂЎР ВµР С–Р С• Р Р…Р Вµ Р Р…Р В°РЎв‚¬Р В»Р С•РЎРѓРЎРЉ.',
    customizeTitle: 'Customize',
    customizeSubtitle:
      'Р ВРЎРѓР С—Р С•Р В»РЎРЉР В·РЎС“Р в„–РЎвЂљР Вµ runtime surfaces Р С‘ adoption patterns, РЎвЂЎРЎвЂљР С•Р В±РЎвЂ№ РЎР‚Р В°Р В·Р Т‘Р ВµР В»Р С‘РЎвЂљРЎРЉ Р С•РЎвЂљР Р†Р ВµРЎвЂљРЎРѓРЎвЂљР Р†Р ВµР Р…Р Р…Р С•РЎРѓРЎвЂљРЎРЉ Р СР ВµР В¶Р Т‘РЎС“ qwen core, native host Р С‘ renderer.',
    customizeLibraryTitle: 'Р вЂР С‘Р В±Р В»Р С‘Р С•РЎвЂљР ВµР С”Р В° Р С—Р В°РЎвЂљРЎвЂљР ВµРЎР‚Р Р…Р С•Р Р†',
    customizeDetailTitle: 'Р вЂќР ВµРЎвЂљР В°Р В»Р С‘ Р С—Р В°РЎвЂљРЎвЂљР ВµРЎР‚Р Р…Р В°',
    referenceFromQwen: 'Р ВР В· qwen-code',
    referenceFromClaude: 'Р ВР В· claude-code',
    desktopDecision: 'Р В Р ВµРЎв‚¬Р ВµР Р…Р С‘Р Вµ Р Т‘Р В»РЎРЏ desktop',
    deliveryState: 'Р РЋР С•РЎРѓРЎвЂљР С•РЎРЏР Р…Р С‘Р Вµ РЎР‚Р ВµР В°Р В»Р С‘Р В·Р В°РЎвЂ Р С‘Р С‘',
    capabilityLanes: 'Р В Р В°Р В±Р С•РЎвЂЎР С‘Р Вµ Р С”Р С•Р Р…РЎвЂљРЎС“РЎР‚РЎвЂ№',
    responsibilities: 'Р С›РЎвЂљР Р†Р ВµРЎвЂљРЎРѓРЎвЂљР Р†Р ВµР Р…Р Р…Р С•РЎРѓРЎвЂљР С‘',
    projectsTitle: 'Р СџРЎР‚Р С•Р ВµР С”РЎвЂљРЎвЂ№',
    projectsSubtitle:
      'Р В Р В°Р В·Р Т‘Р ВµР В»РЎРЏР в„–РЎвЂљР Вµ runtime, native host Р С‘ renderer Р Р…Р В° Р С•РЎвЂљР Т‘Р ВµР В»РЎРЉР Р…РЎвЂ№Р Вµ, Р Р…Р С• РЎРѓР С‘Р Р…РЎвЂ¦РЎР‚Р С•Р Р…Р С‘Р В·Р С‘РЎР‚Р С•Р Р†Р В°Р Р…Р Р…РЎвЂ№Р Вµ Р Р…Р В°Р С—РЎР‚Р В°Р Р†Р В»Р ВµР Р…Р С‘РЎРЏ РЎР‚Р В°Р В±Р С•РЎвЂљРЎвЂ№.',
    artifactsTitle: 'Р С’РЎР‚РЎвЂљР ВµРЎвЂћР В°Р С”РЎвЂљРЎвЂ№',
    artifactsSubtitle:
      'Р СџРЎР‚Р ВµР Р†РЎР‚Р В°РЎвЂ°Р В°Р в„–РЎвЂљР Вµ Р В°РЎР‚РЎвЂ¦Р С‘РЎвЂљР ВµР С”РЎвЂљРЎС“РЎР‚Р Р…РЎвЂ№Р Вµ РЎР‚Р ВµРЎв‚¬Р ВµР Р…Р С‘РЎРЏ Р Р† РЎРЏР Р†Р Р…РЎвЂ№Р Вµ desktop-Р В°РЎР‚РЎвЂљР ВµРЎвЂћР В°Р С”РЎвЂљРЎвЂ№, Р В° Р Р…Р Вµ Р Р† РЎРѓР С”РЎР‚РЎвЂ№РЎвЂљРЎвЂ№Р Вµ terminal-Р С”Р С•Р Р…Р Р†Р ВµР Р…РЎвЂ Р С‘Р С‘.',
    compatibilityGoals: 'Р В¦Р ВµР В»Р С‘ РЎРѓР С•Р Р†Р СР ВµРЎРѓРЎвЂљР С‘Р СР С•РЎРѓРЎвЂљР С‘',
    runtimeProfileLabel: 'Runtime profile',
    runtimeApprovalLabel: 'Approval profile',
    toolCatalogLabel: 'Р С™Р В°РЎвЂљР В°Р В»Р С•Р С– Р С‘Р Р…РЎРѓРЎвЂљРЎР‚РЎС“Р СР ВµР Р…РЎвЂљР С•Р Р†',
    nativeHostLabel: 'Р СњР В°РЎвЂљР С‘Р Р†Р Р…РЎвЂ№Р в„– host',
    currentLocale: 'Р Р‡Р В·РЎвЂ№Р С”',
    workspaceTag: 'Workspace',
    modeLabel: 'Р СџР С•Р Р†Р ВµРЎР‚РЎвЂ¦Р Р…Р С•РЎРѓРЎвЂљРЎРЉ',
    searchPlaceholder: 'Р СџР С•Р С‘РЎРѓР С” Р С—Р С• РЎРѓР ВµРЎРѓРЎРѓР С‘РЎРЏР С, Р С—Р В°РЎвЂљРЎвЂљР ВµРЎР‚Р Р…Р В°Р С Р С‘ Р С—Р С•Р Р†Р ВµРЎР‚РЎвЂ¦Р Р…Р С•РЎРѓРЎвЂљРЎРЏР С...',
    settingsLayersLabel: 'Р РЋР В»Р С•Р С‘ Р Р…Р В°РЎРѓРЎвЂљРЎР‚Р С•Р ВµР С”',
    surfaceDirectoriesLabel: 'Р СџР С•Р Р†Р ВµРЎР‚РЎвЂ¦Р Р…Р С•РЎРѓРЎвЂљР С‘ РЎРѓР С•Р Р†Р СР ВµРЎРѓРЎвЂљР С‘Р СР С•РЎРѓРЎвЂљР С‘',
  },
}

export function getCopy(locale: string): LocaleCopy {
  return locale.startsWith('ru') ? copyByLanguage.ru : copyByLanguage.en
}

export function formatSessionMode(mode: DesktopMode) {
  return mode === 'code' ? 'Code' : mode
}
