const sharedTranslations = {
  app: { workspace: 'Desktop shell' },
  modes: { title: 'Code mode', code: 'Code' },
  status: { connected: 'IPC attached', local: 'Local preview' },
  top: { locale: 'Locale' },
  content: {
    optimization: 'Optimization tracks',
    compatibility: 'Compatibility guardrails',
    sources: 'Workspace',
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
        sources: 'Workspace',
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
        newChat: 'РќРѕРІР°СЏ СЃРµСЃСЃРёСЏ',
        search: 'РџРѕРёСЃРє РїРѕ СЃРµСЃСЃРёСЏРј',
        searchPlaceholder: 'Р¤РёР»СЊС‚СЂ РїРѕ Р°СЂС…РёС‚РµРєС‚СѓСЂРЅС‹Рј РЅР°РїСЂР°РІР»РµРЅРёСЏРј',
        recent: 'РќРµРґР°РІРЅРёРµ С‚СЂРµРґС‹',
        sources: 'РСЃС…РѕРґРЅРёРєРё РґР»СЏ СЂРµСЃРµСЂС‡Р°',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Code-first РѕР±РѕР»РѕС‡РєР° РїРѕРІРµСЂС… qwen runtime.',
        chatSubtitle:
          'РЎРѕС…СЂР°РЅСЏРµРј СЃРѕРІРјРµСЃС‚РёРјРѕСЃС‚СЊ qwen Рё РґРµР»Р°РµРј СЂР°Р±РѕС‚Сѓ СЃ СЃРµСЃСЃРёСЏРјРё, РєРѕРЅС‚РµРєСЃС‚РѕРј, РїР°РјСЏС‚СЊСЋ Рё РёСЃС‚РѕСЂРёРµР№ РµСЃС‚РµСЃС‚РІРµРЅРЅРѕР№ РґР»СЏ РґРµСЃРєС‚РѕРїР°.',
        codeTitle: 'РљРѕРґРѕРІС‹Р№ cockpit РЅР° qwen core СЃ СЌСЂРіРѕРЅРѕРјРёРєРѕР№ СѓСЂРѕРІРЅСЏ Claude.',
        codeSubtitle: 'Claude РёСЃРїРѕР»СЊР·СѓРµРј РєР°Рє СЂРµС„РµСЂРµРЅСЃ UX Рё tool-flow, Р° РЅРµ РєР°Рє РёСЃС‚РѕС‡РЅРёРє runtime-Р»РѕРіРёРєРё.',
      },
      composer: {
        currentMode: 'РўРµРєСѓС‰Р°СЏ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ',
        chatPlaceholder: 'РћРїРёС€Рё СЂРµР°Р»РёР·Р°С†РёСЋ, РјРёРіСЂР°С†РёСЋ РёР»Рё Р·Р°РґР°С‡Сѓ РґР»СЏ РєРѕРґРѕРІРѕР№ СЃРµСЃСЃРёРё.',
        codePlaceholder:
          'РЎРїСЂРѕСЃРё РїСЂРѕ IPC, РёРЅС‚РµРіСЂР°С†РёСЋ qwen runtime, СЂРµР°Р»РёР·Р°С†РёСЋ renderer РёР»Рё compatibility hardening.',
        chatAction: 'РћС‚РєСЂС‹С‚СЊ РєРѕРґРѕРІСѓСЋ СЃРµСЃСЃРёСЋ',
        codeAction: 'РћС‚РєСЂС‹С‚СЊ code workflow',
      },
    },
  },
  'zh-CN': {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'ж–°дјљиЇќ',
        search: 'жђњзґўдјљиЇќ',
        searchPlaceholder: 'жЊ‰жћ¶жћ„ж–№еђ‘з­›йЂ‰',
        recent: 'жњЂиї‘зєїзЁ‹',
        sources: 'з ”з©¶й•њеѓЏ',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'е›ґз»• qwen runtime зљ„жЎЊйќўеЇ№иЇќе¤–еЈігЂ‚',
        chatSubtitle: 'дїќжЊЃ qwen е…је®№жЂ§зљ„еђЊж—¶пјЊи®©дёЉдё‹ж–‡гЂЃи®°еї†е’ЊеЋ†еЏІеЇји€Єж›ґйЂ‚еђ€жЎЊйќўдЅ“йЄЊгЂ‚',
        codeTitle: 'д»Ґ qwen core дёєеџєзЎЂпјЊе№¶еЂџй‰ґ Claude зє§дє¤дє’дЅ“йЄЊзљ„зј–з Ѓе·ҐдЅњеЏ°гЂ‚',
        codeSubtitle: 'Claude еЏЄдЅњдёєе·Ґе…·зј–жЋ’дёЋ UX еЏ‚иЂѓпјЊиЂЊдёЌжЇиїђиЎЊж—¶жќѓеЁЃгЂ‚',
      },
      composer: {
        currentMode: 'еЅ“е‰ЌжЁЎејЏ',
        chatPlaceholder: 'жЏЏиї°дЅ и¦Ѓе¤„зђ†зљ„з ”з©¶д»»еЉЎгЂЃиїЃз§»ж–№жЎ€ж€–дјљиЇќз›®ж ‡гЂ‚',
        codePlaceholder: 'иЇ·ж±‚ qwen runtime й›†ж€ђгЂЃIPC ејЂеЏ‘гЂЃrenderer е®ћзЋ°ж€–е…је®№жЂ§еЉ е›єгЂ‚',
        chatAction: 'ејЂе§‹з ”з©¶зєїзЁ‹',
        codeAction: 'иї›е…Ґзј–з ЃжµЃзЁ‹',
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
        chatTitle: 'Eine DialogoberflГ¤che auf dem qwen-Runtime.',
        chatSubtitle: 'Qwen-KompatibilitГ¤t bleibt erhalten, wГ¤hrend Kontext, Verlauf und Speicher nativ am Desktop wirken.',
        codeTitle: 'Ein Coding-Cockpit mit qwen-Core und Claude-artiger Ergonomie.',
        codeSubtitle: 'Claude dient nur als UX- und Tool-Referenz, nicht als Runtime-AutoritГ¤t.',
      },
      composer: {
        currentMode: 'Aktueller Modus',
        chatPlaceholder: 'Beschreibe die Recherche, Migration oder Sitzung, die du angehen willst.',
        codePlaceholder: 'Frage nach Runtime-Integration, IPC, Renderer-Umsetzung oder KompatibilitГ¤ts-HГ¤rtung.',
        chatAction: 'Recherche starten',
        codeAction: 'Code-Workflow Г¶ffnen',
      },
    },
  },
  fr: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Nouvelle session',
        search: 'Rechercher des sessions',
        searchPlaceholder: 'Filtrer les pistes dвЂ™architecture',
        recent: 'Fils rГ©cents',
        sources: 'Mirroirs de recherche',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Une interface de conversation au-dessus du runtime qwen.',
        chatSubtitle:
          'PrГ©server la compatibilitГ© qwen tout en rendant le contexte, la mГ©moire et lвЂ™historique naturels sur desktop.',
        codeTitle: 'Un cockpit de code basГ© sur qwen avec une ergonomie de niveau Claude.',
        codeSubtitle: 'Claude sert de rГ©fГ©rence UX et dвЂ™orchestration des outils, pas dвЂ™autoritГ© runtime.',
      },
      composer: {
        currentMode: 'Mode actuel',
        chatPlaceholder: 'DГ©cris la recherche, la migration ou la session que tu veux lancer.',
        codePlaceholder: 'Demande une intГ©gration runtime, du travail IPC, le renderer ou un renforcement de compatibilitГ©.',
        chatAction: 'Ouvrir une recherche',
        codeAction: 'Ouvrir le workflow code',
      },
    },
  },
  es: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Nueva sesiГіn',
        search: 'Buscar sesiones',
        searchPlaceholder: 'Filtrar lГ­neas de arquitectura',
        recent: 'Hilos recientes',
        sources: 'Espejos de investigaciГіn',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Una capa conversacional sobre el runtime de qwen.',
        chatSubtitle: 'Mantenemos la compatibilidad de qwen mientras contexto, memoria e historial se sienten nativos en escritorio.',
        codeTitle: 'Una cabina de cГіdigo con qwen core y ergonomГ­a al nivel de Claude.',
        codeSubtitle: 'Claude se usa como referencia de UX y orquestaciГіn, no como autoridad de runtime.',
      },
      composer: {
        currentMode: 'Modo actual',
        chatPlaceholder: 'Describe la investigaciГіn, migraciГіn o sesiГіn que quieres abordar.',
        codePlaceholder: 'Pide integraciГіn de runtime, IPC, renderer o endurecimiento de compatibilidad.',
        chatAction: 'Abrir investigaciГіn',
        codeAction: 'Abrir flujo de cГіdigo',
      },
    },
  },
  ja: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'ж–°гЃ—гЃ„г‚»гѓѓг‚·гѓ§гѓі',
        search: 'г‚»гѓѓг‚·гѓ§гѓіж¤њзґў',
        searchPlaceholder: 'г‚ўгѓјг‚­гѓ†г‚ЇгѓЃгѓЈж–№й‡ќг‚’зµћг‚Љиѕјг‚Ђ',
        recent: 'жњЂиї‘гЃ®г‚№гѓ¬гѓѓгѓ‰',
        sources: 'иЄїжџ»гѓџгѓ©гѓј',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'qwen runtime гЃ®дёЉгЃ«иј‰г‚‹дјљи©±г‚·г‚§гѓ«гЂ‚',
        chatSubtitle: 'qwen дє’жЏ›жЂ§г‚’дїќгЃЎгЃЄгЃЊг‚‰гЂЃг‚ігѓігѓ†г‚­г‚№гѓ€гЂЃгѓЎгѓўгѓЄгЂЃе±Ґж­ґж“ЌдЅњг‚’гѓ‡г‚№г‚Їгѓ€гѓѓгѓ—еђ‘гЃ‘гЃ«и‡Єз„¶еЊ–гЃ—гЃѕгЃ™гЂ‚',
        codeTitle: 'qwen core г‚’еџєз›¤гЃ«гЃ—гЃ¤гЃ¤гЂЃClaude зґљгЃ®дЅїгЃ„е‹ќж‰‹г‚’з›®жЊ‡гЃ™г‚ігѓјгѓ‡г‚Јгѓіг‚°з’°еўѓгЂ‚',
        codeSubtitle: 'Claude гЃЇ UX гЃЁгѓ„гѓјгѓ«з·Ёж€ђгЃ®еЏ‚з…§гЃ§гЃ‚г‚ЉгЂЃruntime гЃ®жЁ©еЁЃгЃ§гЃЇгЃ‚г‚ЉгЃѕгЃ›г‚“гЂ‚',
      },
      composer: {
        currentMode: 'зЏѕењЁгЃ®гѓўгѓјгѓ‰',
        chatPlaceholder: 'еЏ–г‚Љзµ„гЃїгЃџгЃ„иЄїжџ»гЂЃз§»иЎЊгЂЃгЃѕгЃџгЃЇг‚»гѓѓг‚·гѓ§гѓігЃ®з›®зљ„г‚’иЄ¬жЋгЃ—гЃ¦гЃЏгЃ гЃ•гЃ„гЂ‚',
        codePlaceholder: 'runtime зµ±еђ€гЂЃIPCгЂЃrenderer е®џиЈ…гЂЃдє’жЏ›жЂ§еј·еЊ–гЃ«гЃ¤гЃ„гЃ¦дѕќй јгЃ—гЃ¦гЃЏгЃ гЃ•гЃ„гЂ‚',
        chatAction: 'иЄїжџ»г‚’й–‹е§‹',
        codeAction: 'г‚ігѓјгѓ‰гѓЇгѓјг‚Їгѓ•гѓ­гѓјг‚’й–‹гЃЏ',
      },
    },
  },
  ko: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'мѓ€ м„ём…',
        search: 'м„ём… кІЂмѓ‰',
        searchPlaceholder: 'м•„н‚¤н…ЌмІ нЉёлћ™ н•„н„°л§Ѓ',
        recent: 'мµњк·ј мЉ¤л €л“њ',
        sources: 'л¦¬м„њм№ лЇёлџ¬',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'qwen runtime мњ„м—ђм„њ лЏ™мћ‘н•лЉ” лЊЂн™”н• м…ё.',
        chatSubtitle: 'qwen нён™м„±мќ„ мњ м§Ђн•л©ґм„њ м»Ён…ЌмЉ¤нЉё, л©”лЄЁл¦¬, нћ€мЉ¤н† л¦¬ нѓђмѓ‰мќ„ лЌ°мЉ¤нЃ¬н†±м—ђ л§ћкІЊ л‹¤л“¬мЉµл‹€л‹¤.',
        codeTitle: 'qwen core кё°л°м—ђ Claude кё‰ м‚¬мљ©м„±мќ„ лЌ”н•њ мЅ”л”© мЅ•н•Џ.',
        codeSubtitle: 'Claude лЉ” UX м™Ђ лЏ„кµ¬ нќђл¦„мќ м°ёкі  лЊЂмѓЃмќј лїђ, runtime к¶Њн•њмћђлЉ” м•„л‹™л‹€л‹¤.',
      },
      composer: {
        currentMode: 'н„мћ¬ лЄЁл“њ',
        chatPlaceholder: 'л‹¤лЈЁкі  м‹¶мќЂ л¦¬м„њм№, л§€мќґк·ёл €мќґм… лђлЉ” м„ём… лЄ©н‘њлҐј м„¤лЄ…н•м„ёмљ”.',
        codePlaceholder: 'runtime н†µн•©, IPC мћ‘м—…, renderer кµ¬н„ лђлЉ” нён™м„± к°•н™”лҐј мљ”мІ­н•м„ёмљ”.',
        chatAction: 'л¦¬м„њм№ м‹њмћ‘',
        codeAction: 'мЅ”л“њ м›ЊнЃ¬н”ЊлЎњ м—ґкё°',
      },
    },
  },
  'pt-BR': {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Nova sessГЈo',
        search: 'Buscar sessГµes',
        searchPlaceholder: 'Filtrar trilhas de arquitetura',
        recent: 'Threads recentes',
        sources: 'Espelhos de pesquisa',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Uma camada conversacional sobre o runtime do qwen.',
        chatSubtitle: 'Mantemos a compatibilidade do qwen enquanto contexto, memГіria e histГіrico ficam naturais no desktop.',
        codeTitle: 'Um cockpit de cГіdigo baseado em qwen core com ergonomia de nГ­vel Claude.',
        codeSubtitle: 'Claude entra como referГЄncia de UX e orquestraГ§ГЈo de ferramentas, nГЈo como autoridade de runtime.',
      },
      composer: {
        currentMode: 'Modo atual',
        chatPlaceholder: 'Descreva a pesquisa, migraГ§ГЈo ou sessГЈo que vocГЄ quer conduzir.',
        codePlaceholder: 'PeГ§a integraГ§ГЈo de runtime, IPC, renderer ou reforГ§o de compatibilidade.',
        chatAction: 'Abrir pesquisa',
        codeAction: 'Abrir fluxo de cГіdigo',
      },
    },
  },
  tr: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Yeni oturum',
        search: 'Oturum ara',
        searchPlaceholder: 'Mimari akД±ЕџlarД± filtrele',
        recent: 'Son baЕџlД±klar',
        sources: 'AraЕџtД±rma aynalarД±',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'qwen runtime Гјzerinde Г§alД±Еџan bir konuЕџma kabuДџu.',
        chatSubtitle: 'qwen uyumluluДџunu korurken baДџlam, bellek ve geГ§miЕџ gezintisini masaГјstГјne uygun hale getiriyoruz.',
        codeTitle: 'qwen core temelli, Claude dГјzeyinde ergonomiye sahip bir kod kokpiti.',
        codeSubtitle: 'Claude yalnД±zca UX ve araГ§ akД±ЕџД± referansД±dД±r; runtime otoritesi deДџildir.',
      },
      composer: {
        currentMode: 'GeГ§erli mod',
        chatPlaceholder: 'Ele almak istediДџin araЕџtД±rmayД±, geГ§iЕџi veya oturum hedefini aГ§Д±kla.',
        codePlaceholder: 'Runtime entegrasyonu, IPC, renderer veya uyumluluk gГјГ§lendirmesi iste.',
        chatAction: 'AraЕџtД±rmayД± aГ§',
        codeAction: 'Kod akД±ЕџД±nД± aГ§',
      },
    },
  },
  ar: {
    translation: {
      ...sharedTranslations,
      sidebar: {
        newChat: 'Ш¬Щ„ШіШ© Ш¬ШЇЩЉШЇШ©',
        search: 'ШЁШ­Ш« ЩЃЩЉ Ш§Щ„Ш¬Щ„ШіШ§ШЄ',
        searchPlaceholder: 'ШЄШµЩЃЩЉШ© Щ…ШіШ§Ш±Ш§ШЄ Ш§Щ„Щ…Ш№Щ…Ш§Ш±ЩЉШ©',
        recent: 'Ш§Щ„Щ…Ш­Ш§ШЇШ«Ш§ШЄ Ш§Щ„ШЈШ®ЩЉШ±Ш©',
        sources: 'Щ…Ш±Ш§ЩЉШ§ Ш§Щ„ШЁШ­Ш«',
      },
      hero: {
        kicker: 'Qwen-first desktop architecture',
        chatTitle: 'Щ€Ш§Ш¬Щ‡Ш© Щ…Ш­Ш§ШЇШ«Ш© ЩЃЩ€Щ‚ qwen runtime.',
        chatSubtitle: 'Щ†Ш­Ш§ЩЃШё Ш№Щ„Щ‰ ШЄЩ€Ш§ЩЃЩ‚ qwen Щ…Ш№ Ш¬Ш№Щ„ Ш§Щ„ШіЩЉШ§Щ‚ Щ€Ш§Щ„Ш°Ш§ЩѓШ±Ш© Щ€Ш§Щ„ШЄШ§Ш±ЩЉШ® ШЈЩѓШ«Ш± Ш·ШЁЩЉШ№ЩЉШ© Ш№Щ„Щ‰ ШіШ·Ш­ Ш§Щ„Щ…ЩѓШЄШЁ.',
        codeTitle: 'Щ…Щ‚ШµЩ€Ш±Ш© ШЁШ±Щ…Ш¬Ш© Щ…ШЁЩ†ЩЉШ© Ш№Щ„Щ‰ qwen core Щ…Ш№ ШЄШ¬Ш±ШЁШ© Ш§ШіШЄШ®ШЇШ§Щ… ШЁЩ…ШіШЄЩ€Щ‰ Claude.',
        codeSubtitle: 'ЩЉЩЏШіШЄШ®ШЇЩ… Claude ЩѓЩ…Ш±Ш¬Ш№ Щ„ШЄШ¬Ш±ШЁШ© Ш§Щ„Ш§ШіШЄШ®ШЇШ§Щ… Щ€ШЄШЇЩЃЩ‚ Ш§Щ„ШЈШЇЩ€Ш§ШЄ ЩЃЩ‚Ш·ШЊ Щ€Щ„ЩЉШі ЩѓЩ…Ш±Ш¬Ш№ЩЉШ© runtime.',
      },
      composer: {
        currentMode: 'Ш§Щ„Щ€Ш¶Ш№ Ш§Щ„Ш­Ш§Щ„ЩЉ',
        chatPlaceholder: 'ШµЩЃ Щ…Щ‡Щ…Ш© Ш§Щ„ШЁШ­Ш« ШЈЩ€ Ш§Щ„ШЄШ±Ш­ЩЉЩ„ ШЈЩ€ Ш§Щ„Ш¬Щ„ШіШ© Ш§Щ„ШЄЩЉ ШЄШ±ЩЉШЇ Ш§Щ„Ш№Щ…Щ„ Ш№Щ„ЩЉЩ‡Ш§.',
        codePlaceholder: 'Ш§Ш·Щ„ШЁ ШЄЩѓШ§Щ…Щ„ runtime ШЈЩ€ ШЈШ№Щ…Ш§Щ„ IPC ШЈЩ€ ШЄЩ†ЩЃЩЉШ° renderer ШЈЩ€ ШЄШ№ШІЩЉШІ Ш§Щ„ШЄЩ€Ш§ЩЃЩ‚.',
        chatAction: 'ШЁШЇШЎ Ш§Щ„ШЁШ­Ш«',
        codeAction: 'ЩЃШЄШ­ Щ…ШіШ§Ш± Ш§Щ„ШЁШ±Щ…Ш¬Ш©',
      },
    },
  },
} as const
