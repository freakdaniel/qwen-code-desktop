import {
  Box,
  VStack,
  Text,
  Button,
  HStack,
  Skeleton,
} from '@chakra-ui/react';
import { AnimatePresence, motion } from 'framer-motion';
import { Plus, Search, Settings, ChevronRight, FolderOpen, Folder, Puzzle, MessageCircle, Code2 } from 'lucide-react';
import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { SessionPreview } from '@/types/desktop';
import {
  filterSessionsByNavigationMode,
  groupProjectSessions,
  type SessionNavigationMode,
} from './sessionNavigation';

interface SidebarProps {
  isOpen: boolean;
  onClose: () => void;
  sessions: SessionPreview[];
  activeTurnSessions: Record<string, true>;
  selectedSessionId?: string;
  mode: SessionNavigationMode;
  runtimeBaseDirectory?: string;
  workspaceRoot?: string;
  onNewChat?: () => void;
  onSelectSession?: (sessionId: string) => void;
  onToggleMode?: () => void;
  onOpenSettings?: () => void;
  onOpenSearch?: () => void;
  onOpenSkills?: () => void;
}

interface ChatSection {
  key: string;
  label: string;
  sessions: SessionPreview[];
}

function formatRelativeTime(dateStr: string, t: ReturnType<typeof useTranslation>['t']): string {
  const now = Date.now();
  const then = new Date(dateStr).getTime();
  const diffMs = now - then;
  if (diffMs < 0) return t('sidebar.now');
  if (diffMs < 60_000) return t('sidebar.now');
  if (diffMs < 3_600_000) return `${Math.floor(diffMs / 60_000)}${t('sidebar.minutesAgo')}`;
  if (diffMs < 86_400_000) return `${Math.floor(diffMs / 3_600_000)}${t('sidebar.hoursAgo')}`;
  if (diffMs < 604_800_000) return `${Math.floor(diffMs / 86_400_000)}${t('sidebar.daysAgo')}`;
  return `${Math.floor(diffMs / 604_800_000)}${t('sidebar.weeksAgo')}`;
}

// Animation variants for group sessions list
const sessionsListVariants = {
  hidden: {
    opacity: 0,
    height: 0,
    transition: {
      height: { duration: 0.2, ease: 'easeInOut' },
      opacity: { duration: 0.15, ease: 'easeInOut' },
    },
  },
  visible: {
    opacity: 1,
    height: 'auto',
    transition: {
      height: { duration: 0.25, ease: 'easeInOut' },
      opacity: { duration: 0.2, ease: 'easeInOut', delay: 0.05 },
    },
  },
};

// Stagger animation for individual session items
const sessionItemVariants = {
  hidden: { opacity: 0, x: -8 },
  visible: (i: number) => ({
    opacity: 1,
    x: 0,
    transition: { opacity: { duration: 0.15 }, x: { duration: 0.2 }, delay: i * 0.03 },
  }),
  exit: { opacity: 0, x: -4, transition: { duration: 0.1 } },
};

export default function Sidebar({
  isOpen,
  sessions,
  activeTurnSessions,
  selectedSessionId = '',
  mode,
  runtimeBaseDirectory = '',
  workspaceRoot = '',
  onNewChat = () => console.log('New chat'),
  onSelectSession = (id: string) => console.log(`Selected conversation ${id}`),
  onToggleMode = () => console.log('Sidebar mode toggled'),
  onOpenSettings = () => console.log('Settings clicked'),
  onOpenSearch = () => console.log('Search opened'),
  onOpenSkills = () => console.log('Skills clicked'),
}: SidebarProps) {
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const { t, i18n } = useTranslation();

  const visibleSessions = useMemo(
    () => filterSessionsByNavigationMode(sessions, mode, { runtimeBaseDirectory, workspaceRoot }),
    [mode, runtimeBaseDirectory, sessions, workspaceRoot],
  );

  const groupedConversations = useMemo(
    () => groupProjectSessions(visibleSessions, t('sidebar.otherProjects')),
    [t, visibleSessions],
  );

  const orderedChatSessions = useMemo(
    () =>
      [...visibleSessions].sort(
        (left, right) => new Date(right.lastActivity).getTime() - new Date(left.lastActivity).getTime(),
      ),
    [visibleSessions],
  );

  const chatSections = useMemo(() => {
    if (mode !== 'chats') {
      return [];
    }

    const language = i18n.language || 'en-US';
    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const recentCutoff = new Date(now);
    recentCutoff.setDate(recentCutoff.getDate() - 30);

    const sections = new Map<string, ChatSection>();

    const resolveSection = (session: SessionPreview): ChatSection => {
      const activityDate = new Date(session.lastActivity);
      const startOfActivityDay = new Date(
        activityDate.getFullYear(),
        activityDate.getMonth(),
        activityDate.getDate(),
      );
      const dayDifference = Math.floor(
        (startOfToday.getTime() - startOfActivityDay.getTime()) / 86_400_000,
      );

      if (dayDifference <= 0) {
        return {
          key: 'today',
          label: t('sidebar.today'),
          sessions: [],
        };
      }

      if (dayDifference === 1) {
        return {
          key: 'yesterday',
          label: t('sidebar.yesterday'),
          sessions: [],
        };
      }

      if (dayDifference === 2) {
        return {
          key: 'day-before-yesterday',
          label: t('sidebar.dayBeforeYesterday'),
          sessions: [],
        };
      }

      if (dayDifference <= 7) {
        return {
          key: 'previous-week',
          label: t('sidebar.previousWeek'),
          sessions: [],
        };
      }

      if (activityDate >= recentCutoff) {
        return {
          key: 'recent-30-days',
          label: t('sidebar.previousThirtyDays'),
          sessions: [],
        };
      }

      const monthLabel = new Intl.DateTimeFormat(language, {
        month: 'long',
      }).format(activityDate);
      const monthKey = `${activityDate.getFullYear()}-${activityDate.getMonth()}`;

      return {
        key: monthKey,
        label: monthLabel,
        sessions: [],
      };
    };

    for (const session of orderedChatSessions) {
      const section = resolveSection(session);
      const existing = sections.get(section.key) ?? section;
      existing.sessions.push(session);
      sections.set(section.key, existing);
    }

    return Array.from(sections.values());
  }, [i18n.language, mode, orderedChatSessions, t]);

  const toggleGroup = (name: string) => {
    setOpenGroups(prev => ({
      ...prev,
      [name]: !(prev[name] !== false),
    }));
  };

  const renderSessionButton = (conv: SessionPreview) => {
    const isSelected = conv.sessionId === selectedSessionId;
    const isRunning = conv.sessionId in activeTurnSessions;

    return (
      <Button
        key={conv.sessionId}
        variant="ghost"
        colorScheme="gray"
        onClick={() => onSelectSession(conv.sessionId)}
        h="38px"
        px={3}
        py={0}
        justifyContent="space-between"
        w="full"
        minW={0}
        bg={isSelected ? '#3a3a42' : 'transparent'}
        color={isSelected ? 'white' : 'gray.200'}
        _hover={{ bg: isSelected ? '#404049' : 'rgba(255,255,255,0.06)' }}
        _active={{ bg: isSelected ? '#404049' : 'rgba(255,255,255,0.08)' }}
        borderRadius="full"
        fontSize="sm"
        fontWeight="normal"
        boxShadow={isSelected ? '0 0 0 1px rgba(255,255,255,0.04) inset' : 'none'}
      >
        <Box flex={1} minW={0} pr={2}>
          {conv.title === null ? (
            <Skeleton
              h="14px"
              w="120px"
              borderRadius="sm"
              startColor="gray.700"
              endColor="gray.600"
              flexShrink={0}
            />
          ) : (
            <Text
              color="inherit"
              display="block"
              fontWeight="normal"
              minW={0}
              overflow="hidden"
              textOverflow="ellipsis"
              whiteSpace="nowrap"
              textAlign="left"
            >
              {conv.title}
            </Text>
          )}
        </Box>
        {mode !== 'chats' && (
          <HStack spacing={2} ml={3} flexShrink={0}>
            <Text fontSize="xs" color={isSelected ? 'gray.300' : 'gray.500'}>
              {formatRelativeTime(conv.lastActivity, t)}
            </Text>
            <Box
              boxSize="6px"
              borderRadius="full"
              bg={isRunning ? 'green.400' : 'transparent'}
              transition="background-color 0.2s ease"
            />
          </HStack>
        )}
        {mode === 'chats' && isRunning && (
          <Box
            boxSize="6px"
            borderRadius="full"
            bg="green.400"
            flex={1}
            transition="background-color 0.2s ease"
          />
        )}
      </Button>
    );
  };

  return (
    <motion.div
      initial={{ x: '-100%' }}
      animate={{ x: isOpen ? 0 : '-100%' }}
      transition={{ type: 'spring', damping: 25, stiffness: 200 }}
      style={{
        position: 'absolute',
        left: 0,
        top: '36px',
        height: 'calc(100vh - 36px)',
        width: '260px',
        zIndex: 10,
        overflow: 'hidden',
      }}
    >
      <VStack
        h="100%"
        spacing={0}
        align="stretch"
        bg="gray.800"
        borderRight="1px solid"
        borderColor="gray.700"
      >
        {/* Top: Settings & Skills */}
        <Box px={3} pt={3} pb={2}>
          <VStack spacing={1} align="stretch">
            <Button
              leftIcon={mode === 'projects' ? <MessageCircle size={15} /> : <Code2 size={15} />}
              variant="ghost"
              colorScheme="gray"
              size="sm"
              width="100%"
              justifyContent="flex-start"
              fontWeight="regular"
              borderRadius="md"
              h="36px"
              onClick={onToggleMode}
              color="gray.400"
              _hover={{ bg: 'transparent', color: 'white' }}
              _active={{ bg: 'transparent', color: 'white' }}
              transition="color 0.2s ease"
            >
              {mode === 'projects' ? t('top.chats') : t('top.coder')}
            </Button>
            <Button
              leftIcon={<Puzzle size={15} />}
              variant="ghost"
              colorScheme="gray"
              size="sm"
              width="100%"
              justifyContent="flex-start"
              fontWeight="regular"
              borderRadius="md"
              h="36px"
              onClick={onOpenSkills}
              color="gray.400"
              _hover={{ bg: 'transparent', color: 'white' }}
              _active={{ bg: 'transparent', color: 'white' }}
              transition="color 0.2s ease"
            >
              {t('top.skills')}
            </Button>
            <Button
              leftIcon={<Settings size={15} />}
              variant="ghost"
              colorScheme="gray"
              size="sm"
              width="100%"
              justifyContent="flex-start"
              fontWeight="regular"
              borderRadius="md"
              h="36px"
              onClick={onOpenSettings}
              color="gray.400"
              _hover={{ bg: 'transparent', color: 'white' }}
              _active={{ bg: 'transparent', color: 'white' }}
              transition="color 0.2s ease"
            >
              {t('top.settings')}
            </Button>
          </VStack>
        </Box>

        {/* Combined New Chat + Search block */}
        <Box px={3} py={2}>
          <Box
            borderRadius="xl"
            overflow="hidden"
            border="1px solid"
            borderColor="gray.600"
          >
            <Button
              leftIcon={<Plus size={15} />}
              bg="brand.500"
              color="white"
              variant="solid"
              size="sm"
              width="100%"
              h="36px"
              borderRadius="0"
              onClick={onNewChat}
              transition="background-color 0.2s ease"
              _hover={{ bg: 'brand.600' }}
              _active={{ bg: 'brand.800' }}
            >
              {t('sidebar.newChat')}
            </Button>

            <Box h="1px" bg="gray.600" />

            {/* Search — opens modal */}
            <Box
              role="button"
              tabIndex={0}
              h="36px"
              px={3}
              display="flex"
              alignItems="center"
              justifyContent="center"
              bg="gray.700"
              cursor="pointer"
              transition="background-color 0.2s ease"
              _hover={{ bg: '#3a3a42' }}
              onClick={onOpenSearch}
              onKeyDown={(e) => { if (e.key === 'Enter') onOpenSearch(); }}
            >
              <HStack spacing={2}>
                <Search size={14} color="#9494a2" />
                <Text fontSize="sm" color="gray.400">{t('sidebar.search')}</Text>
              </HStack>
            </Box>
          </Box>
        </Box>

        {/* Grouped conversations */}
        <Box
          flex={1}
          overflowY="auto"
          overflowX="hidden"
          py={1}
          px={3}
          sx={{
            scrollbarGutter: 'stable',
            scrollbarWidth: 'none',
            msOverflowStyle: 'none',
            '&::-webkit-scrollbar': {
              width: '0px',
              height: '0px',
              display: 'none',
            },
          }}
        >
          <VStack spacing={1} align="stretch">
            {mode === 'projects' ? groupedConversations.map((group) => {
              const isGroupOpen = openGroups[group.name] !== false;
              return (
                <Box key={group.name}>
                  <Button
                    variant="ghost"
                    w="full"
                    h="28px"
                    px={1}
                    justifyContent="flex-start"
                    color="gray.500"
                    fontSize="xs"
                    fontWeight="medium"
                    _hover={{ bg: 'transparent', color: 'gray.300' }}
                    _active={{ bg: 'transparent', color: 'gray.300' }}
                    transition="color 0.2s ease"
                    onClick={() => toggleGroup(group.name)}
                    leftIcon={
                      <ChevronRight
                        size={12}
                        style={{
                          transition: 'transform 0.2s ease',
                          transform: isGroupOpen ? 'rotate(90deg)' : 'none',
                        }}
                      />
                    }
                  >
                    {isGroupOpen ? (
                      <FolderOpen size={12} style={{ marginRight: '4px' }} />
                    ) : (
                      <Folder size={12} style={{ marginRight: '4px' }} />
                    )}
                    {group.name}
                  </Button>

                  <AnimatePresence initial={false}>
                    {isGroupOpen && (
                      <motion.div
                        variants={sessionsListVariants}
                        initial="hidden"
                        animate="visible"
                        exit="hidden"
                        style={{ overflow: 'hidden', width: '100%' }}
                      >
                        <VStack spacing={1} align="stretch">
                          {group.sessions.map((conv, idx) => (
                            <motion.div
                              key={conv.sessionId}
                              variants={sessionItemVariants}
                              initial="hidden"
                              animate="visible"
                              exit="exit"
                              custom={idx}
                              style={{ width: '100%' }}
                            >
                              {renderSessionButton(conv)}
                            </motion.div>
                          ))}
                        </VStack>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </Box>
              );
            }) : chatSections.map((section) => (
              <Box key={section.key}>
                <Text
                  px={2}
                  pt={1}
                  pb={2}
                  fontSize="xs"
                  color="gray.400"
                  fontWeight="medium"
                  textTransform="none"
                >
                  {section.label}
                </Text>
                <VStack spacing={1} align="stretch">
                  {section.sessions.map((conv, idx) => (
                    <motion.div
                      key={conv.sessionId}
                      variants={sessionItemVariants}
                      initial="hidden"
                      animate="visible"
                      exit="exit"
                      custom={idx}
                      style={{ width: '100%' }}
                    >
                      {renderSessionButton(conv)}
                    </motion.div>
                  ))}
                </VStack>
              </Box>
            ))}
            {visibleSessions.length === 0 && (
              <Text px={2} py={3} fontSize="sm" color="gray.500">
                {t('sidebar.noSessionsFound')}
              </Text>
            )}
          </VStack>
        </Box>

      </VStack>
    </motion.div>
  );
}
