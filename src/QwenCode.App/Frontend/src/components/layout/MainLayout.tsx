import { Box, Input, Text, Button, VStack, HStack, IconButton } from '@chakra-ui/react';
import { motion, AnimatePresence } from 'framer-motion';
import { useState, useMemo, useEffect, useRef } from 'react';
import { Search, X } from 'lucide-react';
import { useBootstrap } from '@/hooks/useBootstrap';
import { useTranslation } from 'react-i18next';
import Sidebar from './Sidebar';
import ChatArea from './ChatArea';
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
  const [lastSelectedProjectSessionId, setLastSelectedProjectSessionId] = useState('');
  const [lastSelectedChatSessionId, setLastSelectedChatSessionId] = useState('');
  const [searchModalOpen, setSearchModalOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const { t } = useTranslation();
  const { bootstrap, activeTurnSessions, setBootstrap, setSessionCache } = useBootstrap();
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

  const resolveRememberedSessionId = (
    nextMode: SessionNavigationMode,
    rememberedSessionId: string,
  ) => {
    if (!rememberedSessionId) {
      return '';
    }

    const rememberedSession = sessions.find((session) => session.sessionId === rememberedSessionId);
    if (!rememberedSession) {
      return '';
    }

    const rememberedMode = isProjectlessSession(rememberedSession, sessionScopeOptions) ? 'chats' : 'projects';
    return rememberedMode === nextMode ? rememberedSessionId : '';
  };

  const handleSelectSession = (sessionId: string) => {
    const nextSession = sessions.find((session) => session.sessionId === sessionId);
    if (nextSession) {
      const nextMode = isProjectlessSession(nextSession, sessionScopeOptions) ? 'chats' : 'projects';
      setSidebarMode(nextMode);
      if (nextMode === 'chats') {
        setLastSelectedChatSessionId(sessionId);
      } else {
        setLastSelectedProjectSessionId(sessionId);
      }
    }

    setSelectedSessionId(sessionId);
    setSearchModalOpen(false);
    setSearchTerm('');
  };

  const handleNewChat = () => {
    setSelectedSessionId('');
  };

  const handleRenameSession = async (sessionId: string) => {
    const session = sessions.find((item) => item.sessionId === sessionId);
    if (!session || !window.qwenDesktop?.renameSession) {
      return;
    }

    const nextTitle = window.prompt(t('sidebar.renameChatPrompt'), session.title ?? '');
    if (nextTitle === null) {
      return;
    }

    const trimmedTitle = nextTitle.trim();
    if (!trimmedTitle || trimmedTitle === (session.title ?? '').trim()) {
      return;
    }

    const result = await window.qwenDesktop.renameSession({
      sessionId,
      title: trimmedTitle,
    });

    setBootstrap((current) => ({
      ...current,
      recentSessions: current.recentSessions.map((item) =>
        item.sessionId === sessionId ? { ...item, title: result.title || trimmedTitle } : item,
      ),
    }));
    setSessionCache((current) => {
      const detail = current[sessionId];
      if (!detail) return current;

      return {
        ...current,
        [sessionId]: {
          ...detail,
          session: {
            ...detail.session,
            title: trimmedTitle,
          },
        },
      };
    });
  };

  const handleDeleteSession = async (sessionId: string) => {
    const session = sessions.find((item) => item.sessionId === sessionId);
    if (!session || !window.qwenDesktop?.removeSession) {
      return;
    }

    if (!window.confirm(t('sidebar.deleteChatConfirm', { title: session.title ?? session.sessionId }))) {
      return;
    }

    await window.qwenDesktop.removeSession({ sessionId });
    setBootstrap((current) => ({
      ...current,
      recentSessions: current.recentSessions.filter((item) => item.sessionId !== sessionId),
    }));
    setSessionCache((current) => {
      const next = { ...current };
      delete next[sessionId];
      return next;
    });

    if (selectedSessionId === sessionId) {
      setSelectedSessionId('');
    }
    if (lastSelectedChatSessionId === sessionId) {
      setLastSelectedChatSessionId('');
    }
    if (lastSelectedProjectSessionId === sessionId) {
      setLastSelectedProjectSessionId('');
    }
  };

  const handleToggleMode = () => {
    const nextMode = sidebarMode === 'projects' ? 'chats' : 'projects';
    const nextSelectedSessionId = nextMode === 'chats'
      ? resolveRememberedSessionId(nextMode, lastSelectedChatSessionId)
      : resolveRememberedSessionId(nextMode, lastSelectedProjectSessionId);

    setSidebarMode(nextMode);
    setSelectedSessionId(nextSelectedSessionId);
    setSearchModalOpen(false);
    setSearchTerm('');
  };

  useEffect(() => {
    if (!selectedSessionId) {
      return;
    }

    const selectedSession = sessions.find((session) => session.sessionId === selectedSessionId);
    if (!selectedSession) {
      setSelectedSessionId('');
    }
  }, [selectedSessionId, sessions]);

  useEffect(() => {
    if (
      lastSelectedProjectSessionId &&
      !sessions.some((session) => session.sessionId === lastSelectedProjectSessionId)
    ) {
      setLastSelectedProjectSessionId('');
    }

    if (
      lastSelectedChatSessionId &&
      !sessions.some((session) => session.sessionId === lastSelectedChatSessionId)
    ) {
      setLastSelectedChatSessionId('');
    }
  }, [lastSelectedChatSessionId, lastSelectedProjectSessionId, sessions]);

  return (
    <Box h="100vh" w="100vw" overflow="hidden" bg="#1f1f23" position="relative">
      <HStack h="100%" w="100%" spacing={0} align="stretch">
        <Sidebar
          isOpen={isSidebarOpen}
          onClose={() => setIsSidebarOpen((current) => !current)}
          sessions={sessions}
          activeTurnSessions={activeTurnSessions}
          selectedSessionId={selectedSessionId}
          mode={sidebarMode}
          runtimeBaseDirectory={sessionScopeOptions.runtimeBaseDirectory}
          workspaceRoot={sessionScopeOptions.workspaceRoot}
          onSelectSession={handleSelectSession}
          onNewChat={handleNewChat}
          onToggleMode={handleToggleMode}
          onOpenSearch={openSearch}
          onOpenSkills={() => console.log('Skills & Integrations')}
          onRenameSession={(session) => void handleRenameSession(session.sessionId)}
          onDeleteSession={(session) => void handleDeleteSession(session.sessionId)}
        />

        <Box flex={1} minW={0} h="100%" overflow="hidden">
          <ChatArea
            selectedSessionId={selectedSessionId}
            sidebarMode={sidebarMode}
            onSelectSession={handleSelectSession}
          />
        </Box>
      </HStack>

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
              layout
              initial={{ opacity: 0, scale: 0.96, y: 8 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.96, y: 8 }}
              transition={{
                duration: 0.15,
                ease: 'easeOut',
                layout: { duration: 0.22, ease: [0.22, 1, 0.36, 1] },
              }}
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
                <motion.div
                  layout
                  transition={{ layout: { duration: 0.22, ease: [0.22, 1, 0.36, 1] } }}
                >
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
                </motion.div>
              </Box>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </Box>
  );
}
