const sharedTranslations = {
  app: { workspace: 'Desktop shell' },
  modes: { title: 'Code mode', code: 'Code' },
  status: { connected: 'IPC attached', local: 'Local preview' },
  top: { settings: 'Settings' },
  content: {
    optimization: 'Optimization tracks',
    compatibility: 'Compatibility guardrails',
    sources: 'Workspace',
    workspace: 'Workspace',
    ipcReference: 'IPC reference',
  },
  sidebar: {
    newChat: 'New Chat',
    search: 'Session search',
    searchPlaceholder: 'Filter sessions by name',
    recent: 'Recent conversations',
    sources: 'Workspace Sources',
  },
}

export const resources = {
  en: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'New Chat',
        search: 'Session search',
        searchPlaceholder: 'Filter sessions by name',
        recent: 'Recent conversations',
        sources: 'Workspace Sources',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'A code-first shell over the qwen runtime.',
        chatSubtitle:
          'Keep qwen compatibility intact while making sessions, context, and history navigation feel native on desktop.',
        codeTitle: 'A coding cockpit shaped by qwen core and Claude-grade ergonomics.',
        codeSubtitle: 'Use Claude as the UX reference for tool orchestration, not as the runtime authority.',
      },
      composer: {
        currentMode: 'Current surface',
        chatPlaceholder: 'Describe the implementation, migration, or session task you want to tackle.',
        codePlaceholder:
          'Ask for runtime integration, IPC work, renderer implementation, or qwen compatibility hardening.',
        chatAction: 'Start code session',
        codeAction: 'Open code workflow',
      },
    },
  },
  ru: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Новый чат',
        search: 'Поиск сессии',
        searchPlaceholder: 'Фильтровать сессии по названию',
        recent: 'Недавние разговоры',
        sources: 'Источники рабочей области',
      },
      top: {
        settings: 'Настройки',
      },
      hero: {
        kicker: 'Архитектура Qwen-first для рабочего стола',
        chatTitle: 'Оболочка для работы с кодом над средой выполнения qwen.',
        chatSubtitle:
          'Сохраняем совместимость с qwen, делая навигацию по сессиям, контексту и истории естественной для рабочего стола.',
        codeTitle: 'Рабочее место для кодирования, созданное на основе ядра qwen с эргономикой уровня Claude.',
        codeSubtitle: 'Используйте Claude как эталон UX и оркестрации инструментов, а не как источник управления выполнением.',
      },
      composer: {
        currentMode: 'Текущая поверхность',
        chatPlaceholder: 'Опишите реализацию, перенос или задачу сессии, которую вы хотите решить.',
        codePlaceholder:
          'Запросите интеграцию среды выполнения, работу IPC, реализацию рендерера или улучшение совместимости qwen.',
        chatAction: 'Начать сессию кодирования',
        codeAction: 'Открыть рабочий процесс кодирования',
      },
    },
  },
  'zh-CN': {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: '新聊天',
        search: '会话搜索',
        searchPlaceholder: '按名称筛选会话',
        recent: '最近对话',
        sources: '工作区源',
      },
      hero: {
        kicker: 'Qwen优先的桌面架构',
        chatTitle: '基于qwen运行时的代码优先外壳。',
        chatSubtitle: '在保持qwen兼容性的同时，让会话、上下文和历史导航在桌面上感觉更自然。',
        codeTitle: '以qwen核心为基础，具有Claude级别人体工程学的设计编码驾驶舱。',
        codeSubtitle: '使用Claude作为工具编排的UX参考，而不是运行时权威。',
      },
      composer: {
        currentMode: '当前表面',
        chatPlaceholder: '描述您想要处理的实现、迁移或会话任务。',
        codePlaceholder: '请求运行时集成、IPC工作、渲染器实现或qwen兼容性强化。',
        chatAction: '开始代码会话',
        codeAction: '打开代码工作流',
      },
    },
  },
  ja: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: '新しいチャット',
        search: 'セッション検索',
        searchPlaceholder: '名前でセッションをフィルタリング',
        recent: '最近の会話',
        sources: 'ワークスペースソース',
      },
      hero: {
        kicker: 'Qwenファーストのデスクトップアーキテクチャ',
        chatTitle: 'qwenランタイム上でのコード優先シェル。',
        chatSubtitle: 'qwen互換性を維持しながら、セッション、コンテキスト、履歴ナビゲーションをデスクトップに自然に感じさせます。',
        codeTitle: 'qwenコアに基づき、Claude級の人間工学を備えたコーディング操縦室。',
        codeSubtitle: 'Claudeはツール編成のUXリファレンスとして使用し、ランタイム権限としては使用しません。',
      },
      composer: {
        currentMode: '現在のサーフェス',
        chatPlaceholder: '実装、移行、またはセッションタスクについて説明してください。',
        codePlaceholder: 'ランタイム統合、IPC作業、レンダラー実装、またはqwen互換性強化を要求します。',
        chatAction: 'コードセッションを開始',
        codeAction: 'コードワークフローを開く',
      },
    },
  },
  ko: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: '새 채팅',
        search: '세션 검색',
        searchPlaceholder: '이름으로 세션 필터링',
        recent: '최근 대화',
        sources: '작업공간 소스',
      },
      hero: {
        kicker: 'Qwen 우선 데스크톱 아키텍처',
        chatTitle: 'qwen 런타임 위의 코드 중심 쉘.',
        chatSubtitle: 'qwen 호환성을 유지하면서 세션, 컨텍스트 및 기록 탐색을 데스크톱에 자연스럽게 느끼게 합니다.',
        codeTitle: 'qwen 코어를 기반으로 한 Claude 수준의 인간 공학 설계 코딩 조종석.',
        codeSubtitle: 'Claude를 런타임 권한이 아닌 도구 오케스트레이션의 UX 기준으로 사용합니다.',
      },
      composer: {
        currentMode: '현재 표면',
        chatPlaceholder: '처리하려는 구현, 마이그레이션 또는 세션 작업을 설명하세요.',
        codePlaceholder: '런타임 통합, IPC 작업, 렌더러 구현 또는 qwen 호환성 강화를 요청하세요.',
        chatAction: '코드 세션 시작',
        codeAction: '코드 워크플로우 열기',
      },
    },
  },
  'pt-BR': {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Novo Chat',
        search: 'Pesquisa de sessão',
        searchPlaceholder: 'Filtrar sessões por nome',
        recent: 'Conversas recentes',
        sources: 'Fontes do espaço de trabalho',
      },
      hero: {
        kicker: 'Arquitetura de desktop Qwen-first',
        chatTitle: 'Uma interface de código sobre o runtime qwen.',
        chatSubtitle: 'Mantém a compatibilidade com o qwen intacta, fazendo com que a navegação por sessões, contexto e histórico pareça nativa na área de trabalho.',
        codeTitle: 'Uma cabine de codificação moldada pelo núcleo qwen e pela ergonomia nível Claude.',
        codeSubtitle: 'Use Claude como referência de UX para orquestração de ferramentas, não como autoridade de runtime.',
      },
      composer: {
        currentMode: 'Superfície atual',
        chatPlaceholder: 'Descreva a implementação, migração ou tarefa de sessão que você deseja resolver.',
        codePlaceholder: 'Peça integração de runtime, trabalho IPC, implementação de renderizador ou endurecimento de compatibilidade com qwen.',
        chatAction: 'Iniciar sessão de código',
        codeAction: 'Abrir fluxo de trabalho de código',
      },
    },
  },
} as const
