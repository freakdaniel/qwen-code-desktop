import {
  Box,
  VStack,
  IconButton,
  Input,
  Text,
  Button,
  HStack,
} from '@chakra-ui/react';
import { motion } from 'framer-motion';
import { Plus, Search, Settings, LayoutDashboard, ChevronRight, FolderOpen, Folder } from 'lucide-react';
import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { SessionPreview } from '@/types/desktop';

interface SidebarProps {
  isOpen: boolean;
  onClose: () => void;
  onToggle: () => void;
  sessions: SessionPreview[];
  activeTurnSessions: Record<string, true>;
  onNewChat?: () => void;
  onSelectSession?: (sessionId: string) => void;
  onOpenSettings?: () => void;
}

function formatRelativeTime(dateStr: string): string {
  const now = Date.now();
  const then = new Date(dateStr).getTime();
  const diffMs = now - then;
  if (diffMs < 0) return 'сейчас';
  if (diffMs < 60_000) return 'сейчас';
  if (diffMs < 3_600_000) return `${Math.floor(diffMs / 60_000)}м`;
  if (diffMs < 86_400_000) return `${Math.floor(diffMs / 3_600_000)}ч`;
  if (diffMs < 604_800_000) return `${Math.floor(diffMs / 86_400_000)}д`;
  return `${Math.floor(diffMs / 604_800_000)}н`;
}

function getProjectName(workingDir: string): string {
  if (!workingDir) return 'Другие';
  const parts = workingDir.replace(/\\/g, '/').split('/').filter(Boolean);
  return parts[parts.length - 1] || 'Другие';
}

interface ProjectGroup {
  name: string;
  sessions: SessionPreview[];
}

export default function Sidebar({
  isOpen,
  onToggle,
  sessions,
  activeTurnSessions,
  onNewChat = () => console.log('New chat'),
  onSelectSession = (id: string) => console.log(`Selected conversation ${id}`),
  onOpenSettings = () => console.log('Settings clicked')
}: SidebarProps) {

  const [searchTerm, setSearchTerm] = useState('');
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const { t } = useTranslation();

  const filteredConversations = useMemo(() => {
    const filtered = sessions.filter(conv =>
      conv.title.toLowerCase().includes(searchTerm.toLowerCase())
    );

    // Group by project
    const groups: Record<string, SessionPreview[]> = {};
    for (const conv of filtered) {
      const project = getProjectName(conv.workingDirectory);
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
  }, [sessions, searchTerm]);

  const toggleGroup = (name: string) => {
    setOpenGroups(prev => ({ ...prev, [name]: !prev[name] }));
  };

  return (
    <motion.div
      initial={{ x: '-100%' }}
      animate={{ x: isOpen ? 0 : '-100%' }}
      transition={{ type: 'spring', damping: 25, stiffness: 200 }}
      style={{
        position: 'absolute',
        left: 0,
        top: 0,
        height: '100vh',
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
        {/* Header: Logo + toggle */}
        <Box px={3} pt={3} pb={2}>
          <HStack justify="space-between" align="center">
            <HStack spacing={2}>
              <Box
                w="28px"
                h="28px"
                borderRadius="md"
                bg="brand.500"
                display="flex"
                alignItems="center"
                justifyContent="center"
              >
                <Text fontSize="xs" fontWeight="bold" color="white" lineHeight="1">Q</Text>
              </Box>
              <Text fontWeight="bold" fontSize="sm" color="white" letterSpacing="wide">QWEN</Text>
            </HStack>
            <IconButton
              aria-label="Toggle sidebar"
              icon={<LayoutDashboard size={15} />}
              size="xs"
              variant="ghost"
              colorScheme="gray"
              onClick={onToggle}
            />
          </HStack>
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
              colorScheme="brand"
              variant="solid"
              size="sm"
              width="100%"
              h="36px"
              borderRadius="0"
              onClick={onNewChat}
              _hover={{ bg: 'brand.600' }}
            >
              {t('sidebar.newChat')}
            </Button>

            <Box h="1px" bg="gray.600" />

            {/* Search — button-like with icon next to text like Plus button */}
            <Box position="relative">
              <Input
                placeholder="Search"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                bg="gray.700"
                borderColor="transparent"
                color="white"
                borderRadius="0"
                h="36px"
                fontSize="sm"
                pl="36px"
                _placeholder={{ color: 'gray.400' }}
                _focus={{ borderColor: 'brand.500', boxShadow: 'none' }}
                _hover={{ bg: 'gray.600' }}
              />
              <Box
                position="absolute"
                left="12px"
                top="50%"
                transform="translateY(-50%)"
                pointerEvents="none"
                color="gray.400"
              >
                <Search size={14} />
              </Box>
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
            {filteredConversations.map((group) => {
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
                              {formatRelativeTime(conv.lastActivity)}
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
