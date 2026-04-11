import { Box, Input, Text, Button, VStack, HStack, IconButton } from '@chakra-ui/react';
import { motion, AnimatePresence } from 'framer-motion';
import { useState, useMemo, useEffect, useRef } from 'react';
import { Search, X } from 'lucide-react';
import { useBootstrap } from '@/hooks/useBootstrap';
import { useTranslation } from 'react-i18next';
import Sidebar from './Sidebar';
import ChatArea from './ChatArea';
import TitleBar from './TitleBar';
import {
  filterSessionsByNavigationMode,
  getProjectNameFromWorkingDirectory,
  groupProjectSessions,
  isProjectlessSession,
  type SessionNavigationMode,
} from './sessionNavigation';

export default function MainLayout() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [sidebarMode, setSidebarMode] = useState<SessionNavigationMode>('projects');
  const [selectedSessionId, setSelectedSessionId] = useState('');
  const [searchModalOpen, setSearchModalOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const { t } = useTranslation();
  const { bootstrap, activeTurnSessions } = useBootstrap();
  const searchInputRef = useRef<HTMLInputElement>(null);
  const sessions = bootstrap?.recentSessions ?? [];
  const sessionScopeOptions = useMemo(
    () => ({
      runtimeBaseDirectory: bootstrap?.qwenRuntime?.runtimeBaseDirectory ?? '',
      workspaceRoot: bootstrap?.workspaceRoot ?? '',
    }),
    [bootstrap?.qwenRuntime?.runtimeBaseDirectory, bootstrap?.workspaceRoot],
  );

  // Focus search input when modal opens
  useEffect(() => {
    if (searchModalOpen && searchInputRef.current) {
      searchInputRef.current.focus();
    }
  }, [searchModalOpen]);

  // Close modal on Escape
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setSearchModalOpen(false);
        setSearchTerm('');
      }
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, []);

  const openSearch = () => {
    setSearchModalOpen(true);
    setSearchTerm('');
  };

  const filteredSessions = useMemo(
    () => sessions.filter((conv) => (conv.title ?? '').toLowerCase().includes(searchTerm.toLowerCase())),
    [searchTerm, sessions],
  );

  const visibleSearchSessions = useMemo(
    () => filterSessionsByNavigationMode(filteredSessions, sidebarMode, sessionScopeOptions),
    [filteredSessions, sessionScopeOptions, sidebarMode],
  );

  const groupedAndFiltered = useMemo(
    () => groupProjectSessions(visibleSearchSessions, t('sidebar.otherProjects')),
    [t, visibleSearchSessions],
  );

  const orderedChatSessions = useMemo(
    () =>
      [...visibleSearchSessions].sort(
        (left, right) => new Date(right.lastActivity).getTime() - new Date(left.lastActivity).getTime(),
      ),
    [visibleSearchSessions],
  );

  const handleSelectSession = (sessionId: string) => {
    const nextSession = sessions.find((session) => session.sessionId === sessionId);
    if (nextSession) {
      setSidebarMode(isProjectlessSession(nextSession, sessionScopeOptions) ? 'chats' : 'projects');
    }

    setSelectedSessionId(sessionId);
    setSearchModalOpen(false);
    setSearchTerm('');
  };

  const handleNewChat = () => {
    setSelectedSessionId('');
  };

  return (
    <Box h="100vh" w="100vw" overflow="hidden" bg="gray.900" position="relative">
      {/* Title bar - always on top */}
      <Box position="absolute" top={0} left={0} right={0} zIndex={20}>
        <TitleBar onToggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)} />
      </Box>

      {/* Sidebar - under title bar */}
      <Sidebar
        isOpen={isSidebarOpen}
        onClose={() => setIsSidebarOpen(false)}
        sessions={sessions}
        activeTurnSessions={activeTurnSessions}
        selectedSessionId={selectedSessionId}
        mode={sidebarMode}
        runtimeBaseDirectory={sessionScopeOptions.runtimeBaseDirectory}
        workspaceRoot={sessionScopeOptions.workspaceRoot}
        onSelectSession={handleSelectSession}
        onNewChat={handleNewChat}
        onToggleMode={() => setSidebarMode((current) => current === 'projects' ? 'chats' : 'projects')}
        onOpenSearch={openSearch}
        onOpenSkills={() => console.log('Skills & Integrations')}
      />

      {/* Main content area - under title bar */}
      <Box
        ml={isSidebarOpen ? "260px" : "0"}
        mt="36px"
        h="calc(100vh - 36px)"
        transition="margin-left 0.3s ease"
        overflow="hidden"
      >
        <ChatArea
          isSidebarOpen={isSidebarOpen}
          onToggleSidebar={() => setIsSidebarOpen(true)}
          selectedSessionId={selectedSessionId}
          sidebarMode={sidebarMode}
          onSelectSession={handleSelectSession}
        />
      </Box>

      {/* Search Modal - centered on the whole app screen */}
      <AnimatePresence>
        {searchModalOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.15 }}
            style={{
              position: 'fixed',
              inset: 0,
              zIndex: 1000,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            {/* Backdrop */}
            <Box
              position="absolute"
              inset={0}
              bg="rgba(0, 0, 0, 0.5)"
              onClick={() => { setSearchModalOpen(false); setSearchTerm(''); }}
            />
            {/* Modal content */}
            <motion.div
              initial={{ opacity: 0, scale: 0.96, y: 8 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.96, y: 8 }}
              transition={{ duration: 0.15, ease: 'easeOut' }}
              style={{ position: 'relative', zIndex: 1 }}
            >
              <Box
                w="520px"
                maxW="90vw"
                bg="gray.800"
                border="1px solid"
                borderColor="gray.700"
                borderRadius="2xl"
                shadow="2xl"
              >
                {/* Search input row */}
                <HStack px={4} py={3}>
                  <Search size={16} color="#9494a2" />
                  <Input
                    ref={searchInputRef}
                    placeholder={t('search.placeholder')}
                    value={searchTerm}
                    onChange={(e) => setSearchTerm(e.target.value)}
                    bg="transparent"
                    border="none"
                    color="white"
                    fontSize="sm"
                    p={0}
                    _placeholder={{ color: 'gray.500' }}
                    _focusVisible={{ boxShadow: 'none' }}
                    flex={1}
                  />
                  <IconButton
                    aria-label={t('search.close')}
                    icon={<X size={16} />}
                    size="xs"
                    variant="ghost"
                    colorScheme="gray"
                    color="gray.500"
                    minW="24px"
                    w="24px"
                    h="24px"
                    borderRadius="md"
                    onClick={() => { setSearchModalOpen(false); setSearchTerm(''); }}
                    _hover={{ bg: 'gray.700', color: 'white' }}
                  />
                </HStack>

                <Box borderTop="1px solid" borderColor="gray.700" />

                {/* Results */}
                <Box
                  maxH="300px"
                  overflowY="auto"
                  p={3}
                  sx={{
                    scrollbarGutter: 'stable',
                    '&::-webkit-scrollbar': { width: '6px' },
                    '&::-webkit-scrollbar-track': { background: 'transparent' },
                    '&::-webkit-scrollbar-thumb': { background: '#5b5b67', borderRadius: '3px' },
                  }}
                >
                  {visibleSearchSessions.length > 0 ? (
                    sidebarMode === 'projects' ? (
                      <VStack spacing={1} align="stretch">
                        {groupedAndFiltered.map((group) => (
                          <Box key={group.name}>
                            <Text fontSize="xs" color="gray.500" fontWeight="medium" px={1} mb={1}>
                              {group.name}
                            </Text>
                            <VStack spacing={0} align="stretch">
                              {group.sessions.map((conv) => (
                                <Button
                                  key={conv.sessionId}
                                  variant="ghost"
                                  colorScheme="gray"
                                  onClick={() => handleSelectSession(conv.sessionId)}
                                  h="32px"
                                  px={2}
                                  justifyContent="flex-start"
                                  bg="transparent"
                                  _hover={{ bg: 'gray.700' }}
                                  borderRadius="md"
                                  whiteSpace="nowrap"
                                  fontSize="sm"
                                  color="gray.200"
                                >
                                  <Text overflow="hidden" textOverflow="ellipsis" flex={1} textAlign="left">
                                    {conv.title ?? ''}
                                  </Text>
                                </Button>
                              ))}
                            </VStack>
                          </Box>
                        ))}
                      </VStack>
                    ) : (
                      <VStack spacing={0} align="stretch">
                        {orderedChatSessions.map((conv) => (
                          <Button
                            key={conv.sessionId}
                            variant="ghost"
                            colorScheme="gray"
                            onClick={() => handleSelectSession(conv.sessionId)}
                            h="32px"
                            px={2}
                            justifyContent="space-between"
                            bg="transparent"
                            _hover={{ bg: 'gray.700' }}
                            borderRadius="md"
                            whiteSpace="nowrap"
                            fontSize="sm"
                            color="gray.200"
                          >
                            <Text overflow="hidden" textOverflow="ellipsis" flex={1} textAlign="left">
                              {conv.title ?? getProjectNameFromWorkingDirectory(conv.workingDirectory, t('sidebar.otherProjects'))}
                            </Text>
                          </Button>
                        ))}
                      </VStack>
                    )
                  ) : (
                    <Text textAlign="center" color="gray.500" fontSize="sm" py={4}>
                      {searchTerm ? t('search.noResults') : t('search.startTyping')}
                    </Text>
                  )}
                </Box>
              </Box>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </Box>
  );
}
