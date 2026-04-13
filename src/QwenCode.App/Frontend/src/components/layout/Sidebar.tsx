import {
  Box,
  VStack,
  Text,
  Button,
  HStack,
  Portal,
  Skeleton,
  IconButton,
} from '@chakra-ui/react';
import { AnimatePresence, motion } from 'framer-motion';
import { Plus, Search, Settings, ChevronRight, FolderOpen, Folder, Puzzle, MessageCircle, Code2, MoreHorizontal, Pencil, Trash2, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useState, useMemo, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { SessionPreview } from '@/types/desktop';
import qwenLogo from '@/assets/qwen-logo.svg';
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
  onRenameSession?: (session: SessionPreview) => void;
  onDeleteSession?: (session: SessionPreview) => void;
}

interface ChatSection {
  key: string;
  label: string;
  sessions: SessionPreview[];
}

const SIDEBAR_EXPANDED_WIDTH = 292;
const SIDEBAR_COLLAPSED_WIDTH = 54;
const APP_BACKGROUND = '#1f1f23';
const SIDEBAR_BACKGROUND = '#17171b';
const SIDEBAR_HOVER = { bg: 'transparent', color: 'white' };

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
  onClose,
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
  onRenameSession = (session) => console.log(`Rename conversation ${session.sessionId}`),
  onDeleteSession = (session) => console.log(`Delete conversation ${session.sessionId}`),
}: SidebarProps) {
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const [sessionMenu, setSessionMenu] = useState<{ session: SessionPreview; x: number; y: number } | null>(null);
  const [hoveredSessionId, setHoveredSessionId] = useState('');
  const menuRef = useRef<HTMLDivElement>(null);
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

  useEffect(() => {
    if (!sessionMenu) return;

    const closeOnPointerDown = (event: MouseEvent) => {
      const target = event.target as Node | null;
      if (target && menuRef.current?.contains(target)) {
        return;
      }

      if ((event.target as Element | null)?.closest?.('.session-actions')) {
        return;
      }

      setSessionMenu(null);
    };

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setSessionMenu(null);
      }
    };

    window.addEventListener('mousedown', closeOnPointerDown);
    window.addEventListener('keydown', closeOnEscape);
    return () => {
      window.removeEventListener('mousedown', closeOnPointerDown);
      window.removeEventListener('keydown', closeOnEscape);
    };
  }, [sessionMenu]);

  const toggleSessionMenu = (session: SessionPreview, x: number, y: number) => {
    const menuWidth = 184;
    const menuHeight = 84;
    setSessionMenu((current) => {
      if (current?.session.sessionId === session.sessionId) {
        return null;
      }

      return {
        session,
        x: Math.min(window.innerWidth - menuWidth - 8, Math.max(8, x)),
        y: Math.min(window.innerHeight - menuHeight - 8, Math.max(8, y)),
      };
    });
  };

  const renderSessionButton = (conv: SessionPreview) => {
    const isSelected = conv.sessionId === selectedSessionId;
    const isRunning = conv.sessionId in activeTurnSessions;
    const showSessionActions = hoveredSessionId === conv.sessionId;

    return (
      <Button
        key={conv.sessionId}
        variant="ghost"
        colorScheme="gray"
        onClick={() => onSelectSession(conv.sessionId)}
        onContextMenu={(event) => {
          event.preventDefault();
          toggleSessionMenu(conv, event.clientX, event.clientY);
        }}
        onMouseEnter={() => setHoveredSessionId(conv.sessionId)}
        onMouseLeave={() => setHoveredSessionId((current) => (current === conv.sessionId ? '' : current))}
        className="session-row"
        h="38px"
        px={3}
        py={0}
        alignItems="center"
        justifyContent="space-between"
        w="full"
        minW={0}
        position="relative"
        bg={isSelected ? '#3a3a42' : 'transparent'}
        color={isSelected ? 'white' : 'gray.200'}
        _hover={{ bg: isSelected ? '#3a3a42' : 'transparent', color: 'white' }}
        _active={{ bg: isSelected ? '#3a3a42' : 'transparent', color: 'white' }}
        borderRadius="full"
        fontSize="sm"
        fontWeight="normal"
        lineHeight="normal"
        overflow="visible"
        boxShadow={isSelected ? '0 0 0 1px rgba(255,255,255,0.04) inset' : 'none'}
      >
        <Box flex={1} minW={0} h="22px" pr={2} display="flex" alignItems="center">
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
              lineHeight="22px"
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
            flexShrink={0}
            transition="background-color 0.2s ease"
          />
        )}
        {showSessionActions && (
          <Box
            className="session-actions"
            position="absolute"
            right="6px"
            top="50%"
            transform="translateY(-50%)"
            w="24px"
            h="24px"
            display="flex"
            alignItems="center"
            justifyContent="center"
            borderRadius="full"
            color={isSelected ? 'gray.200' : 'gray.400'}
            bg={isSelected ? '#34343c' : 'rgba(255,255,255,0.04)'}
            transition="color 0.14s ease, background-color 0.14s ease"
            role="button"
            tabIndex={0}
            aria-label={t('sidebar.sessionActions')}
            onClick={(event) => {
              event.preventDefault();
              event.stopPropagation();
              toggleSessionMenu(conv, event.clientX, event.clientY);
            }}
            onKeyDown={(event) => {
              if (event.key !== 'Enter' && event.key !== ' ') return;
              event.preventDefault();
              event.stopPropagation();
              const rect = event.currentTarget.getBoundingClientRect();
              toggleSessionMenu(conv, rect.right, rect.bottom);
            }}
            _hover={{ color: 'white', bg: 'transparent' }}
          >
            <MoreHorizontal size={15} />
          </Box>
        )}
      </Button>
    );
  };

  const railActions = [
    {
      key: 'toggle-mode',
      label: mode === 'projects' ? t('top.chats') : t('top.coder'),
      icon: mode === 'projects' ? <MessageCircle size={17} /> : <Code2 size={17} />,
      onClick: onToggleMode,
    },
    {
      key: 'new-chat',
      label: t('sidebar.newChat'),
      icon: <Plus size={17} />,
      onClick: onNewChat,
    },
    {
      key: 'search',
      label: t('sidebar.search'),
      icon: <Search size={17} />,
      onClick: onOpenSearch,
    },
    {
      key: 'skills',
      label: t('top.skills'),
      icon: <Puzzle size={17} />,
      onClick: onOpenSkills,
    },
    {
      key: 'settings',
      label: t('top.settings'),
      icon: <Settings size={17} />,
      onClick: onOpenSettings,
    },
  ];

  return (
    <motion.div
      initial={false}
      animate={{
        width: isOpen ? SIDEBAR_EXPANDED_WIDTH : SIDEBAR_COLLAPSED_WIDTH,
        backgroundColor: isOpen ? SIDEBAR_BACKGROUND : APP_BACKGROUND,
      }}
      transition={{ duration: 0.26, ease: [0.22, 1, 0.36, 1] }}
      style={{
        height: '100%',
        overflow: 'hidden',
        flexShrink: 0,
        position: 'relative',
      }}
    >
      <VStack
        h="100%"
        spacing={0}
        align="stretch"
        bg="transparent"
        borderRight="1px solid"
        borderColor="rgba(255,255,255,0.06)"
      >
        <AnimatePresence initial={false} mode="wait">
        {isOpen ? (
          <motion.div
            key="expanded-sidebar"
            initial={{ opacity: 0, x: -8 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -8 }}
            transition={{ duration: 0.18, ease: 'easeOut' }}
            style={{ width: SIDEBAR_EXPANDED_WIDTH, height: '100%', overflow: 'hidden' }}
          >
          <VStack h="100%" spacing={0} align="stretch">
            <Box px={4} pt={3} pb={3}>
              <HStack justify="space-between" align="center">
                <img src={qwenLogo} alt="Qwen" style={{ width: '26px', height: '26px' }} draggable={false} />
                <IconButton
                  aria-label="Collapse sidebar"
                  icon={<PanelLeftClose size={16} />}
                  variant="ghost"
                  size="sm"
                  color="gray.400"
                  borderRadius="10px"
                  onClick={onClose}
                  _hover={SIDEBAR_HOVER}
                />
              </HStack>
            </Box>
            <Box px={3} pb={3}>
              <VStack spacing={1.5} align="stretch">
                <Button
                  leftIcon={<Plus size={15} />}
                  variant="ghost"
                  size="sm"
                  width="100%"
                  h="38px"
                  borderRadius="14px"
                  justifyContent="flex-start"
                  fontWeight="normal"
                  color="gray.400"
                  onClick={onNewChat}
                  _hover={SIDEBAR_HOVER}
                  _active={{ bg: 'transparent', color: 'white' }}
                >
                  {t('sidebar.newChat')}
                </Button>
                <Button
                  leftIcon={<Search size={15} />}
                  variant="ghost"
                  size="sm"
                  width="100%"
                  h="38px"
                  borderRadius="14px"
                  justifyContent="flex-start"
                  fontWeight="normal"
                  color="gray.400"
                  onClick={onOpenSearch}
                  _hover={SIDEBAR_HOVER}
                  _active={{ bg: 'transparent', color: 'white' }}
                >
                  {t('sidebar.search')}
                </Button>
                <Button
                  leftIcon={mode === 'projects' ? <MessageCircle size={15} /> : <Code2 size={15} />}
                  variant="ghost"
                  size="sm"
                  width="100%"
                  h="38px"
                  borderRadius="14px"
                  justifyContent="flex-start"
                  fontWeight="normal"
                  color="gray.400"
                  onClick={onToggleMode}
                  _hover={SIDEBAR_HOVER}
                  _active={{ bg: 'transparent', color: 'white' }}
                >
                  {mode === 'projects' ? t('top.chats') : t('top.coder')}
                </Button>
                <Button
                  leftIcon={<Puzzle size={15} />}
                  variant="ghost"
                  size="sm"
                  width="100%"
                  h="38px"
                  borderRadius="14px"
                  justifyContent="flex-start"
                  fontWeight="normal"
                  color="gray.400"
                  onClick={onOpenSkills}
                  _hover={SIDEBAR_HOVER}
                  _active={{ bg: 'transparent', color: 'white' }}
                >
                  {t('top.skills')}
                </Button>
                <Button
                  leftIcon={<Settings size={15} />}
                  variant="ghost"
                  size="sm"
                  width="100%"
                  h="38px"
                  borderRadius="14px"
                  justifyContent="flex-start"
                  fontWeight="normal"
                  color="gray.400"
                  onClick={onOpenSettings}
                  _hover={SIDEBAR_HOVER}
                  _active={{ bg: 'transparent', color: 'white' }}
                >
                  {t('top.settings')}
                </Button>
              </VStack>
            </Box>

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
              <VStack spacing={1.5} align="stretch">
                {mode === 'projects' ? groupedConversations.map((group) => {
                  const isGroupOpen = openGroups[group.name] !== false;
                  return (
                    <Box key={group.name}>
                      <Button
                        variant="ghost"
                        w="full"
                        h="28px"
                        px={2}
                        justifyContent="flex-start"
                        color="gray.500"
                        fontSize="xs"
                        fontWeight="normal"
                        borderRadius="12px"
                        _hover={{ bg: 'transparent', color: 'gray.200' }}
                        _active={{ bg: 'transparent', color: 'gray.100' }}
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
                      color="gray.500"
                      fontWeight="normal"
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
        ) : (
          <motion.div
            key="collapsed-sidebar"
            initial={{ opacity: 0, x: -4 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: -4 }}
            transition={{ duration: 0.16, ease: 'easeOut' }}
            style={{ width: SIDEBAR_COLLAPSED_WIDTH, height: '100%', overflow: 'hidden' }}
          >
          <VStack h="100%" spacing={2} align="center" px={1} pt={3} pb={3}>
            <IconButton
              aria-label="Expand sidebar"
              icon={<PanelLeftOpen size={16} />}
              variant="ghost"
              size="md"
              w="40px"
              h="40px"
              borderRadius="14px"
              color="gray.400"
              onClick={onClose}
              _hover={SIDEBAR_HOVER}
              _active={{ bg: 'transparent', color: 'white' }}
            />
            {railActions.map((action) => (
              <IconButton
                key={action.key}
                aria-label={action.label}
                icon={action.icon}
                variant="ghost"
                size="md"
                w="40px"
                h="40px"
                borderRadius="14px"
                color="gray.400"
                onClick={action.onClick}
                _hover={SIDEBAR_HOVER}
                _active={{ bg: 'transparent', color: 'white' }}
              />
            ))}
          </VStack>
          </motion.div>
        )}
        </AnimatePresence>

      </VStack>
      <Portal>
        <AnimatePresence>
          {sessionMenu && (
            <motion.div
              initial={{ opacity: 0, y: 4, scale: 0.98 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: 4, scale: 0.98 }}
              transition={{ duration: 0.12, ease: 'easeOut' }}
              style={{
                position: 'fixed',
                left: `${sessionMenu.x}px`,
                top: `${sessionMenu.y}px`,
                zIndex: 3000,
              }}
            >
              <Box
                ref={menuRef}
                w="184px"
                p={1}
                bg="gray.800"
                border="1px solid"
                borderColor="gray.700"
                borderRadius="lg"
                shadow="xl"
              >
                <Button
                  variant="ghost"
                  w="full"
                  h="34px"
                  px={2}
                  justifyContent="flex-start"
                  leftIcon={<Pencil size={14} />}
                  color="gray.200"
                  fontSize="sm"
                  fontWeight="normal"
                  borderRadius="md"
                  _hover={{ bg: 'gray.700', color: 'white' }}
                  onClick={() => {
                    const session = sessionMenu.session;
                    setSessionMenu(null);
                    onRenameSession(session);
                  }}
                >
                  {t('sidebar.renameChat')}
                </Button>
                <Button
                  variant="ghost"
                  w="full"
                  h="34px"
                  px={2}
                  justifyContent="flex-start"
                  leftIcon={<Trash2 size={14} />}
                  color="red.300"
                  fontSize="sm"
                  fontWeight="normal"
                  borderRadius="md"
                  _hover={{ bg: 'rgba(248,113,113,0.12)', color: 'red.200' }}
                  onClick={() => {
                    const session = sessionMenu.session;
                    setSessionMenu(null);
                    onDeleteSession(session);
                  }}
                >
                  {t('sidebar.deleteChat')}
                </Button>
              </Box>
            </motion.div>
          )}
        </AnimatePresence>
      </Portal>
    </motion.div>
  );
}
