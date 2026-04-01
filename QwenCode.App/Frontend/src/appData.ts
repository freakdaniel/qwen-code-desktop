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
  | 'artifacts'
  | 'write'
  | 'learn'
  | 'code'
  | 'spark'
  | 'ghost'
  | 'chevronLeft'

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
  sourceMirrors: string
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

export const fallbackBootstrap: AppBootstrapPayload = {
  productName: 'Qwen Code Desktop',
  currentMode: 'code',
  currentLocale: 'ru',
  locales: [
    { code: 'en', name: 'English', nativeName: 'English' },
    { code: 'ru', name: 'Russian', nativeName: 'Русский' },
    { code: 'zh-CN', name: 'Chinese', nativeName: '简体中文' },
    { code: 'de', name: 'German', nativeName: 'Deutsch' },
    { code: 'fr', name: 'French', nativeName: 'Francais' },
    { code: 'es', name: 'Spanish', nativeName: 'Espanol' },
    { code: 'ja', name: 'Japanese', nativeName: '日本語' },
    { code: 'ko', name: 'Korean', nativeName: '한국어' },
    { code: 'pt-BR', name: 'Portuguese (Brazil)', nativeName: 'Português (Brasil)' },
    { code: 'tr', name: 'Turkish', nativeName: 'Türkçe' },
    { code: 'ar', name: 'Arabic', nativeName: 'العربية' },
  ],
  sources: {
    workspaceRoot: 'D:\\Projects\\qwen-code-desktop',
    qwenRoot: 'D:\\Projects\\qwen-code-main',
    claudeRoot: 'D:\\Projects\\claude-code-main',
    ipcReferenceRoot: 'D:\\Projects\\HyPrism',
  },
  tracks: [
    {
      title: 'Lift qwen core behind a native session host',
      summary:
        'The desktop backend should own orchestration, but the model loop, tools, history, and policy logic must stay source-compatible with qwen.',
    },
    {
      title: 'Adopt Claude-grade session ergonomics',
      summary:
        'Claude desktop patterns are strongest around workspaces, approvals, and context visibility rather than provider-specific logic.',
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
    'Treat claude-code as a UX and session-orchestration reference only.',
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
      title: 'Claude-inspired renderer lane',
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
        'packages/core should remain the authority for prompt assembly, tool execution, and session semantics.',
      claudeReference:
        'claude-code adds a session bridge instead of shoving desktop behavior into the renderer.',
      desktopDirection:
        'Build a native host around qwen core primitives, not a wrapper around qwen CLI stdout.',
      deliveryState: 'Foundation',
    },
    {
      area: 'Session lifecycle',
      qwenSource:
        'CLI and history flows already define how turns, resumes, and config layering work.',
      claudeReference:
        'BridgeConfig, SessionHandle, and session status tracking show how desktop workspaces can reconnect and expose live activity.',
      desktopDirection:
        'Promote sessions to first-class desktop objects with reconnect, activity, and branch/worktree awareness.',
      deliveryState: 'High priority',
    },
    {
      area: 'Approvals and tools',
      qwenSource:
        'Approval modes and sandbox policies already exist in qwen and should be preserved.',
      claudeReference:
        "Claude's UX makes permission requests, task state, and tool activity visible instead of burying them in terminal text.",
      desktopDirection:
        'Move approvals into explicit desktop panels while keeping qwen policy logic intact.',
      deliveryState: 'High priority',
    },
    {
      area: 'Context surfaces',
      qwenSource:
        'Settings, memory, slash commands, and project context are already well-defined.',
      claudeReference:
        'Customize, connectors, scheduled work, and project surfaces make these capabilities discoverable.',
      desktopDirection:
        'Expose qwen capabilities as browseable desktop surfaces rather than hidden CLI-only concepts.',
      deliveryState: 'In design',
    },
    {
      area: 'IPC discipline',
      qwenSource:
        'Core/CLI separation means renderer-specific contracts should stay outside the engine.',
      claudeReference:
        'Separate bridge types and APIs keep desktop traffic structured and evolvable.',
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
      workingDirectory: 'D:\\Projects\\qwen-code-desktop',
      gitBranch: 'main',
      messageCount: 14,
      transcriptPath: 'D:\\Projects\\qwen-code-desktop\\.qwen\\chats\\desktop-parity-audit.jsonl',
    },
    {
      sessionId: 'claude-session-bridge-mapping',
      title: 'Claude session bridge mapping',
      lastActivity: 'Updated 44 minutes ago',
      category: 'Customize',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: 'D:\\Projects\\qwen-code-desktop',
      gitBranch: 'main',
      messageCount: 11,
      transcriptPath: 'D:\\Projects\\qwen-code-desktop\\.qwen\\chats\\claude-session-bridge-mapping.jsonl',
    },
    {
      sessionId: 'qwen-core-host-extraction',
      title: 'qwen core host extraction',
      lastActivity: 'Updated 2 hours ago',
      category: 'Code',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: 'D:\\Projects\\qwen-code-desktop',
      gitBranch: 'main',
      messageCount: 22,
      transcriptPath: 'D:\\Projects\\qwen-code-desktop\\.qwen\\chats\\qwen-core-host-extraction.jsonl',
    },
    {
      sessionId: 'approval-panel-behaviors',
      title: 'Approval panel behaviors',
      lastActivity: 'Updated yesterday',
      category: 'UX',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: 'D:\\Projects\\qwen-code-desktop',
      gitBranch: 'main',
      messageCount: 8,
      transcriptPath: 'D:\\Projects\\qwen-code-desktop\\.qwen\\chats\\approval-panel-behaviors.jsonl',
    },
    {
      sessionId: 'workspace-source-mirror-review',
      title: 'Workspace source mirror review',
      lastActivity: 'Updated 2 days ago',
      category: 'Research',
      mode: 'code',
      status: 'resume-ready',
      workingDirectory: 'D:\\Projects\\qwen-code-desktop',
      gitBranch: 'main',
      messageCount: 5,
      transcriptPath: 'D:\\Projects\\qwen-code-desktop\\.qwen\\chats\\workspace-source-mirror-review.jsonl',
    },
  ],
  sourceStatuses: [
    {
      id: 'workspace',
      title: 'Desktop workspace',
      path: 'D:\\Projects\\qwen-code-desktop',
      status: 'ready',
      summary: 'Electron host, IPC generator, and renderer workspace. Repository and expected markers are available.',
      exists: true,
      isGitRepository: true,
      primaryMarker: 'QwenCode.slnx',
      highlights: ['Directory found', 'Git repository detected', 'Primary marker: QwenCode.slnx'],
    },
    {
      id: 'qwen',
      title: 'qwen-code',
      path: 'D:\\Projects\\qwen-code-main',
      status: 'ready',
      summary: 'Primary runtime and tool execution reference. Repository and expected markers are available.',
      exists: true,
      isGitRepository: true,
      primaryMarker: 'package.json',
      highlights: ['Directory found', 'Git repository detected', 'Primary marker: package.json'],
    },
    {
      id: 'claude',
      title: 'claude-code',
      path: 'D:\\Projects\\claude-code-main',
      status: 'ready',
      summary: 'Desktop UX and session bridge reference. Repository and expected markers are available.',
      exists: true,
      isGitRepository: true,
      primaryMarker: 'src/bridge/types.ts',
      highlights: ['Directory found', 'Git repository detected', 'Primary marker: src/bridge/types.ts'],
    },
    {
      id: 'ipc',
      title: 'IPC reference',
      path: 'D:\\Projects\\HyPrism',
      status: 'partial',
      summary: 'Typed preload and shell integration reference. Path exists, but some expected repository markers are missing.',
      exists: true,
      isGitRepository: false,
      primaryMarker: '',
      highlights: ['Directory found'],
    },
  ],
  runtimePortPlan: [
    {
      id: 'qwen-core-engine',
      title: 'Port qwen core engine to .NET runtime services',
      sourceSystem: 'qwen-code',
      targetModule: 'QwenCode.Runtime',
      stage: 'next',
      summary: 'Mirror is ready: qwen-code 0.14.0 with 6 workspace entries and a detected core package.',
      compatibilityContract:
        'Preserve prompt assembly, session turns, model/tool orchestration, and history semantics without routing through qwen CLI.',
      evidencePaths: ['package.json', 'packages/core', 'docs/developers/architecture.md'],
    },
    {
      id: 'qwen-tooling-host',
      title: 'Rebuild qwen tool registry as native .NET services',
      sourceSystem: 'qwen-code',
      targetModule: 'QwenCode.Runtime.Tools',
      stage: 'next',
      summary:
        'Tool sources are present under packages/core/src/tools with key markers for shell, file, search, and MCP work.',
      compatibilityContract:
        'Keep qwen tool names, approval boundaries, and workspace behavior stable while swapping the execution host to .NET.',
      evidencePaths: [
        'packages/core/src/tools',
        'packages/core/src/tools/tool-registry.ts',
        'packages/core/src/tools/shell.ts',
      ],
    },
    {
      id: 'qwen-compat-settings',
      title: 'Preserve qwen settings, skills, and command compatibility',
      sourceSystem: 'qwen-code',
      targetModule: 'QwenCode.Runtime.Configuration',
      stage: 'next',
      summary:
        'Compatibility markers for .qwen commands, skills, and documented settings are present and ready to be modeled in .NET.',
      compatibilityContract:
        'Do not fork .qwen conventions; instead read and honor compatible settings, commands, and skill locations from the native runtime.',
      evidencePaths: ['.qwen', '.qwen/commands', '.qwen/skills', 'docs/users/configuration/settings.md'],
    },
    {
      id: 'claude-session-host',
      title: 'Adapt Claude session bridge patterns for native desktop hosting',
      sourceSystem: 'claude-code',
      targetModule: 'QwenCode.SessionHost',
      stage: 'next',
      summary:
        'Bridge sources are present with typed session and transport contracts ready to be adapted.',
      compatibilityContract:
        'Adopt reconnectable session-host and activity-tracking patterns, but keep Qwen as the only model/runtime authority.',
      evidencePaths: ['src/bridge/types.ts', 'src/bridge/sessionRunner.ts', 'src/bridge/codeSessionApi.ts'],
    },
    {
      id: 'claude-approval-ux',
      title: 'Port explicit approval and permission UX into the renderer',
      sourceSystem: 'claude-code',
      targetModule: 'QwenCode.Renderer.Approvals',
      stage: 'queued',
      summary:
        'Permission- and session-oriented command surfaces are present and can be translated into desktop approval panels.',
      compatibilityContract:
        'Renderer should expose approvals clearly, but the decision rules must still come from qwen-compatible runtime policy.',
      evidencePaths: ['src/bridge/bridgePermissionCallbacks.ts', 'src/commands/permissions', 'src/commands/plan'],
    },
    {
      id: 'claude-workspace-ux',
      title: 'Adapt Claude desktop workspace navigation to qwen desktop surfaces',
      sourceSystem: 'claude-code',
      targetModule: 'QwenCode.Renderer.Workspace',
      stage: 'foundation',
      summary:
        'Desktop, statusline, and session command surfaces are available as concrete UX references for a code-first workspace shell.',
      compatibilityContract:
        'Borrow desktop navigation, session discovery, and activity presentation while keeping qwen storage and behavior compatible.',
      evidencePaths: ['src/commands/desktop/desktop.tsx', 'src/commands/statusline.tsx', 'src/commands/session'],
    },
  ],
  qwenCompatibility: {
    projectRoot: 'D:\\Projects\\qwen-code-desktop',
    defaultContextFileName: 'QWEN.md',
    settingsLayers: [
      {
        id: 'system-defaults',
        title: 'System defaults',
        scope: 'system defaults',
        priority: 2,
        path: 'C:\\ProgramData\\qwen-code\\system-defaults.json',
        exists: false,
        categories: [],
      },
      {
        id: 'user-settings',
        title: 'User settings',
        scope: 'user',
        priority: 3,
        path: 'C:\\Users\\Daniel Freak\\.qwen\\settings.json',
        exists: false,
        categories: [],
      },
      {
        id: 'project-settings',
        title: 'Project settings',
        scope: 'project',
        priority: 4,
        path: 'D:\\Projects\\qwen-code-desktop\\.qwen\\settings.json',
        exists: false,
        categories: [],
      },
      {
        id: 'system-settings',
        title: 'System settings',
        scope: 'system override',
        priority: 5,
        path: 'C:\\ProgramData\\qwen-code\\settings.json',
        exists: false,
        categories: [],
      },
    ],
    surfaceDirectories: [
      {
        id: 'project-commands',
        title: 'Project commands',
        path: 'D:\\Projects\\qwen-code-desktop\\.qwen\\commands',
        exists: false,
        itemCount: 0,
        summary: 'Slash-command markdown and command surfaces. Not found yet.',
      },
      {
        id: 'project-skills',
        title: 'Project skills',
        path: 'D:\\Projects\\qwen-code-desktop\\.qwen\\skills',
        exists: false,
        itemCount: 0,
        summary: 'Project-local skills stored as directories with SKILL.md. Not found yet.',
      },
      {
        id: 'user-skills',
        title: 'User skills',
        path: 'C:\\Users\\Daniel Freak\\.qwen\\skills',
        exists: false,
        itemCount: 0,
        summary: 'User-level skill surface shared across projects. Not found yet.',
      },
      {
        id: 'context-root',
        title: 'Workspace context file',
        path: 'D:\\Projects\\qwen-code-desktop\\QWEN.md',
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
        path: 'D:\\Projects\\qwen-code-desktop\\.qwen\\commands\\qc\\code-review.md',
        description: 'Review a pull request with qwen-native guidance.',
        group: 'qc',
      },
      {
        id: 'user:team/release.md',
        name: 'team/release',
        scope: 'user',
        path: 'C:\\Users\\Daniel Freak\\.qwen\\commands\\team\\release.md',
        description: 'Prepare a release note draft.',
        group: 'team',
      },
    ],
    skills: [
      {
        id: 'project:project-review',
        name: 'project-review',
        scope: 'project',
        path: 'D:\\Projects\\qwen-code-desktop\\.qwen\\skills\\project-review\\SKILL.md',
        description: 'Review project changes with local context.',
        allowedTools: ['read_file', 'grep_search'],
      },
      {
        id: 'user:user-skill',
        name: 'user-skill',
        scope: 'user',
        path: 'C:\\Users\\Daniel Freak\\.qwen\\skills\\user-skill\\SKILL.md',
        description: 'A user-level reusable skill.',
        allowedTools: [],
      },
    ],
  },
  qwenRuntime: {
    projectRoot: 'D:\\Projects\\qwen-code-desktop',
    globalQwenDirectory: 'C:\\Users\\Daniel Freak\\.qwen',
    runtimeBaseDirectory: 'C:\\Users\\Daniel Freak\\.qwen',
    runtimeSource: 'default-home',
    projectDataDirectory: 'C:\\Users\\Daniel Freak\\.qwen\\projects\\d--projects--qwen-code-desktop',
    chatsDirectory: 'C:\\Users\\Daniel Freak\\.qwen\\projects\\d--projects--qwen-code-desktop\\chats',
    historyDirectory: 'C:\\Users\\Daniel Freak\\.qwen\\history\\demo-project-hash',
    contextFileNames: ['QWEN.md'],
    contextFilePaths: ['D:\\Projects\\qwen-code-desktop\\QWEN.md'],
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
    sourceMode: 'source-assisted',
    totalCount: 4,
    allowedCount: 2,
    askCount: 2,
    denyCount: 0,
    tools: [
      {
        name: 'read_file',
        displayName: 'ReadFile',
        kind: 'read',
        sourcePath: 'D:/Projects/qwen-code-main/packages/core/src/tools/read-file.ts',
        approvalState: 'allow',
        approvalReason: 'Allowed by explicit compatibility rule.',
      },
      {
        name: 'edit',
        displayName: 'Edit',
        kind: 'modify',
        sourcePath: 'D:/Projects/qwen-code-main/packages/core/src/tools/edit.ts',
        approvalState: 'ask',
        approvalReason: 'Requires confirmation due to explicit ask rule.',
      },
      {
        name: 'run_shell_command',
        displayName: 'Shell',
        kind: 'execute',
        sourcePath: 'D:/Projects/qwen-code-main/packages/core/src/tools/shell.ts',
        approvalState: 'ask',
        approvalReason: 'Requires confirmation in default mode.',
      },
      {
        name: 'agent',
        displayName: 'Agent',
        kind: 'coordination',
        sourcePath: 'D:/Projects/qwen-code-main/packages/core/src/tools/agent.ts',
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
      'A native shell over qwen-code that borrows Claude-grade desktop patterns without inheriting CLI coupling.',
    homeGreeting: 'Good evening, Daniel',
    homeLead: 'How can I help you move qwen-code from terminal shell to a real desktop product?',
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
      { label: 'Claude patterns', icon: 'spark' },
    ],
    bridgeStatus: { connected: 'IPC attached', local: 'Local preview' },
    chatSurfaceTitle: 'Sessions',
    chatSurfaceSubtitle:
      'Keep implementation work, approvals, and native runtime sessions visible in one desktop index.',
    emptySearch: 'Nothing matched this search yet.',
    customizeTitle: 'Customize',
    customizeSubtitle:
      'Use source mirrors and adoption patterns to decide what belongs in qwen core, in the native host, and in the renderer.',
    customizeLibraryTitle: 'Architecture library',
    customizeDetailTitle: 'Pattern details',
    referenceFromQwen: 'From qwen-code',
    referenceFromClaude: 'From claude-code',
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
    sourceMirrors: 'Source mirrors',
    compatibilityGoals: 'Compatibility goals',
    runtimeProfileLabel: 'Runtime profile',
    runtimeApprovalLabel: 'Approval profile',
    toolCatalogLabel: 'Tool catalog',
    nativeHostLabel: 'Native host',
    currentLocale: 'Locale',
    workspaceTag: 'Workspace',
    modeLabel: 'Surface',
    searchPlaceholder: 'Search sessions, patterns, and source mirrors...',
    settingsLayersLabel: 'Settings layers',
    surfaceDirectoriesLabel: 'Compatibility surfaces',
  },
  ru: {
    appLabel: 'Desktop shell',
    newChat: 'Новая сессия',
    search: 'Поиск',
    customize: 'Customize',
    chats: 'Сессии',
    projects: 'Проекты',
    artifacts: 'Артефакты',
    recents: 'Недавние',
    allConversations: 'Ваши кодовые сессии с Qwen',
    rootViewTitle: 'Qwen-first desktop workspace',
    rootViewSubtitle:
      'Нативная оболочка над qwen-code, которая заимствует сильные desktop-паттерны Claude без зависимости от их CLI.',
    homeGreeting: 'Добрый вечер, Daniel',
    homeLead: 'Чем помочь с переносом qwen-code из terminal shell в полноценный desktop-продукт?',
    homeModeDescriptions: {
      code: 'Реализация backend extraction, IPC-контрактов, renderer-поведения и session hosting.',
    },
    composerPlaceholder: {
      code: 'Попроси реализацию backend extraction, IPC, renderer или compatibility hardening поверх qwen.',
    },
    modelLabel: 'Qwen Max Preview',
    sendLabel: 'Отправить',
    sendingLabel: 'Запускаю turn...',
    sessionHostLabel: 'Нативный session host',
    sessionCreatedLabel: 'Сессия создана',
    sessionUpdatedLabel: 'Сессия обновлена',
    transcriptLabel: 'Транскрипт',
    quickActions: [
      { label: 'Писать', icon: 'write' },
      { label: 'Учиться', icon: 'learn' },
      { label: 'Код', icon: 'code' },
      { label: 'Паттерны Claude', icon: 'spark' },
    ],
    bridgeStatus: { connected: 'IPC подключен', local: 'Локальный превью-режим' },
    chatSurfaceTitle: 'Сессии',
    chatSurfaceSubtitle:
      'Держите implementation-задачи, approvals и нативные runtime-сессии в одном desktop-индексе.',
    emptySearch: 'По этому запросу пока ничего не нашлось.',
    customizeTitle: 'Customize',
    customizeSubtitle:
      'Используйте source mirrors и adoption patterns, чтобы разделить ответственность между qwen core, native host и renderer.',
    customizeLibraryTitle: 'Библиотека паттернов',
    customizeDetailTitle: 'Детали паттерна',
    referenceFromQwen: 'Из qwen-code',
    referenceFromClaude: 'Из claude-code',
    desktopDecision: 'Решение для desktop',
    deliveryState: 'Состояние реализации',
    capabilityLanes: 'Рабочие контуры',
    responsibilities: 'Ответственности',
    projectsTitle: 'Проекты',
    projectsSubtitle:
      'Разделяйте runtime, native host и renderer на отдельные, но синхронизированные направления работы.',
    artifactsTitle: 'Артефакты',
    artifactsSubtitle:
      'Превращайте архитектурные решения в явные desktop-артефакты, а не в скрытые terminal-конвенции.',
    sourceMirrors: 'Source mirrors',
    compatibilityGoals: 'Цели совместимости',
    runtimeProfileLabel: 'Runtime profile',
    runtimeApprovalLabel: 'Approval profile',
    toolCatalogLabel: 'Каталог инструментов',
    nativeHostLabel: 'Нативный host',
    currentLocale: 'Язык',
    workspaceTag: 'Workspace',
    modeLabel: 'Поверхность',
    searchPlaceholder: 'Поиск по сессиям, паттернам и исходникам...',
    settingsLayersLabel: 'Слои настроек',
    surfaceDirectoriesLabel: 'Поверхности совместимости',
  },
}

export function getCopy(locale: string): LocaleCopy {
  return locale.startsWith('ru') ? copyByLanguage.ru : copyByLanguage.en
}

export function formatSessionMode(mode: DesktopMode) {
  return mode === 'code' ? 'Code' : mode
}
