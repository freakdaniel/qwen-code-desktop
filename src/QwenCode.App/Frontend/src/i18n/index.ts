import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { resources } from './resources'

void i18n.use(initReactI18next).init({
  resources,
  lng: 'en', // Default to English
  fallbackLng: 'en',
  supportedLngs: ['en', 'ru', 'zh-CN', 'ja', 'ko', 'pt-BR'], // Supported languages
  interpolation: {
    escapeValue: false,
  },
})

export default i18n

