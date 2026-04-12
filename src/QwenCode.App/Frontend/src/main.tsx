import { ChakraProvider, ColorModeScript } from '@chakra-ui/react'
import { createRoot } from 'react-dom/client'
import 'katex/dist/katex.min.css'
import { theme } from './theme'
import './index.css'
import App from './App.tsx'
import { initI18n } from './i18n/index.ts'
import { BootstrapProvider } from './hooks/useBootstrap.ts'

// Initialize i18n BEFORE rendering so the loading screen
// already uses the correct system language
await initI18n()

createRoot(document.getElementById('root')!).render(
  <>
    <ColorModeScript initialColorMode={theme.config.initialColorMode} />
    <ChakraProvider theme={theme}>
      <BootstrapProvider>
        <App />
      </BootstrapProvider>
    </ChakraProvider>
  </>
)
