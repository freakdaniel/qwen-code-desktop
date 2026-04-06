import { useState, useEffect } from 'react';
import { VStack, Heading, Text, Center } from '@chakra-ui/react';
import { motion } from 'framer-motion';
import qwenLogo from './assets/qwen-logo.svg';
import AuthScreen from './components/screens/AuthScreen';
import MainLayout from './components/layout/MainLayout';

function App() {
  const [appState, setAppState] = useState<'loading' | 'auth' | 'main'>('loading');

  // Simulate loading and dependency check
  useEffect(() => {
    const timer = setTimeout(() => {
      // In a real app, this would check for configurations and auth status
      const isAuthenticated = localStorage.getItem('qwen-auth-token') !== null || localStorage.getItem('openai-api-key') !== null;
      setAppState(isAuthenticated ? 'main' : 'auth');
    }, 2000);

    return () => clearTimeout(timer);
  }, []);

  if (appState === 'loading') {
    return (
      <Center h="100vh" bg="gray.900">
        <VStack spacing={6}>
          <motion.img
            src={qwenLogo}
            alt="Qwen"
            initial={{ scale: 0, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            transition={{ duration: 0.5 }}
            style={{ height: '72px', width: '72px' }}
          />
          <VStack spacing={1}>
            <Heading size="md" color="white">Qwen Code</Heading>
            <Text color="gray.400" fontSize="sm">Initializing...</Text>
          </VStack>
        </VStack>
      </Center>
    );
  }

  if (appState === 'auth') {
    return <AuthScreen onComplete={() => setAppState('main')} />;
  }

  // Main app layout
  return <MainLayout />;
}

export default App;
