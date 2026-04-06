import { ChakraProvider, ColorModeScript } from '@chakra-ui/react'
import { createRoot } from 'react-dom/client'
import { theme } from './theme'
import './index.css'
import App from './App.tsx'
import './i18n/index.ts'

createRoot(document.getElementById('root')!).render(
  <>
    <ColorModeScript initialColorMode={theme.config.initialColorMode} />
    <ChakraProvider theme={theme}>
      <App />
    </ChakraProvider>
  </>
)
