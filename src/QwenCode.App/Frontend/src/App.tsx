import { VStack, Heading, Text, Center } from '@chakra-ui/react';
import { motion } from 'framer-motion';
import { useTranslation } from 'react-i18next';
import qwenLogo from './assets/qwen-logo.svg';
import AuthScreen from './components/screens/AuthScreen';
import MainLayout from './components/layout/MainLayout';
import { useBootstrap } from './hooks/useBootstrap';

function App() {
  const { t } = useTranslation();
  const { authSnapshot, isReady } = useBootstrap();

  if (!isReady) {
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
            <Text color="gray.400" fontSize="sm">{t('app.initializing')}</Text>
          </VStack>
        </VStack>
      </Center>
    );
  }

  if (authSnapshot.status !== 'connected') {
    return <AuthScreen />;
  }

  return <MainLayout />;
}

export default App;
