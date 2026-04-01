const sharedTranslations = {
  app: { workspace: 'Desktop shell' },
  modes: { title: 'Mode selector', chat: 'Chat', code: 'Code' },
  status: { connected: 'IPC attached', local: 'Local preview' },
  top: { locale: 'Locale' },
  content: {
    optimization: 'Optimization tracks',
    compatibility: 'Compatibility guardrails',
    sources: 'Source mirrors',
    workspace: 'Workspace',
    ipcReference: 'IPC reference',
  },
}

export const resources = {
  en: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'New session',
        search: 'Session search',
        searchPlaceholder: 'Filter architecture tracks',
        recent: 'Recent threads',
        sources: 'Research mirrors',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'A conversation shell over the qwen runtime.',
        chatSubtitle:
          'Keep qwen compatibility intact while making session, context, and history navigation feel native on desktop.',
        codeTitle: 'A coding cockpit shaped by qwen core and Claude-grade ergonomics.',
        codeSubtitle: 'Use Claude as the UX reference for tool orchestration, not as the runtime authority.',
      },
      composer: {
        currentMode: 'Current mode',
        chatPlaceholder: 'Describe the session, migration, or research task you want to tackle.',
        codePlaceholder:
          'Ask for runtime integration, IPC work, renderer implementation, or qwen compatibility hardening.',
        chatAction: 'Start research thread',
        codeAction: 'Open code workflow',
      },
    },
  },
  ru: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Новая сессия',
        search: 'Поиск по сессиям',
        searchPlaceholder: 'Фильтр по архитектурным направлениям',
        recent: 'Недавние треды',
        sources: 'Исходники для ресерча',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Диалоговая оболочка поверх qwen runtime.',
        chatSubtitle:
          'Сохраняем совместимость qwen и делаем работу с контекстом, памятью и историей естественной для десктопа.',
        codeTitle: 'Кодовый cockpit на qwen core с эргономикой уровня Claude.',
        codeSubtitle: 'Claude используем как референс UX и tool-flow, а не как источник runtime-логики.',
      },
      composer: {
        currentMode: 'Текущий режим',
        chatPlaceholder: 'Опиши исследовательскую задачу, миграцию или сценарий сессии.',
        codePlaceholder:
          'Спроси про IPC, интеграцию qwen runtime, реализацию renderer или compatibility hardening.',
        chatAction: 'Открыть исследование',
        codeAction: 'Открыть code workflow',
      },
    },
  },
  'zh-CN': {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: '新会话',
        search: '搜索会话',
        searchPlaceholder: '按架构方向筛选',
        recent: '最近线程',
        sources: '研究镜像',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: '围绕 qwen runtime 的桌面对话外壳。',
        chatSubtitle: '保持 qwen 兼容性的同时，让上下文、记忆和历史导航更适合桌面体验。',
        codeTitle: '以 qwen core 为基础，并借鉴 Claude 级交互体验的编码工作台。',
        codeSubtitle: 'Claude 只作为工具编排与 UX 参考，而不是运行时权威。',
      },
      composer: {
        currentMode: '当前模式',
        chatPlaceholder: '描述你要处理的研究任务、迁移方案或会话目标。',
        codePlaceholder: '请求 qwen runtime 集成、IPC 开发、renderer 实现或兼容性加固。',
        chatAction: '开始研究线程',
        codeAction: '进入编码流程',
      },
    },
  },
  de: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Neue Sitzung',
        search: 'Sitzungen durchsuchen',
        searchPlaceholder: 'Architekturpfade filtern',
        recent: 'Letzte Threads',
        sources: 'Quellspiegel',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Eine Dialogoberfläche auf dem qwen-Runtime.',
        chatSubtitle: 'Qwen-Kompatibilität bleibt erhalten, während Kontext, Verlauf und Speicher nativ am Desktop wirken.',
        codeTitle: 'Ein Coding-Cockpit mit qwen-Core und Claude-artiger Ergonomie.',
        codeSubtitle: 'Claude dient nur als UX- und Tool-Referenz, nicht als Runtime-Autorität.',
      },
      composer: {
        currentMode: 'Aktueller Modus',
        chatPlaceholder: 'Beschreibe die Recherche, Migration oder Sitzung, die du angehen willst.',
        codePlaceholder: 'Frage nach Runtime-Integration, IPC, Renderer-Umsetzung oder Kompatibilitäts-Härtung.',
        chatAction: 'Recherche starten',
        codeAction: 'Code-Workflow öffnen',
      },
    },
  },
  fr: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Nouvelle session',
        search: 'Rechercher des sessions',
        searchPlaceholder: 'Filtrer les pistes d’architecture',
        recent: 'Fils récents',
        sources: 'Mirroirs de recherche',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Une interface de conversation au-dessus du runtime qwen.',
        chatSubtitle:
          'Préserver la compatibilité qwen tout en rendant le contexte, la mémoire et l’historique naturels sur desktop.',
        codeTitle: 'Un cockpit de code basé sur qwen avec une ergonomie de niveau Claude.',
        codeSubtitle: 'Claude sert de référence UX et d’orchestration des outils, pas d’autorité runtime.',
      },
      composer: {
        currentMode: 'Mode actuel',
        chatPlaceholder: 'Décris la recherche, la migration ou la session que tu veux lancer.',
        codePlaceholder: 'Demande une intégration runtime, du travail IPC, le renderer ou un renforcement de compatibilité.',
        chatAction: 'Ouvrir une recherche',
        codeAction: 'Ouvrir le workflow code',
      },
    },
  },
  es: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Nueva sesión',
        search: 'Buscar sesiones',
        searchPlaceholder: 'Filtrar líneas de arquitectura',
        recent: 'Hilos recientes',
        sources: 'Espejos de investigación',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Una capa conversacional sobre el runtime de qwen.',
        chatSubtitle: 'Mantenemos la compatibilidad de qwen mientras contexto, memoria e historial se sienten nativos en escritorio.',
        codeTitle: 'Una cabina de código con qwen core y ergonomía al nivel de Claude.',
        codeSubtitle: 'Claude se usa como referencia de UX y orquestación, no como autoridad de runtime.',
      },
      composer: {
        currentMode: 'Modo actual',
        chatPlaceholder: 'Describe la investigación, migración o sesión que quieres abordar.',
        codePlaceholder: 'Pide integración de runtime, IPC, renderer o endurecimiento de compatibilidad.',
        chatAction: 'Abrir investigación',
        codeAction: 'Abrir flujo de código',
      },
    },
  },
  ja: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: '新しいセッション',
        search: 'セッション検索',
        searchPlaceholder: 'アーキテクチャ方針を絞り込む',
        recent: '最近のスレッド',
        sources: '調査ミラー',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'qwen runtime の上に載る会話シェル。',
        chatSubtitle: 'qwen 互換性を保ちながら、コンテキスト、メモリ、履歴操作をデスクトップ向けに自然化します。',
        codeTitle: 'qwen core を基盤にしつつ、Claude 級の使い勝手を目指すコーディング環境。',
        codeSubtitle: 'Claude は UX とツール編成の参照であり、runtime の権威ではありません。',
      },
      composer: {
        currentMode: '現在のモード',
        chatPlaceholder: '取り組みたい調査、移行、またはセッションの目的を説明してください。',
        codePlaceholder: 'runtime 統合、IPC、renderer 実装、互換性強化について依頼してください。',
        chatAction: '調査を開始',
        codeAction: 'コードワークフローを開く',
      },
    },
  },
  ko: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: '새 세션',
        search: '세션 검색',
        searchPlaceholder: '아키텍처 트랙 필터링',
        recent: '최근 스레드',
        sources: '리서치 미러',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'qwen runtime 위에서 동작하는 대화형 셸.',
        chatSubtitle: 'qwen 호환성을 유지하면서 컨텍스트, 메모리, 히스토리 탐색을 데스크톱에 맞게 다듬습니다.',
        codeTitle: 'qwen core 기반에 Claude 급 사용성을 더한 코딩 콕핏.',
        codeSubtitle: 'Claude 는 UX 와 도구 흐름의 참고 대상일 뿐, runtime 권한자는 아닙니다.',
      },
      composer: {
        currentMode: '현재 모드',
        chatPlaceholder: '다루고 싶은 리서치, 마이그레이션 또는 세션 목표를 설명하세요.',
        codePlaceholder: 'runtime 통합, IPC 작업, renderer 구현 또는 호환성 강화를 요청하세요.',
        chatAction: '리서치 시작',
        codeAction: '코드 워크플로 열기',
      },
    },
  },
  'pt-BR': {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Nova sessão',
        search: 'Buscar sessões',
        searchPlaceholder: 'Filtrar trilhas de arquitetura',
        recent: 'Threads recentes',
        sources: 'Espelhos de pesquisa',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Uma camada conversacional sobre o runtime do qwen.',
        chatSubtitle: 'Mantemos a compatibilidade do qwen enquanto contexto, memória e histórico ficam naturais no desktop.',
        codeTitle: 'Um cockpit de código baseado em qwen core com ergonomia de nível Claude.',
        codeSubtitle: 'Claude entra como referência de UX e orquestração de ferramentas, não como autoridade de runtime.',
      },
      composer: {
        currentMode: 'Modo atual',
        chatPlaceholder: 'Descreva a pesquisa, migração ou sessão que você quer conduzir.',
        codePlaceholder: 'Peça integração de runtime, IPC, renderer ou reforço de compatibilidade.',
        chatAction: 'Abrir pesquisa',
        codeAction: 'Abrir fluxo de código',
      },
    },
  },
  tr: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Yeni oturum',
        search: 'Oturum ara',
        searchPlaceholder: 'Mimari akışları filtrele',
        recent: 'Son başlıklar',
        sources: 'Araştırma aynaları',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'qwen runtime üzerinde çalışan bir konuşma kabuğu.',
        chatSubtitle: 'qwen uyumluluğunu korurken bağlam, bellek ve geçmiş gezintisini masaüstüne uygun hale getiriyoruz.',
        codeTitle: 'qwen core temelli, Claude düzeyinde ergonomiye sahip bir kod kokpiti.',
        codeSubtitle: 'Claude yalnızca UX ve araç akışı referansıdır; runtime otoritesi değildir.',
      },
      composer: {
        currentMode: 'Geçerli mod',
        chatPlaceholder: 'Ele almak istediğin araştırmayı, geçişi veya oturum hedefini açıkla.',
        codePlaceholder: 'Runtime entegrasyonu, IPC, renderer veya uyumluluk güçlendirmesi iste.',
        chatAction: 'Araştırmayı aç',
        codeAction: 'Kod akışını aç',
      },
    },
  },
  ar: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'جلسة جديدة',
        search: 'بحث في الجلسات',
        searchPlaceholder: 'تصفية مسارات المعمارية',
        recent: 'المحادثات الأخيرة',
        sources: 'مرايا البحث',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'واجهة محادثة فوق qwen runtime.',
        chatSubtitle: 'نحافظ على توافق qwen مع جعل السياق والذاكرة والتاريخ أكثر طبيعية على سطح المكتب.',
        codeTitle: 'مقصورة برمجة مبنية على qwen core مع تجربة استخدام بمستوى Claude.',
        codeSubtitle: 'يُستخدم Claude كمرجع لتجربة الاستخدام وتدفق الأدوات فقط، وليس كمرجعية runtime.',
      },
      composer: {
        currentMode: 'الوضع الحالي',
        chatPlaceholder: 'صف مهمة البحث أو الترحيل أو الجلسة التي تريد العمل عليها.',
        codePlaceholder: 'اطلب تكامل runtime أو أعمال IPC أو تنفيذ renderer أو تعزيز التوافق.',
        chatAction: 'بدء البحث',
        codeAction: 'فتح مسار البرمجة',
      },
    },
  },
} as const
