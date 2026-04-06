import { extendTheme, type ThemeConfig } from '@chakra-ui/react'

const config: ThemeConfig = {
  initialColorMode: 'dark',
  useSystemColorMode: false,
}

export const theme = extendTheme({
  config,
  colors: {
    brand: {
      50: '#f0effe',
      100: '#d5d3fc',
      200: '#bbb8fa',
      300: '#a19df8',
      400: '#8782f5',
      500: '#615CED',
      600: '#4e49d9',
      700: '#3c38b5',
      800: '#2b278f',
      900: '#1b1866',
    },
    gray: {
      50: '#f7f7f8',
      100: '#ededf0',
      200: '#d8d8de',
      300: '#b8b8c2',
      400: '#9494a2',
      500: '#72727f',
      600: '#5b5b67',
      700: '#46464f',
      800: '#2a2a30',
      900: '#18181b',
      950: '#0f0f12',
    },
  },
  components: {
    Button: {
      variants: {
        primary: {
          bg: 'brand.500',
          color: 'white',
          _hover: {
            bg: 'brand.600',
          },
        },
      },
    },
  },
  styles: {
    global: {
      body: {
        bg: 'gray.900',
        color: 'white',
      },
    },
  },
})