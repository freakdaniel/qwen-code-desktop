import { Box, VStack, Heading, Text, Button, FormControl, Input, useToast, HStack, Link } from '@chakra-ui/react';
import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowRight, Loader2 } from 'lucide-react';
import qwenLogo from '../../assets/qwen-logo.svg';
import { useTranslation } from 'react-i18next';

export default function AuthScreen() {
  const { t } = useTranslation();
  const [showApiKey, setShowApiKey] = useState(false);
  const [apiKey, setApiKey] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const toast = useToast();

  const handleOAuthLogin = useCallback(async () => {
    if (isLoading || !window.qwenDesktop) return;
    setIsLoading(true);
    try {
      await window.qwenDesktop.startQwenOAuthDeviceFlow({ scope: '' });
    } catch (err) {
      toast({
        title: 'OAuth Error',
        description: String(err),
        status: 'error',
        duration: 4000,
        isClosable: true,
      });
    } finally {
      setIsLoading(false);
    }
  }, [isLoading, toast]);

  const handleApiKeySubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (isLoading || !window.qwenDesktop) return;
    if (!apiKey.trim()) {
      toast({
        title: 'API Key Required',
        description: 'Please enter your API key',
        status: 'error',
        duration: 3000,
        isClosable: true,
      });
      return;
    }
    setIsLoading(true);
    try {
      await window.qwenDesktop.configureOpenAiCompatibleAuth({
        scope: '',
        authType: 'api-key',
        model: '',
        baseUrl: '',
        apiKey: apiKey.trim(),
        apiKeyEnvironmentVariable: '',
      });
    } catch (err) {
      toast({
        title: 'Authentication Failed',
        description: String(err),
        status: 'error',
        duration: 4000,
        isClosable: true,
      });
    } finally {
      setIsLoading(false);
    }
  }, [apiKey, isLoading, toast]);

  const toggleToApiKey = useCallback(() => {
    if (isLoading) return;
    setShowApiKey(true);
    setApiKey('');
  }, [isLoading]);

  const toggleToOAuth = useCallback(() => {
    if (isLoading) return;
    setShowApiKey(false);
  }, [isLoading]);

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
    >
      <Box
        minH="100vh"
        bg="gray.900"
        display="flex"
        flexDirection="column"
        overflow="hidden"
        position="relative"
      >
        {/* Drag region (title bar area) */}
        <Box
          h="36px"
          sx={{ WebkitAppRegion: 'drag' } as React.CSSProperties}
          flexShrink={0}
        />

        {/* Main content - centered */}
        <Box
          flex="1"
          display="flex"
          alignItems="center"
          justifyContent="center"
          w="100%"
          p={4}
        >
          <Box maxW="md" width="100%" bg="transparent" p={8}>
            <VStack spacing={6} align="stretch">
              {/* Qwen Logo */}
              <VStack spacing={2} align="center">
                <Box
                  boxSize="64px"
                  display="flex"
                  alignItems="center"
                  justifyContent="center"
                >
                  <img src={qwenLogo} alt="Qwen" style={{ width: '64px', height: '64px' }} />
                </Box>
                <Heading size="lg" color="white">{t('auth.welcomeTitle')}</Heading>
                <Text color="gray.400" textAlign="center">
                  {t('auth.welcomeSubtitle')}
                </Text>
              </VStack>

              {/* Auth Mode Content */}
              <AnimatePresence mode="wait">
                {!showApiKey ? (
                  <motion.div
                    key="oauth"
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -10 }}
                    transition={{ duration: 0.25 }}
                  >
                    <VStack spacing={4}>
                      <Button
                        onClick={handleOAuthLogin}
                        size="lg"
                        width="100%"
                        bg="brand.500"
                        color="white"
                        _hover={{ bg: 'brand.600' }}
                        height="48px"
                        fontSize="md"
                        isDisabled={isLoading}
                        position="relative"
                      >
                        <AnimatePresence mode="wait">
                          {isLoading ? (
                            <motion.div
                              key="spinner"
                              initial={{ opacity: 0, scale: 0.8 }}
                              animate={{ opacity: 1, scale: 1 }}
                              exit={{ opacity: 0, scale: 0.8 }}
                              transition={{ duration: 0.15 }}
                              style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
                            >
                              <Loader2 size={18} className="animate-spin" />
                              <Text>{t('auth.connecting')}</Text>
                            </motion.div>
                          ) : (
                            <motion.div
                              key="text"
                              initial={{ opacity: 0 }}
                              animate={{ opacity: 1 }}
                              exit={{ opacity: 0 }}
                              transition={{ duration: 0.15 }}
                            >
                              {t('auth.signInWithQwen')}
                            </motion.div>
                          )}
                        </AnimatePresence>
                      </Button>
                    </VStack>
                  </motion.div>
                ) : (
                  <motion.div
                    key="apikey"
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: -10 }}
                    transition={{ duration: 0.25 }}
                  >
                    <form onSubmit={handleApiKeySubmit}>
                      <VStack spacing={4}>
                        <HStack width="100%" spacing={2}>
                          <FormControl flex="1">
                            <Input
                              type="password"
                              placeholder={t('auth.apiKeyPlaceholder')}
                              value={apiKey}
                              onChange={(e) => setApiKey(e.target.value)}
                              bg="gray.700"
                              borderColor="gray.600"
                              color="white"
                              _placeholder={{ color: 'gray.400' }}
                              height="48px"
                              fontSize="md"
                              _focus={{ borderColor: 'brand.500', boxShadow: `0 0 0 1px var(--chakra-colors-brand-500)` }}
                              isDisabled={isLoading}
                            />
                          </FormControl>
                          <Button
                            type="submit"
                            bg="brand.500"
                            color="white"
                            _hover={{ bg: 'brand.600' }}
                            width="48px"
                            height="48px"
                            minW="48px"
                            p={0}
                            display="flex"
                            alignItems="center"
                            justifyContent="center"
                            isDisabled={isLoading}
                          >
                            <AnimatePresence mode="wait">
                              {isLoading ? (
                                <motion.div
                                  key="spinner"
                                  initial={{ opacity: 0, scale: 0.8, rotate: 0 }}
                                  animate={{ opacity: 1, scale: 1, rotate: 360 }}
                                  exit={{ opacity: 0, scale: 0.8 }}
                                  transition={{ duration: 0.15, ease: "easeInOut" }}
                                >
                                  <Loader2 size={20} className="animate-spin" />
                                </motion.div>
                              ) : (
                                <motion.div
                                  key="arrow"
                                  initial={{ opacity: 0, scale: 0.8 }}
                                  animate={{ opacity: 1, scale: 1 }}
                                  exit={{ opacity: 0, scale: 0.8 }}
                                  transition={{ duration: 0.15 }}
                                >
                                  <ArrowRight size={20} />
                                </motion.div>
                              )}
                            </AnimatePresence>
                          </Button>
                        </HStack>
                      </VStack>
                    </form>
                  </motion.div>
                )}
              </AnimatePresence>
            </VStack>
          </Box>
        </Box>

        {/* Bottom link - positioned at bottom */}
        <Box
          position="absolute"
          bottom="32px"
          left="0"
          right="0"
          display="flex"
          justifyContent="center"
        >
          <AnimatePresence mode="wait">
            {isLoading ? (
              <motion.div
                key="hidden"
                initial={{ opacity: 1, y: 0 }}
                animate={{ opacity: 0, y: 10 }}
                exit={{ opacity: 0, y: 10 }}
                transition={{ duration: 0.15 }}
                style={{ visibility: 'hidden' }}
              >
                <Text fontSize="sm">&nbsp;</Text>
              </motion.div>
            ) : (
              <motion.div
                key="visible"
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -10 }}
                transition={{ duration: 0.2 }}
              >
                <Text fontSize="sm" color="gray.500">
                  {!showApiKey
                    ? `${t('auth.orUseApiKeyPrefix')} `
                    : `${t('auth.orUseQwenAccountPrefix')} `}
                  <Link
                    as="button"
                    color="brand.400"
                    cursor="pointer"
                    onClick={!showApiKey ? toggleToApiKey : toggleToOAuth}
                    _hover={{ color: 'brand.300', textDecoration: 'underline' }}
                    transition="color 0.15s ease"
                  >
                    {!showApiKey ? t('auth.useApiKey') : t('auth.useQwenAccount')}
                  </Link>
                </Text>
              </motion.div>
            )}
          </AnimatePresence>
        </Box>
      </Box>
    </motion.div>
  );
}
