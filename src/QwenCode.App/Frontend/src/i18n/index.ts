import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { resources } from './resources'

// Determine the initial language: system locale first, fallback to 'en'
function getInitialLanguage(): string {
  const supported = ['en', 'ru', 'zh-CN', 'ja', 'ko', 'pt-BR']

  // Try navigator language
  if (typeof navigator !== 'undefined') {
    // Check user's primary language
    if (navigator.language && supported.includes(navigator.language)) return navigator.language
    // Check all preferred languages
    if (navigator.languages) {
      for (const lang of navigator.languages) {
        // Exact match
        if (supported.includes(lang)) return lang
        // Prefix match (e.g. 'ru-RU' -> 'ru', 'zh-Hans-CN' -> 'zh-CN')
        const short = lang.split('-')[0]
        const match = supported.find((s) => s === short || s.startsWith(short + '-'))
        if (match) return match
      }
    }
    // Fallback to browser language property
    const short = navigator.language.split('-')[0]
    const match = supported.find((s) => s === short || s.startsWith(short + '-'))
    if (match) return match
  }

  return 'en'
}

void i18n.use(initReactI18next).init({
  resources,
  lng: getInitialLanguage(),
  fallbackLng: 'en',
  supportedLngs: ['en', 'ru', 'zh-CN', 'ja', 'ko', 'pt-BR'],
  interpolation: {
    escapeValue: false,
  },
})

export default i18n
