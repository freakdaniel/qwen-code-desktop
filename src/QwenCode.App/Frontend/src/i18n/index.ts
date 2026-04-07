import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

// Map browser language tags to our locale file names
const BROWSER_LANG_MAP: Record<string, string> = {
  // Russian
  'ru': 'ru-RU',
  'ru-RU': 'ru-RU',
  // Chinese
  'zh': 'zh-CN',
  'zh-CN': 'zh-CN',
  'zh-Hans': 'zh-CN',
  // Japanese
  'ja': 'ja-JP',
  'ja-JP': 'ja-JP',
  // Korean
  'ko': 'ko-KR',
  'ko-KR': 'ko-KR',
  // Portuguese (Brazil)
  'pt': 'pt-BR',
  'pt-BR': 'pt-BR',
  'pt-PT': 'pt-BR',
  // German
  'de': 'de-DE',
  'de-DE': 'de-DE',
  'de-AT': 'de-DE',
  'de-CH': 'de-DE',
  // French
  'fr': 'fr-FR',
  'fr-FR': 'fr-FR',
  'fr-CA': 'fr-FR',
  // Spanish
  'es': 'es-ES',
  'es-ES': 'es-ES',
  'es-MX': 'es-ES',
  // Turkish
  'tr': 'tr-TR',
  'tr-TR': 'tr-TR',
  // Arabic
  'ar': 'ar-SA',
  'ar-SA': 'ar-SA',
}

/**
 * Detect the system/browser language at startup.
 * Uses navigator.language (matches browser/OS language setting).
 * Falls back to English if nothing matches.
 */
function detectBrowserLocale(): string {
  try {
    const browserLang = navigator.language || ''
    const full = browserLang
    const base = browserLang.split('-')[0]

    // Try exact match first
    if (BROWSER_LANG_MAP[full]) return BROWSER_LANG_MAP[full]
    // Try base language
    if (BROWSER_LANG_MAP[base]) return BROWSER_LANG_MAP[base]
  } catch {
    // navigator might not be available
  }

  return 'en-US'
}

// Pre-loaded resources (imported at build time)
import enUS from './locales/en-US.json'
import ruRU from './locales/ru-RU.json'
import zhCN from './locales/zh-CN.json'
import jaJP from './locales/ja-JP.json'
import koKR from './locales/ko-KR.json'
import ptBR from './locales/pt-BR.json'

const RESOURCES: Record<string, Record<string, unknown>> = {
  'en-US': enUS,
  'ru-RU': ruRU,
  'zh-CN': zhCN,
  'ja-JP': jaJP,
  'ko-KR': koKR,
  'pt-BR': ptBR,
}

/**
 * Initialize i18n with auto-detected system language.
 * Called BEFORE React renders — no language flash.
 */
export async function initI18n(): Promise<void> {
  const locale = detectBrowserLocale()
  const resources = RESOURCES[locale] || RESOURCES['en-US']

  await i18n.use(initReactI18next).init({
    resources: {
      [locale]: { translation: resources },
    },
    lng: locale,
    fallbackLng: 'en-US',
    supportedLngs: Object.values(BROWSER_LANG_MAP),
    interpolation: {
      escapeValue: false,
    },
  })
}

/**
 * Change language at runtime. Uses pre-loaded resources.
 */
export async function changeLanguage(locale: string): Promise<void> {
  // Normalize to our locale format
  const mapped = BROWSER_LANG_MAP[locale] || BROWSER_LANG_MAP[locale.split('-')[0]] || locale
  const localeKey = RESOURCES[mapped] ? mapped : 'en-US'

  if (!i18n.hasResourceBundle(localeKey, 'translation')) {
    const resources = RESOURCES[localeKey]
    if (resources) {
      i18n.addResourceBundle(localeKey, 'translation', resources)
    }
  }

  await i18n.changeLanguage(localeKey)
}

export default i18n
