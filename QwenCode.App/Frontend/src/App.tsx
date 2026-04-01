import { startTransition, useDeferredValue, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import './App.css'
import type {
  AppBootstrapPayload,
  DesktopMode,
} from './types/desktop'

const fallbackBootstrap: AppBootstrapPayload = {
  productName: 'Qwen Code Desktop',
  currentMode: 'chat',
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
    workspaceRoot: 'E:\\Projects\\qwen-code-desktop',
    qwenRoot: 'E:\\Projects\\qwen-code-main',
    claudeRoot: 'E:\\Projects\\claude-code-main',
    ipcReferenceRoot: 'E:\\Projects\\HyPrism',
  },
  tracks: [
    {
      title: 'Keep qwen-core authoritative',
      summary: 'Desktop should wrap qwen runtime instead of replacing its execution model.',
    },
    {
      title: 'Borrow Claude UX contracts',
      summary: 'Structured tool, task, and approval flows should shape the renderer.',
    },
    {
      title: 'Use HyPrism IPC discipline',
      summary: 'Typed preload bridges keep Electron integration narrow and testable.',
    },
  ],
  compatibilityGoals: [
    'Do not break .qwen storage.',
    'Preserve qwen session compatibility.',
    'Keep runtime logic in qwen.',
    'Use Claude as a UX reference only.',
  ],
}

const sampleSessions = [
  'Desktop shell baseline',
  'Claude parity matrix',
  'IPC contract hardening',
  'Localization rollout',
  'Session compatibility audit',
  'Renderer tool timeline',
]

function App() {
  const { t, i18n } = useTranslation()
  const [bootstrap, setBootstrap] = useState<AppBootstrapPayload>(fallbackBootstrap)
  const [query, setQuery] = useState('')
  const [connected, setConnected] = useState(false)
  const deferredQuery = useDeferredValue(query)

  useEffect(() => {
    let dispose: (() => void) | undefined

    const hydrate = async () => {
      if (!window.qwenDesktop) {
        await i18n.changeLanguage(fallbackBootstrap.currentLocale)
        return
      }

      const payload = await window.qwenDesktop.bootstrap()
      setBootstrap(payload)
      setConnected(true)
      await i18n.changeLanguage(payload.currentLocale)

      dispose = window.qwenDesktop.subscribeStateChanged((event) => {
        setBootstrap((current) => ({
          ...current,
          currentMode: event.currentMode,
          currentLocale: event.currentLocale,
        }))

        startTransition(() => {
          void i18n.changeLanguage(event.currentLocale)
        })
      })
    }

    void hydrate()

    return () => dispose?.()
  }, [i18n])

  useEffect(() => {
    document.documentElement.dir = i18n.language === 'ar' ? 'rtl' : 'ltr'
  }, [i18n.language])

  const visibleSessions = sampleSessions.filter((item) =>
    item.toLowerCase().includes(deferredQuery.toLowerCase()),
  )

  const isCodeMode = bootstrap.currentMode === 'code'

  const handleModeChange = (mode: DesktopMode) => {
    setBootstrap((current) => ({ ...current, currentMode: mode }))

    const bridge = window.qwenDesktop
    if (!bridge) {
      return
    }

    startTransition(() => {
      void bridge.setMode(mode)
    })
  }

  const handleLocaleChange = (locale: string) => {
    startTransition(() => {
      void i18n.changeLanguage(locale)
      setBootstrap((current) => ({ ...current, currentLocale: locale }))
    })

    const bridge = window.qwenDesktop
    if (!bridge) {
      return
    }

    void bridge.setLocale(locale)
  }

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand-lockup">
          <div className="brand-mark">Q</div>
          <div>
            <div className="eyebrow">{t('app.workspace')}</div>
            <h1>{bootstrap.productName}</h1>
          </div>
        </div>

        <button className="primary-action" type="button">
          {t('sidebar.newChat')}
        </button>

        <label className="search-box">
          <span>{t('sidebar.search')}</span>
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t('sidebar.searchPlaceholder')}
          />
        </label>

        <section className="sidebar-section">
          <div className="section-title">{t('sidebar.recent')}</div>
          <div className="session-list">
            {visibleSessions.map((item) => (
              <button className="session-pill" key={item} type="button">
                {item}
              </button>
            ))}
          </div>
        </section>

        <section className="sidebar-section">
          <div className="section-title">{t('sidebar.sources')}</div>
          <div className="source-grid compact">
            <div className="source-card">
              <span>qwen-code</span>
              <strong>{bootstrap.sources.qwenRoot}</strong>
            </div>
            <div className="source-card">
              <span>claude-code</span>
              <strong>{bootstrap.sources.claudeRoot}</strong>
            </div>
            <div className="source-card">
              <span>HyPrism</span>
              <strong>{bootstrap.sources.ipcReferenceRoot}</strong>
            </div>
          </div>
        </section>
      </aside>

      <main className="workspace">
        <header className="topbar">
          <div className="mode-switch" role="tablist" aria-label={t('modes.title')}>
            {(['chat', 'code'] as DesktopMode[]).map((mode) => (
              <button
                aria-selected={bootstrap.currentMode === mode}
                className={bootstrap.currentMode === mode ? 'active' : ''}
                key={mode}
                onClick={() => handleModeChange(mode)}
                role="tab"
                type="button"
              >
                {t(`modes.${mode}`)}
              </button>
            ))}
          </div>

          <div className="topbar-actions">
            <span className="environment-chip">
              {connected ? t('status.connected') : t('status.local')}
            </span>

            <label className="locale-picker">
              <span>{t('top.locale')}</span>
              <select
                onChange={(event) => handleLocaleChange(event.target.value)}
                value={bootstrap.currentLocale}
              >
                {bootstrap.locales.map((locale) => (
                  <option key={locale.code} value={locale.code}>
                    {locale.nativeName}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </header>

        <section className="hero-panel">
          <div className="hero-copy">
            <span className="hero-kicker">{t('hero.kicker')}</span>
            <h2>{isCodeMode ? t('hero.codeTitle') : t('hero.chatTitle')}</h2>
            <p>{isCodeMode ? t('hero.codeSubtitle') : t('hero.chatSubtitle')}</p>
          </div>

          <div className="composer">
            <div className="composer-shell">
              <button className="composer-plus" type="button">
                +
              </button>
              <textarea
                defaultValue=""
                placeholder={isCodeMode ? t('composer.codePlaceholder') : t('composer.chatPlaceholder')}
                rows={4}
              />
            </div>
            <div className="composer-footer">
              <div className="composer-hints">
                <span>{t('composer.currentMode')}</span>
                <strong>{t(`modes.${bootstrap.currentMode}`)}</strong>
              </div>
              <button className="composer-submit" type="button">
                {isCodeMode ? t('composer.codeAction') : t('composer.chatAction')}
              </button>
            </div>
          </div>
        </section>

        <section className="content-grid">
          <article className="insight-card">
            <div className="card-heading">{t('content.optimization')}</div>
            <div className="research-grid">
              {bootstrap.tracks.map((track) => (
                <div className="research-tile" key={track.title}>
                  <h3>{track.title}</h3>
                  <p>{track.summary}</p>
                </div>
              ))}
            </div>
          </article>

          <article className="insight-card">
            <div className="card-heading">{t('content.compatibility')}</div>
            <div className="goal-list">
              {bootstrap.compatibilityGoals.map((goal, index) => (
                <div className="goal-item" key={goal}>
                  <span className="goal-index">{String(index + 1).padStart(2, '0')}</span>
                  <p>{goal}</p>
                </div>
              ))}
            </div>
          </article>

          <article className="insight-card wide">
            <div className="card-heading">{t('content.sources')}</div>
            <div className="source-grid">
              <div className="source-card">
                <span>{t('content.workspace')}</span>
                <strong>{bootstrap.sources.workspaceRoot}</strong>
              </div>
              <div className="source-card">
                <span>qwen-code</span>
                <strong>{bootstrap.sources.qwenRoot}</strong>
              </div>
              <div className="source-card">
                <span>claude-code</span>
                <strong>{bootstrap.sources.claudeRoot}</strong>
              </div>
              <div className="source-card">
                <span>{t('content.ipcReference')}</span>
                <strong>{bootstrap.sources.ipcReferenceRoot}</strong>
              </div>
            </div>
          </article>
        </section>
      </main>
    </div>
  )
}

export default App
