import { VStack, Heading, Text, Center, Box } from '@chakra-ui/react';
import { AnimatePresence, motion } from 'framer-motion';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import qwenLogoMarkup from './assets/qwen-logo.svg?raw';
import AuthScreen from './components/screens/AuthScreen';
import MainLayout from './components/layout/MainLayout';
import { useBootstrap } from './hooks/useBootstrap';

const SPLASH_MIN_VISIBLE_MS = 1200;

function App() {
  const { t } = useTranslation();
  const { authSnapshot, isReady } = useBootstrap();
  const [showSplash, setShowSplash] = useState(true);
  const splashVisibleAtRef = useRef<number | null>(null);

  useEffect(() => {
    let frameId = 0;
    let nestedFrameId = 0;

    frameId = window.requestAnimationFrame(() => {
      nestedFrameId = window.requestAnimationFrame(() => {
        splashVisibleAtRef.current ??= Date.now();
      });
    });

    return () => {
      window.cancelAnimationFrame(frameId);
      window.cancelAnimationFrame(nestedFrameId);
    };
  }, []);

  useEffect(() => {
    if (!isReady) {
      return;
    }

    const visibleSince = splashVisibleAtRef.current ?? Date.now();
    const elapsed = Date.now() - visibleSince;
    const delay = Math.max(0, SPLASH_MIN_VISIBLE_MS - elapsed);
    const timeoutId = window.setTimeout(() => setShowSplash(false), delay);
    return () => window.clearTimeout(timeoutId);
  }, [isReady]);

  const appContent = useMemo(
    () => (authSnapshot.status !== 'connected' ? <AuthScreen /> : <MainLayout />),
    [authSnapshot.status],
  );

  return (
    <Box h="100vh" bg="gray.900" position="relative" overflow="hidden">
      <AnimatePresence mode="wait">
        {showSplash ? (
          <motion.div
            key="app-splash"
            initial={{ opacity: 1 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0, scale: 1.015 }}
            transition={{ duration: 0.38, ease: [0.22, 1, 0.36, 1] }}
            style={{ position: 'absolute', inset: 0 }}
          >
            <Center h="100%">
              <VStack spacing={6}>
                <motion.div
                  initial={{ opacity: 0, scale: 0.78, y: 18, rotate: -8 }}
                  animate={{ opacity: 1, scale: 1, y: 0, rotate: 0 }}
                  transition={{ duration: 0.62, ease: [0.22, 1, 0.36, 1] }}
                >
                  <Box
                    aria-label="Qwen"
                    h="72px"
                    w="72px"
                    sx={{
                      '& svg': {
                        display: 'block',
                        width: '100%',
                        height: '100%',
                      },
                    }}
                    dangerouslySetInnerHTML={{ __html: qwenLogoMarkup }}
                  />
                </motion.div>
                <motion.div
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.42, delay: 0.12, ease: [0.22, 1, 0.36, 1] }}
                >
                  <VStack spacing={1}>
                    <Heading size="md" color="white">{t('titlebar.appName')}</Heading>
                    <Text color="gray.400" fontSize="sm">{t('app.initializing')}</Text>
                  </VStack>
                </motion.div>
              </VStack>
            </Center>
          </motion.div>
        ) : (
          <motion.div
            key="app-content"
            initial={{ opacity: 0, y: 22, scale: 0.99 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.42, ease: [0.22, 1, 0.36, 1] }}
            style={{ position: 'absolute', inset: 0 }}
          >
            {appContent}
          </motion.div>
        )}
      </AnimatePresence>
    </Box>
  );
}

export default App;
