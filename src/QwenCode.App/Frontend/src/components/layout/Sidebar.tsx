import {
  Box,
  VStack,
  Text,
  Button,
  HStack,
} from '@chakra-ui/react';
import { motion } from 'framer-motion';
import { Plus, Search, Settings, ChevronRight, FolderOpen, Folder } from 'lucide-react';
import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { SessionPreview } from '@/types/desktop';

interface SidebarProps {
  isOpen: boolean;
  onClose: () => void;
  sessions: SessionPreview[];
  activeTurnSessions: Record<string, true>;
  onNewChat?: () => void;
  onSelectSession?: (sessionId: string) => void;
  onOpenSettings?: () => void;
  onOpenSearch?: () => void;
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

function getProjectName(workingDir: string, t: ReturnType<typeof useTranslation>['t']): string {
  if (!workingDir) return t('sidebar.otherProjects');
  const parts = workingDir.replace(/\\/g, '/').split('/').filter(Boolean);
  return parts[parts.length - 1] || t('sidebar.otherProjects');
}

interface ProjectGroup {
  name: string;
  sessions: SessionPreview[];
}

export default function Sidebar({
  isOpen,
  sessions,
  activeTurnSessions,
  onNewChat = () => console.log('New chat'),
  onSelectSession = (id: string) => console.log(`Selected conversation ${id}`),
  onOpenSettings = () => console.log('Settings clicked'),
  onOpenSearch = () => console.log('Search opened')
}: SidebarProps) {

  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const { t } = useTranslation();

  const groupedConversations = useMemo(() => {
    const groups: Record<string, SessionPreview[]> = {};
    for (const conv of sessions) {
      const project = getProjectName(conv.workingDirectory, t);
      if (!groups[project]) groups[project] = [];
      groups[project].push(conv);
    }

    const result: ProjectGroup[] = Object.entries(groups)
      .sort((a, b) => {
        // Sort by most recent session
        const aLatest = Math.max(...a[1].map(s => new Date(s.lastActivity).getTime()));
        const bLatest = Math.max(...b[1].map(s => new Date(s.lastActivity).getTime()));
        return bLatest - aLatest;
      })
      .map(([name, sess]) => ({ name, sessions: sess }));

    return result;
  }, [sessions]);

  const toggleGroup = (name: string) => {
    setOpenGroups(prev => ({
      ...prev,
      [name]: !(prev[name] !== false),
    }));
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
        {/* Combined New Chat + Search block */}
        <Box px={3} pt={3} pb={2}>
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
                <Text fontSize="sm" color="gray.400">Search</Text>
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
            '&::-webkit-scrollbar': { width: '6px' },
            '&::-webkit-scrollbar-track': { background: 'transparent' },
            '&::-webkit-scrollbar-thumb': {
              background: '#5b5b67',
              borderRadius: '3px',
            },
            '&::-webkit-scrollbar-thumb:hover': {
              background: '#72727f',
            },
          }}
        >
          <VStack spacing={1} align="stretch">
            {groupedConversations.map((group) => {
              const isGroupOpen = openGroups[group.name] !== false;
              return (
                <Box key={group.name}>
                  {/* Group header */}
                  <Button
                    variant="ghost"
                    w="full"
                    h="28px"
                    px={1}
                    justifyContent="flex-start"
                    color="gray.500"
                    fontSize="xs"
                    fontWeight="medium"
                    _hover={{ bg: 'gray.700', color: 'gray.300' }}
                    onClick={() => toggleGroup(group.name)}
                    leftIcon={
                      <ChevronRight
                        size={12}
                        style={{
                          transition: 'transform 0.15s',
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

                  {/* Group sessions */}
                  {isGroupOpen && (
                    <VStack spacing={0} align="stretch" pl={2}>
                      {group.sessions.map((conv) => {
                        const isActive = conv.sessionId in activeTurnSessions;
                        return (
                          <Button
                            key={conv.sessionId}
                            variant="ghost"
                            colorScheme="gray"
                            onClick={() => onSelectSession(conv.sessionId)}
                            h="32px"
                            px={2}
                            py={0}
                            justifyContent="space-between"
                            textAlign="left"
                            bg={isActive ? 'gray.700' : 'transparent'}
                            _hover={{ bg: 'gray.700' }}
                            borderRadius="md"
                            whiteSpace="nowrap"
                            fontSize="sm"
                          >
                            <Text
                              color="gray.200"
                              flex={1}
                              overflow="hidden"
                              textOverflow="ellipsis"
                              textAlign="left"
                            >
                              {conv.title}
                            </Text>
                            <Text
                              fontSize="xs"
                              color="gray.500"
                              ml={2}
                              flexShrink={0}
                            >
                              {formatRelativeTime(conv.lastActivity, t)}
                            </Text>
                          </Button>
                        );
                      })}
                    </VStack>
                  )}
                </Box>
              );
            })}
          </VStack>
        </Box>

        {/* Footer: Settings */}
        <Box px={3} py={2}>
          <Button
            leftIcon={<Settings size={15} />}
            variant="ghost"
            colorScheme="gray"
            size="sm"
            width="100%"
            justifyContent="flex-start"
            borderRadius="md"
            h="36px"
            onClick={onOpenSettings}
            color="gray.300"
            _hover={{ bg: 'gray.700' }}
          >
            {t('top.settings')}
          </Button>
        </Box>
      </VStack>
    </motion.div>
  );
}
