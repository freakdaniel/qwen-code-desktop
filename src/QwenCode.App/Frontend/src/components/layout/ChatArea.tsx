import { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import {
  Box,
  VStack,
  HStack,
  Flex,
  IconButton,
  Button,
  Text,
  Textarea as ChakraTextarea,
  Spinner,
  Center,
} from '@chakra-ui/react';
import {
  ArrowUp,
  Paperclip,
  ShieldCheck,
  FileEdit,
  ScrollText,
  Zap,
  Check,
  Wrench,
  ChevronRight,
  Brain,
  Terminal,
  FileText,
  Bot,
  FolderOpen,
  FilePlus,
  Globe,
  Database,
  Layers,
  Search,
  MessageCircle,
  CheckSquare,
  Download,
} from 'lucide-react';
import { AnimatePresence, motion } from 'framer-motion';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { AGENT_MODES } from '@/types/ui';
import type { AgentMode } from '@/types/ui';
import { useBootstrap } from '@/hooks/useBootstrap';
import { useTranslation } from 'react-i18next';
import type { DesktopSessionDetail, DesktopSessionEntry } from '@/types/desktop';
import qwenLogo from '@/assets/qwen-logo.svg';

interface ChatAreaProps {
  onToggleSidebar?: () => void;
  isSidebarOpen: boolean;
  selectedSessionId?: string;
}

const ACCENT = '#615CED';
const ACCENT_HOVER = '#4e49d9';
const CHAT_MAX_WIDTH = '4xl';

const MODE_ICONS: Record<AgentMode, React.ReactNode> = {
  'default': <ShieldCheck size={14} />,
  'plan': <ScrollText size={14} />,
  'auto-edit': <FileEdit size={14} />,
  'yolo': <Zap size={14} />,
};

// Extract meaningful text from any entry type (NOT used for assistant body — use entry.body directly)
function getEntryText(entry: DesktopSessionEntry): string {
  return entry.body || entry.arguments || '';
}

// Detect if an entry is a legacy thinking/reasoning entry (type-based)
function isThinkingEntry(entry: DesktopSessionEntry): boolean {
  const type = entry.type?.toLowerCase() ?? '';
  const title = entry.title?.toLowerCase() ?? '';
  return type === 'thought' || type === 'thinking' ||
    title === 'thinking' || title === 'thought';
}

// Tool display info: i18n key + icon component
type ToolIconType = React.ComponentType<{ size?: number; color?: string }>;

interface ToolDisplayInfo {
  labelKey: string;
  Icon: ToolIconType;
}

function getToolInfo(toolName: string): ToolDisplayInfo {
  const name = (toolName || '').toLowerCase().trim();
  if (name === 'agent' || name.endsWith('_agent')) return { labelKey: 'tools.agent', Icon: Bot };
  if (name.includes('list_directory') || name.includes('listdir') || name.includes('list_dir')) return { labelKey: 'tools.listDir', Icon: FolderOpen };
  if (name.includes('write_file') || name === 'write' || name.includes('create_file')) return { labelKey: 'tools.writeFile', Icon: FilePlus };
  if (name.includes('edit_file') || name.includes('str_replace') || name === 'edit' || name.includes('patch')) return { labelKey: 'tools.editFile', Icon: FileEdit };
  if (name.includes('read_file') || name === 'read') return { labelKey: 'tools.readFile', Icon: FileText };
  if (name.includes('bash') || name.includes('execute') || name.includes('run_command') || name.includes('shell') || name.includes('terminal')) return { labelKey: 'tools.shell', Icon: Terminal };
  if (name.includes('grep') || name.includes('search_files') || name === 'search') return { labelKey: 'tools.search', Icon: Search };
  if (name.includes('glob') || name.includes('find_files') || name === 'find') return { labelKey: 'tools.findFiles', Icon: Layers };
  if (name.includes('think')) return { labelKey: 'tools.think', Icon: Brain };
  if (name.includes('memory') || name.includes('save_mem')) return { labelKey: 'tools.memory', Icon: Database };
  if (name.includes('web_search') || name.includes('websearch') || name.includes('browse')) return { labelKey: 'tools.webSearch', Icon: Globe };
  if (name.includes('web_fetch') || name.includes('fetch')) return { labelKey: 'tools.webFetch', Icon: Download };
  if (name.includes('todo')) return { labelKey: 'tools.todo', Icon: CheckSquare };
  if (name.includes('ask_user') || name.includes('ask_question')) return { labelKey: 'tools.askUser', Icon: MessageCircle };
  if (name.includes('exit_plan') || name.includes('plan_mode')) return { labelKey: 'tools.planMode', Icon: ScrollText };
  return { labelKey: 'tools.tool', Icon: Wrench };
}

// Truncate a string for display
function trunc(s: string, n = 72): string {
  return s.length > n ? s.slice(0, n) + '…' : s;
}

// Extract just the filename from a full path
function basename(p: string): string {
  return p.split(/[/\\]/).filter(Boolean).pop() ?? p;
}

// Tool-specific argument summary for display
function getToolArgSummary(entry: DesktopSessionEntry): string {
  if (!entry.arguments) return '';
  const toolKey = (entry.toolName || entry.title || '').toLowerCase();
  try {
    const a = JSON.parse(entry.arguments) as Record<string, unknown>;
    const str = (k: string) => typeof a[k] === 'string' ? (a[k] as string) : '';

    // File path tools — show filename
    if (toolKey.includes('read') || toolKey.includes('write') || toolKey.includes('edit') || toolKey.includes('str_replace')) {
      const p = str('path') || str('file_path') || str('filename');
      return p ? basename(p) : '';
    }
    // glob / find — pattern is the most informative arg
    if (toolKey.includes('glob') || toolKey.includes('find')) {
      const pat = str('pattern');
      if (pat) return trunc(pat);
      const p = str('path');
      return p ? trunc(p.split(/[/\\]/).slice(-2).join('/')) : '';
    }
    // grep / search — show pattern or query
    if (toolKey.includes('grep') || toolKey.includes('search')) {
      const pat = str('pattern') || str('query') || str('search');
      return pat ? trunc(pat) : '';
    }
    // list_directory — show path (last 2 segments)
    if (toolKey.includes('list') || toolKey.includes('directory')) {
      const p = str('path');
      return p ? trunc(p.split(/[/\\]/).slice(-2).join('/')) : '';
    }
    // agent — show description
    if (toolKey === 'agent' || toolKey.includes('agent')) {
      const d = str('description') || str('prompt');
      return d ? trunc(d) : '';
    }
    // shell — handled separately with full mono display
    if (toolKey.includes('bash') || toolKey.includes('execute') || toolKey.includes('shell') || toolKey.includes('run')) {
      return '';
    }
    // generic fallback: first meaningful string value
    const v = str('description') || str('command') || str('pattern') || str('file_path') || str('path') || str('query') || str('prompt');
    return v ? trunc(v) : '';
  } catch {
    return trunc(entry.arguments);
  }
}

export default function ChatArea({ selectedSessionId }: ChatAreaProps) {
  const { t } = useTranslation();
  const { bootstrap } = useBootstrap();
  const sessions = bootstrap?.recentSessions ?? [];
  const selectedSession = sessions.find(s => s.sessionId === selectedSessionId);

  const [mode, setMode] = useState<AgentMode>('default');
  const [prompt, setPrompt] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [usedTokens, setUsedTokens] = useState(0);
  const [modeDropdownOpen, setModeDropdownOpen] = useState(false);
  const [showContextTooltip, setShowContextTooltip] = useState(false);

  // Session data from IPC
  const [sessionDetail, setSessionDetail] = useState<DesktopSessionDetail | null>(null);
  const [isLoadingSession, setIsLoadingSession] = useState(false);

  const donutRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const modeBtnRef = useRef<HTMLButtonElement>(null);
  const modeMenuRef = useRef<HTMLDivElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const totalTokens = 128_000;
  const currentModeOption = AGENT_MODES.find((m) => m.value === mode) ?? AGENT_MODES[0];

  const formatTimestamp = useCallback((ts: string): string => {
    if (!ts) return '';
    try {
      return new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch {
      return '';
    }
  }, []);

  // Auto-scroll
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [sessionDetail?.entries]);

  // Load session
  useEffect(() => {
    if (!selectedSessionId || !window.qwenDesktop) {
      setSessionDetail(null);
      return;
    }

    let cancelled = false;
    setIsLoadingSession(true);

    const loadSession = async () => {
      try {
        const detail = await window.qwenDesktop!.getSession({
          sessionId: selectedSessionId,
          offset: null,
          limit: 200,
        });
        if (!cancelled && detail) {
          setSessionDetail(detail);
          const textLength = detail.entries.reduce((sum, e) => sum + (e.body?.length || 0), 0);
          setUsedTokens(Math.ceil(textLength / 4));
        }
      } catch (err) {
        console.error('Failed to load session:', err);
      } finally {
        if (!cancelled) setIsLoadingSession(false);
      }
    };

    void loadSession();
    return () => { cancelled = true; };
  }, [selectedSessionId]);

  const handleSubmit = useCallback(() => {
    if (!prompt.trim() || isSubmitting || !selectedSessionId) return;
    setIsSubmitting(true);
    console.log('Submit prompt:', prompt, 'to session:', selectedSessionId);
    setPrompt('');
    setTimeout(() => setIsSubmitting(false), 1000);
  }, [prompt, isSubmitting, selectedSessionId]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && e.ctrlKey) {
      e.preventDefault();
      handleSubmit();
    }
  };

  // Donut ring
  const contextPercent = totalTokens > 0 ? Math.min(100, Math.round((usedTokens / totalTokens) * 100)) : 0;
  const circumference = 2 * Math.PI * 10;
  const dashOffset = circumference - (contextPercent / 100) * circumference;

  // Group entries into display blocks
  const groupedEntries = useMemo(() => {
    if (!sessionDetail?.entries) return [];

    type Block =
      | { type: 'user'; entries: DesktopSessionEntry[] }
      | { type: 'assistant'; entries: DesktopSessionEntry[] }
      | { type: 'tool-group'; entries: DesktopSessionEntry[] }
      | { type: 'thought'; entries: DesktopSessionEntry[] };

    const blocks: Block[] = [];
    let currentBlock: Block | null = null;

    for (const entry of sessionDetail.entries) {
      if (entry.type === 'system' || entry.type === 'tool_result') continue;

      const isUser = entry.type === 'user';
      const isTool = entry.type === 'tool' || !!entry.toolName;
      const isThought = isThinkingEntry(entry);

      const blockType: Block['type'] =
        isThought ? 'thought' : isTool ? 'tool-group' : isUser ? 'user' : 'assistant';

      if (!currentBlock || currentBlock.type !== blockType) {
        if (currentBlock) blocks.push(currentBlock);
        currentBlock = { type: blockType, entries: [entry] };
      } else {
        currentBlock.entries.push(entry);
      }
    }

    if (currentBlock) blocks.push(currentBlock);
    return blocks;
  }, [sessionDetail?.entries]);

  // Indices of assistant blocks that are "final" in each AI turn
  // (last assistant block before the next user block, or at end of conversation)
  // Only these blocks show a timestamp on their last entry.
  const finalAssistantBlockIndices = useMemo(() => {
    const set = new Set<number>();
    let lastAiIdx = -1;
    for (let i = 0; i < groupedEntries.length; i++) {
      if (groupedEntries[i].type === 'user') {
        if (lastAiIdx >= 0) set.add(lastAiIdx);
        lastAiIdx = -1;
      } else if (groupedEntries[i].type === 'assistant') {
        lastAiIdx = i;
      }
    }
    if (lastAiIdx >= 0) set.add(lastAiIdx);
    return set;
  }, [groupedEntries]);

  // Collapsible state for tool groups and thoughts
  // undefined = collapsed (default), false = expanded, true = explicitly collapsed
  const [collapsedBlocks, setCollapsedBlocks] = useState<Record<string, boolean>>({});

  const toggleBlock = useCallback((key: string) => {
    setCollapsedBlocks(prev => {
      const isCurrentlyCollapsed = prev[key] !== false; // undefined or true → collapsed
      return { ...prev, [key]: !isCurrentlyCollapsed }; // toggle: true→false, false→true
    });
  }, []);

  const hasSession = !!selectedSessionId;

  return (
    // FIX 1: h="100%" instead of h="100vh" — fills parent container exactly
    <VStack h="100%" spacing={0} bg="gray.900" align="stretch" overflow="hidden">

      {/* FIX 2: Header only shown when session is selected */}
      {selectedSessionId && (
        <Box px={6} py={3} borderBottom="1px solid" borderColor="gray.700" minH="48px" flexShrink={0}>
          <Text fontWeight="medium" color="white" fontSize="sm">
            {selectedSession?.title ?? t('chat.newChat')}
          </Text>
        </Box>
      )}

      {/* Main area — messages or welcome */}
      <Box flex={1} overflowY="auto" sx={{
        '&::-webkit-scrollbar': { width: '6px' },
        '&::-webkit-scrollbar-track': { background: 'transparent' },
        '&::-webkit-scrollbar-thumb': { background: '#5b5b67', borderRadius: '3px' },
        '&::-webkit-scrollbar-thumb:hover': { background: '#72727f' },
      }}>
        {hasSession ? (
          isLoadingSession ? (
            <Center h="full">
              <VStack spacing={3}>
                <Spinner size="md" color="brand.500" />
                <Text fontSize="sm" color="gray.500">Loading…</Text>
              </VStack>
            </Center>
          ) : sessionDetail && sessionDetail.entries.length > 0 ? (
            /* FIX 4: px={4} outer padding to match input container width */
            <Box px={4}>
              <Box mx="auto" maxW={CHAT_MAX_WIDTH}>
                <VStack spacing={0} align="stretch" py={4}>
                  {groupedEntries.map((block, blockIdx) => {
                    const blockKey = `block-${blockIdx}-${block.entries[0]?.id ?? blockIdx}`;

                    // ── User messages ──
                    if (block.type === 'user') {
                      return block.entries.map((entry) => {
                        // FIX 2: always use body as primary text for user messages
                        const text = entry.body || entry.title || '';
                        if (!text) return null;
                        return (
                          <Flex key={entry.id} justify="flex-end" py={2}>
                            <Box
                              maxW="80%"
                              px={4}
                              py={2.5}
                              borderRadius="20px"
                              borderTopRightRadius="4px"
                              bg={ACCENT}
                            >
                              <Text color="white" fontSize="sm" whiteSpace="pre-wrap" wordBreak="break-word" lineHeight="relaxed">
                                {text}
                              </Text>
                              {entry.timestamp && (
                                <Text fontSize="10px" color="whiteAlpha.600" mt={1} textAlign="right">
                                  {formatTimestamp(entry.timestamp)}
                                </Text>
                              )}
                            </Box>
                          </Flex>
                        );
                      });
                    }

                    // ── Tool groups — timeline design ──
                    if (block.type === 'tool-group') {
                      const count = block.entries.length;

                      // ── Single tool: show inline, no expand needed ──
                      if (count === 1) {
                        const entry = block.entries[0];
                        const info = getToolInfo(entry.toolName || entry.title || '');
                        const ToolIcon = info.Icon;
                        const label = t(info.labelKey);
                        const isShell = info.labelKey === 'tools.shell';
                        const summary = getToolArgSummary(entry);
                        const isCollapsed = collapsedBlocks[blockKey] !== false;
                        const hasShellDetail = isShell && !!entry.arguments;
                        const files = entry.changedFiles ?? [];

                        return (
                          <Box key={blockKey} py={0.5}>
                            <HStack
                              spacing={2}
                              px={2}
                              h="26px"
                              color="gray.500"
                              cursor={hasShellDetail ? 'pointer' : 'default'}
                              onClick={hasShellDetail ? () => toggleBlock(blockKey) : undefined}
                              _hover={hasShellDetail ? { color: 'gray.300' } : undefined}
                              role={hasShellDetail ? 'button' : undefined}
                            >
                              {hasShellDetail && (
                                <motion.span animate={{ rotate: isCollapsed ? 0 : 90 }} transition={{ duration: 0.18 }} style={{ display: 'flex' }}>
                                  <ChevronRight size={11} />
                                </motion.span>
                              )}
                              <Box color="gray.500" flexShrink={0}><ToolIcon size={12} /></Box>
                              <Text fontSize="xs" color="gray.400" fontWeight="medium" flexShrink={0}>{label}</Text>
                              {summary && (
                                <Text fontSize="xs" color="gray.600" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap" flex={1} minW={0}>
                                  · {summary}
                                </Text>
                              )}
                            </HStack>
                            {/* Shell detail expand */}
                            <AnimatePresence initial={false}>
                              {hasShellDetail && !isCollapsed && (
                                <motion.div key="sh" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.18, ease: 'easeOut' }} style={{ overflow: 'hidden' }}>
                                  <Box ml={7} mt={0.5} mb={1} px={2} py={1.5} bg="gray.900" borderRadius="lg" fontFamily="mono">
                                    <Text fontSize="xs" color="gray.300" whiteSpace="pre-wrap" wordBreak="break-all">
                                      {entry.arguments.length > 200 ? entry.arguments.slice(0, 200) + '…' : entry.arguments}
                                    </Text>
                                    {entry.exitCode != null && (
                                      <Text fontSize="10px" color={entry.exitCode === 0 ? 'green.500' : 'red.500'} mt={0.5}>exit {entry.exitCode}</Text>
                                    )}
                                  </Box>
                                </motion.div>
                              )}
                            </AnimatePresence>
                            {/* Changed files */}
                            {files.length > 0 && (
                              <Box ml={7}>
                                {files.map((f) => (
                                  <HStack key={f} spacing={1} h="18px">
                                    <FileText size={9} color="#6b7280" />
                                    <Text fontSize="10px" color="gray.600" fontFamily="mono" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap">
                                      {f.split(/[/\\]/).slice(-2).join('/')}
                                    </Text>
                                  </HStack>
                                ))}
                              </Box>
                            )}
                          </Box>
                        );
                      }

                      // ── Multiple tools: collapsible with timeline ──
                      const isCollapsed = collapsedBlocks[blockKey] !== false;

                      return (
                        <Box key={blockKey} py={0.5}>
                          {/* Header: chevron + tool icons + count */}
                          <HStack
                            spacing={1.5}
                            px={2}
                            h="26px"
                            color="gray.500"
                            cursor="pointer"
                            onClick={() => toggleBlock(blockKey)}
                            _hover={{ color: 'gray.300' }}
                            role="button"
                          >
                            <motion.span animate={{ rotate: isCollapsed ? 0 : 90 }} transition={{ duration: 0.18 }} style={{ display: 'flex' }}>
                              <ChevronRight size={11} />
                            </motion.span>
                            {/* Show up to 4 tool icons in collapsed state */}
                            {isCollapsed && (
                              <HStack spacing={1.5}>
                                {block.entries.slice(0, 4).map((e) => {
                                  const I = getToolInfo(e.toolName || e.title || '').Icon;
                                  return <Box key={e.id} color="gray.600"><I size={11} /></Box>;
                                })}
                                {count > 4 && <Text fontSize="10px" color="gray.600">+{count - 4}</Text>}
                              </HStack>
                            )}
                            <Text fontSize="xs" color="gray.500">{t('tools.toolCalls', { count })}</Text>
                          </HStack>

                          {/* Expanded: timeline list */}
                          <AnimatePresence initial={false}>
                            {!isCollapsed && (
                              <motion.div key="tg" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2, ease: 'easeOut' }} style={{ overflow: 'hidden' }}>
                                <Box ml={3} mt={1} mb={1}>
                                  {block.entries.map((entry, entryInnerIdx) => {
                                    const isLastEntry = entryInnerIdx === block.entries.length - 1;
                                    const info = getToolInfo(entry.toolName || entry.title || '');
                                    const ToolIcon = info.Icon;
                                    const label = t(info.labelKey);
                                    const isShell = info.labelKey === 'tools.shell';
                                    const summary = getToolArgSummary(entry);
                                    const files = entry.changedFiles ?? [];

                                    return (
                                      <Box key={entry.id} position="relative" pl="20px" py="1px">
                                        {/* Vertical line: full height for non-last, half for last (stops at mid-row) */}
                                        <Box
                                          position="absolute"
                                          left="1px"
                                          top="0"
                                          bottom={isLastEntry ? '50%' : '0'}
                                          width="1.5px"
                                          bg="gray.700"
                                        />
                                        {/* Horizontal arm connecting vertical line to content */}
                                        <Box
                                          position="absolute"
                                          left="1px"
                                          top="50%"
                                          width="13px"
                                          height="1.5px"
                                          bg="gray.700"
                                          style={{ transform: 'translateY(-50%)' }}
                                        />
                                        <HStack spacing={2} minH="22px">
                                          <Box color="gray.500" flexShrink={0}><ToolIcon size={12} /></Box>
                                          <Text fontSize="xs" color="gray.300" fontWeight="medium" flexShrink={0}>{label}</Text>
                                          {summary && (
                                            <Text fontSize="xs" color="gray.600" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap" flex={1} minW={0}>
                                              · {summary}
                                            </Text>
                                          )}
                                        </HStack>
                                        {/* Shell command inline */}
                                        {isShell && entry.arguments && (
                                          <Box ml={5} mt={0.5} px={2} py={1} bg="gray.900" borderRadius="md" fontFamily="mono">
                                            <Text fontSize="xs" color="gray.400" whiteSpace="pre-wrap" wordBreak="break-all">
                                              {entry.arguments.length > 160 ? entry.arguments.slice(0, 160) + '…' : entry.arguments}
                                            </Text>
                                            {entry.exitCode != null && (
                                              <Text fontSize="10px" color={entry.exitCode === 0 ? 'green.500' : 'red.500'} mt={0.5}>exit {entry.exitCode}</Text>
                                            )}
                                          </Box>
                                        )}
                                        {/* Changed files */}
                                        {files.length > 0 && (
                                          <Box ml={5}>
                                            {files.map((f) => (
                                              <HStack key={f} spacing={1} h="18px">
                                                <FileText size={9} color="#6b7280" />
                                                <Text fontSize="10px" color="gray.600" fontFamily="mono" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap">
                                                  {f.split(/[/\\]/).slice(-2).join('/')}
                                                </Text>
                                              </HStack>
                                            ))}
                                          </Box>
                                        )}
                                      </Box>
                                    );
                                  })}
                                </Box>
                              </motion.div>
                            )}
                          </AnimatePresence>
                        </Box>
                      );
                    }

                    // ── Legacy thought blocks (type-level, for backward compat) ──
                    if (block.type === 'thought') {
                      const isCollapsed = collapsedBlocks[blockKey] !== false;
                      return (
                        <Box key={blockKey} py={0.5}>
                          <Button
                            variant="ghost" size="sm" w="auto" justifyContent="flex-start"
                            h="24px" px={2} color="gray.600"
                            _hover={{ bg: 'gray.800', color: 'gray.400' }}
                            onClick={() => toggleBlock(blockKey)}
                            leftIcon={
                              <motion.span animate={{ rotate: isCollapsed ? 0 : 90 }} transition={{ duration: 0.18 }} style={{ display: 'flex' }}>
                                <ChevronRight size={12} />
                              </motion.span>
                            }
                          >
                            <HStack spacing={1.5}>
                              <Brain size={11} />
                              <Text fontSize="xs" fontStyle="italic" color="gray.500">{t('tools.thinking')}</Text>
                            </HStack>
                          </Button>
                          <AnimatePresence initial={false}>
                            {!isCollapsed && (
                              <motion.div
                                key="thought-content"
                                initial={{ height: 0, opacity: 0 }}
                                animate={{ height: 'auto', opacity: 1 }}
                                exit={{ height: 0, opacity: 0 }}
                                transition={{ duration: 0.2, ease: 'easeOut' }}
                                style={{ overflow: 'hidden' }}
                              >
                                <Box pl={4} ml={2} borderLeft="2px solid" borderColor="gray.700" mt={1}>
                                  {block.entries.map((entry) => (
                                    <Text key={entry.id} fontSize="xs" color="gray.500" fontStyle="italic" whiteSpace="pre-wrap" wordBreak="break-word" lineHeight="1.6">
                                      {getEntryText(entry)}
                                    </Text>
                                  ))}
                                </Box>
                              </motion.div>
                            )}
                          </AnimatePresence>
                        </Box>
                      );
                    }

                    // ── Assistant messages ──
                    return block.entries.map((entry, entryIdx) => {
                      // Use body directly — never fall through to title ("Assistant")
                      const text = entry.body ?? '';
                      const thinking = entry.thinkingBody ?? '';
                      // Skip assistant entries that have no content (e.g. pure orchestrator turns)
                      if (!text && !thinking) return null;
                      // Show timestamp only on the last entry of the final assistant block in each AI turn
                      const isLastEntry = entryIdx === block.entries.length - 1;
                      const showTime = isLastEntry && finalAssistantBlockIndices.has(blockIdx) && !!entry.timestamp;

                      const thinkKey = `think-${entry.id}`;
                      const isThinkCollapsed = collapsedBlocks[thinkKey] !== false;

                      return (
                        <Box key={entry.id} py={2}>
                          {/* Per-entry collapsible thinking */}
                          {thinking && (
                            <Box mb={text ? 2 : 0}>
                              <Button
                                variant="ghost" size="sm" w="auto" justifyContent="flex-start"
                                h="24px" px={2} color="gray.600"
                                _hover={{ bg: 'gray.800', color: 'gray.400' }}
                                onClick={() => toggleBlock(thinkKey)}
                                leftIcon={
                                  <motion.span animate={{ rotate: isThinkCollapsed ? 0 : 90 }} transition={{ duration: 0.18 }} style={{ display: 'flex' }}>
                                    <ChevronRight size={12} />
                                  </motion.span>
                                }
                              >
                                <HStack spacing={1.5}>
                                  <Brain size={11} />
                                  <Text fontSize="xs" fontStyle="italic" color="gray.500">{t('tools.thinking')}</Text>
                                </HStack>
                              </Button>
                              <AnimatePresence initial={false}>
                                {!isThinkCollapsed && (
                                  <motion.div
                                    key="think-body"
                                    initial={{ height: 0, opacity: 0 }}
                                    animate={{ height: 'auto', opacity: 1 }}
                                    exit={{ height: 0, opacity: 0 }}
                                    transition={{ duration: 0.2, ease: 'easeOut' }}
                                    style={{ overflow: 'hidden' }}
                                  >
                                    <Box pl={4} ml={2} borderLeft="2px solid" borderColor="gray.700" mt={1} mb={text ? 1 : 0}>
                                      <Text fontSize="xs" color="gray.500" fontStyle="italic" whiteSpace="pre-wrap" wordBreak="break-word" lineHeight="1.6">
                                        {thinking}
                                      </Text>
                                    </Box>
                                  </motion.div>
                                )}
                              </AnimatePresence>
                            </Box>
                          )}

                          {/* Response body with full markdown */}
                          {text && (
                            <Box
                              color="gray.100"
                              fontSize="sm"
                              lineHeight="1.75"
                              sx={{
                                'p': { mb: '0.75em' },
                                'p:last-child': { mb: 0 },
                                'h1,h2,h3,h4,h5,h6': { fontWeight: 'semibold', mt: '1.2em', mb: '0.4em', color: 'white', lineHeight: 1.3 },
                                'h1': { fontSize: 'xl' },
                                'h2': { fontSize: 'lg' },
                                'h3': { fontSize: 'md' },
                                'ul,ol': { pl: 5, mb: '0.75em' },
                                'li': { mb: '0.2em' },
                                'strong': { color: 'white', fontWeight: 'semibold' },
                                'em': { fontStyle: 'italic' },
                                'a': { color: 'blue.400', textDecoration: 'underline' },
                                'blockquote': { borderLeft: '3px solid', borderColor: 'gray.600', pl: 3, color: 'gray.400', fontStyle: 'italic', my: '0.75em' },
                                // Issue 2: inline code bg matches input field bg (gray.800)
                                'code': { fontFamily: 'mono', fontSize: '0.85em', bg: 'gray.800', px: '0.3em', py: '0.15em', borderRadius: '4px', color: 'gray.200' },
                                'pre': {
                                  bg: 'gray.900',
                                  border: '1px solid',
                                  borderColor: 'gray.700',
                                  borderRadius: 'md',
                                  p: 3,
                                  overflowX: 'auto',
                                  my: '0.75em',
                                  '& code': { bg: 'transparent', px: 0, py: 0, color: 'gray.200', fontSize: 'xs' },
                                },
                                'table': { width: '100%', borderCollapse: 'collapse', my: '0.75em' },
                                'th,td': { border: '1px solid', borderColor: 'gray.700', px: 2, py: 1 },
                                'th': { bg: 'gray.800', fontWeight: 'semibold' },
                                'hr': { borderColor: 'gray.700', my: '1em' },
                              }}
                            >
                              <ReactMarkdown remarkPlugins={[remarkGfm]}>{text}</ReactMarkdown>
                            </Box>
                          )}

                          {showTime && (
                            <Text fontSize="10px" color="gray.700" mt={1}>{formatTimestamp(entry.timestamp)}</Text>
                          )}
                        </Box>
                      );
                    });
                  })}
                  <div ref={messagesEndRef} />
                </VStack>
              </Box>
            </Box>
          ) : (
            <Center h="full">
              <Text fontSize="sm" color="gray.600">No messages in this session</Text>
            </Center>
          )
        ) : (
          /* FIX 3: Welcome screen with Qwen logo */
          <Flex h="100%" direction="column" align="center" justify="center" userSelect="none">
            <img
              src={qwenLogo}
              alt="Qwen"
              style={{ height: '64px', width: '64px', opacity: 0.9, marginBottom: '16px' }}
              draggable={false}
            />
            <Text fontSize="2xl" fontWeight="semibold" color="white" letterSpacing="tight">
              {t('chat.welcomeTitle')}
            </Text>
            <Text mt={2} fontSize="sm" color="gray.500" textAlign="center" maxW="sm">
              Select a session from the sidebar or start a new conversation
            </Text>
            {sessions.length > 0 && (
              <Text mt={3} fontSize="xs" color="gray.600">
                {sessions.length} {t('chat.sessionsAvailable')}
              </Text>
            )}
          </Flex>
        )}
      </Box>

      {/* Input Area — always visible */}
      <Box px={4} pb={4} pt={3} position="relative" bg="gray.900" flexShrink={0}>
        {/* Fade gradient mask above input */}
        <Box
          position="absolute"
          top="-48px"
          left={0}
          right={0}
          h="48px"
          pointerEvents="none"
          zIndex={5}
          sx={{
            background: 'linear-gradient(to bottom, transparent, #18181b)',
          }}
        />

        <Box
          mx="auto"
          w="full"
          maxW={CHAT_MAX_WIDTH}
          borderRadius="28px"
          overflow="visible"
          border="1px solid"
          borderColor="gray.700"
          bg="gray.800"
          boxShadow="0 24px 80px -48px rgba(0,0,0,0.95)"
        >
          {/* Textarea */}
          <Box px={5} pt={4}>
            <ChakraTextarea
              ref={textareaRef}
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={t('chat.promptPlaceholder')}
              rows={1}
              minH="96px"
              resize="none"
              overflow="hidden"
              border="none"
              bg="transparent"
              p={0}
              fontSize="sm"
              lineHeight="relaxed"
              color="white"
              _placeholder={{ color: 'gray.500' }}
              _focusVisible={{ boxShadow: 'none' }}
              sx={{ '&::-webkit-scrollbar': { display: 'none' } }}
            />
          </Box>

          {/* Bottom bar */}
          <HStack justify="space-between" px={4} py={3} gap={3}>
            {/* Left: attach + mode */}
            <HStack gap={2}>
              <IconButton
                aria-label="Attach file"
                icon={<Paperclip size={14} />}
                variant="ghost"
                size="sm"
                color="gray.500"
                _hover={{ color: 'white' }}
              />

              {/* Mode selector */}
              <Box position="relative">
                <Button
                  ref={modeBtnRef}
                  variant="ghost"
                  size="sm"
                  h="32px"
                  px={2}
                  color="gray.500"
                  _hover={{ color: 'white' }}
                  onClick={() => setModeDropdownOpen(!modeDropdownOpen)}
                  gap={1.5}
                >
                  {MODE_ICONS[mode]}
                  <Text fontSize="xs">{t(currentModeOption.labelKey)}</Text>
                </Button>

                {/* Mode Dropdown */}
                <AnimatePresence>
                  {modeDropdownOpen && (
                    <Box
                      position="absolute"
                      bottom="calc(100% + 8px)"
                      left={0}
                      zIndex={9999}
                    >
                      <motion.div
                        initial={{ opacity: 0, y: 8, scale: 0.96 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: 8, scale: 0.96 }}
                        transition={{ duration: 0.15, ease: 'easeOut' }}
                      >
                        <Box
                          ref={modeMenuRef}
                          minW="300px"
                          border="1px solid"
                          borderColor="gray.700"
                          bg="gray.800"
                          borderRadius="2xl"
                          shadow="lg"
                          p={1.5}
                        >
                          {AGENT_MODES.map((m) => {
                            const isSelected = mode === m.value;
                            return (
                              <Button
                                key={m.value}
                                variant="ghost"
                                w="full"
                                justifyContent="flex-start"
                                alignItems="center"
                                h="52px"
                                px={3}
                                onClick={() => { setMode(m.value); setModeDropdownOpen(false); }}
                                bg="transparent"
                                _hover={{ bg: 'gray.900', borderRadius: 'xl' }}
                                color="white"
                                gap={3}
                              >
                                <Box w={5} h={5} display="flex" alignItems="center" justifyContent="center" flexShrink={0}>
                                  {isSelected ? (
                                    <Check size={16} color={ACCENT} strokeWidth={2.5} />
                                  ) : (
                                    <Box color="gray.500">
                                      {MODE_ICONS[m.value]}
                                    </Box>
                                  )}
                                </Box>
                                <VStack align="start" spacing={0.5} flex={1}>
                                  <Text fontSize="sm" fontWeight="medium" textAlign="left" whiteSpace="nowrap">{t(m.labelKey)}</Text>
                                  <Text fontSize="xs" color="gray.500" whiteSpace="normal" textAlign="left">{t(m.descriptionKey)}</Text>
                                </VStack>
                              </Button>
                            );
                          })}
                        </Box>
                      </motion.div>
                    </Box>
                  )}
                </AnimatePresence>
              </Box>
            </HStack>

            {/* Right: donut + send */}
            <HStack gap={2}>
              {/* Context ring */}
              <Box
                ref={donutRef}
                position="relative"
                display="flex"
                alignItems="center"
                justifyContent="center"
                onMouseEnter={() => setShowContextTooltip(true)}
                onMouseLeave={() => setShowContextTooltip(false)}
              >
                <AnimatePresence>
                  {showContextTooltip && (
                    <motion.div
                      initial={{ opacity: 0, y: 4 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: 4 }}
                      transition={{ duration: 0.12 }}
                      style={{
                        position: 'absolute',
                        bottom: 'calc(100% + 8px)',
                        right: 0,
                        width: '240px',
                        zIndex: 9999,
                        pointerEvents: 'none',
                      }}
                    >
                      <Box
                        bg="gray.800"
                        border="1px solid"
                        borderColor="gray.700"
                        borderRadius="lg"
                        px={3}
                        py={2}
                        shadow="lg"
                      >
                        <Text fontSize="xs" color="gray.300" fontWeight="medium" wordBreak="break-word">
                          {t('chat.contextUsed', { used: usedTokens.toLocaleString(), total: totalTokens.toLocaleString() })}
                        </Text>
                        {contextPercent >= 70 && (
                          <Text fontSize="xs" color="orange.400" mt={1}>
                            {t('chat.contextCompression')}
                          </Text>
                        )}
                      </Box>
                    </motion.div>
                  )}
                </AnimatePresence>
                <svg width="28" height="28" viewBox="0 0 28 28" style={{ transform: 'rotate(-90deg)' }}>
                  <circle cx="14" cy="14" r="10" fill="none" stroke="#5b5b67" strokeWidth="2.5" />
                  <circle
                    cx="14" cy="14" r="10" fill="none"
                    stroke={contextPercent > 0 ? ACCENT : 'transparent'}
                    strokeWidth="2.5" strokeLinecap="round"
                    strokeDasharray={circumference}
                    strokeDashoffset={dashOffset}
                    style={{ transition: 'stroke-dashoffset 0.5s ease, stroke 0.3s ease' }}
                  />
                </svg>
              </Box>

              {/* Send Button */}
              <IconButton
                aria-label="Send"
                icon={<ArrowUp size={16} />}
                bg={ACCENT}
                color="white"
                _hover={{ bg: ACCENT_HOVER }}
                isDisabled={!prompt.trim() || isSubmitting || !selectedSessionId}
                isLoading={isSubmitting}
                onClick={handleSubmit}
                borderRadius="full"
                w="36px"
                h="36px"
                minW="36px"
              />
            </HStack>
          </HStack>
        </Box>

        {/* Disclaimer — always visible */}
        <Text mx="auto" mt={2} px={2} fontSize="11px" color="gray.600" textAlign="center" maxW={CHAT_MAX_WIDTH}>
          {t('chat.disclaimer')}
        </Text>
      </Box>
    </VStack>
  );
}
