import { useEffect, useRef, useState, useCallback } from 'react';
import {
  Box,
  VStack,
  HStack,
  Flex,
  IconButton,
  Button,
  Text,
  Textarea as ChakraTextarea,
} from '@chakra-ui/react';
import {
  ArrowUp,
  Paperclip,
  ShieldCheck,
  FileEdit,
  ScrollText,
  Zap,
  Check,
} from 'lucide-react';
import { AnimatePresence, motion } from 'framer-motion';
import qwenLogo from '@/assets/qwen-logo.svg';
import { AGENT_MODES } from '@/types/ui';
import type { AgentMode } from '@/types/ui';
import { useBootstrap } from '@/hooks/useBootstrap';
import { useTranslation } from 'react-i18next';

interface ChatAreaProps {
  onToggleSidebar?: () => void;
  isSidebarOpen: boolean;
}

const ACCENT = '#615CED';
const ACCENT_HOVER = '#4e49d9';

const MODE_ICONS: Record<AgentMode, React.ReactNode> = {
  'default': <ShieldCheck size={14} />,
  'plan': <ScrollText size={14} />,
  'auto-edit': <FileEdit size={14} />,
  'yolo': <Zap size={14} />,
};

export default function ChatArea({}: ChatAreaProps) {
  const { t } = useTranslation();
  const { bootstrap } = useBootstrap();
  const sessions = bootstrap?.recentSessions ?? [];
  const [mode, setMode] = useState<AgentMode>('default');
  const [prompt, setPrompt] = useState('');
  const [messages, setMessages] = useState<Array<{ id: string; role: 'user' | 'assistant'; content: string; timestamp?: string }>>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [usedTokens, setUsedTokens] = useState(0);
  const [modeDropdownOpen, setModeDropdownOpen] = useState(false);
  const [showContextTooltip, setShowContextTooltip] = useState(false);
  const [tooltipPos, setTooltipPos] = useState({ top: 0, left: 0 });
  const donutRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const modeBtnRef = useRef<HTMLButtonElement>(null);
  const modeMenuRef = useRef<HTMLDivElement>(null);

  const totalTokens = 128_000;
  const currentModeOption = AGENT_MODES.find((m) => m.value === mode) ?? AGENT_MODES[0];
  const hasMessages = messages.length > 0;

  useEffect(() => {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, 144)}px`;
  }, [prompt]);

  // Close mode dropdown on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (
        modeDropdownOpen &&
        modeBtnRef.current &&
        !modeBtnRef.current.contains(e.target as Node) &&
        modeMenuRef.current &&
        !modeMenuRef.current.contains(e.target as Node)
      ) {
        setModeDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [modeDropdownOpen]);

  const handleSubmit = () => {
    if (!prompt.trim() || isSubmitting) return;

    const userMessage = {
      id: `${Date.now()}-user`,
      role: 'user' as const,
      content: prompt,
      timestamp: new Date().toISOString(),
    };

    setMessages(prev => [...prev, userMessage]);
    const submittedPrompt = prompt;
    setPrompt('');
    setIsSubmitting(true);
    setUsedTokens(prev => prev + Math.ceil(submittedPrompt.length / 4));

    setTimeout(() => {
      const aiMessage = {
        id: `${Date.now()}-assistant`,
        role: 'assistant' as const,
        content: t('chat.demoResponse', { prompt: submittedPrompt }),
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, aiMessage]);
      setUsedTokens(prev => prev + 2000);
      setIsSubmitting(false);
    }, 1500);
  };

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

  const handleTooltipEnter = useCallback(() => {
    setShowContextTooltip(true);
    requestAnimationFrame(() => {
      if (donutRef.current) {
        const rect = donutRef.current.getBoundingClientRect();
        const tooltipWidth = 280;
        setTooltipPos({
          // top of donut minus tooltip height minus gap
          top: rect.top - 80,
          // right edge aligned with donut right edge
          left: rect.right - tooltipWidth,
        });
      }
    });
  }, []);

  return (
    <VStack h="100%" spacing={0} bg="gray.900" align="stretch" overflow="hidden">
      {/* Header — only when messages exist */}
      {hasMessages && (
        <HStack px={6} py={3} borderBottom="1px solid" borderColor="gray.700" minH="48px">
          <Text fontWeight="medium" color="white" fontSize="sm" noOfLines={1}>
            {messages.find(m => m.role === 'user')?.content.slice(0, 80) ?? t('chat.newChat')}
          </Text>
        </HStack>
      )}

      {/* Main area */}
      <Box flex={1} overflowY="auto" sx={{
        '&::-webkit-scrollbar': { width: '6px' },
        '&::-webkit-scrollbar-track': { background: 'transparent' },
        '&::-webkit-scrollbar-thumb': { background: '#5b5b67', borderRadius: '3px' },
        '&::-webkit-scrollbar-thumb:hover': { background: '#72727f' },
      }}>
        {hasMessages ? (
          <VStack spacing={4} align="stretch" p={6}>
            {messages.map((message) => (
              <Flex key={message.id} justify={message.role === 'user' ? 'flex-end' : 'flex-start'}>
                <Box
                  maxW="70%"
                  p={4}
                  borderRadius="20px"
                  bg={message.role === 'user' ? ACCENT : 'gray.800'}
                  borderTopRightRadius={message.role === 'user' ? '4px' : '20px'}
                  borderTopLeftRadius={message.role === 'assistant' ? '4px' : '20px'}
                >
                  <Text color="white" fontSize="sm" whiteSpace="pre-wrap" wordBreak="break-word">
                    {message.content}
                  </Text>
                </Box>
              </Flex>
            ))}
          </VStack>
        ) : (
          /* Welcome Screen */
          <Flex h="100%" direction="column" align="center" justify="center" userSelect="none">
            <img
              src={qwenLogo}
              alt="Qwen"
              style={{ height: '64px', width: '64px', opacity: 0.9 }}
              draggable={false}
            />
            <Text mt={4} fontSize="2xl" fontWeight="semibold" color="white" letterSpacing="tight">
              {t('chat.welcomeTitle')}
            </Text>
            {sessions.length > 0 && (
              <Text mt={1} fontSize="sm" color="gray.500">
                {sessions.length} {t('chat.sessionsAvailable')}
              </Text>
            )}
          </Flex>
        )}
      </Box>

      {/* Input Area */}
      <Box px={4} pb={4} pt={3} position="relative">
        <Box
          mx="auto"
          w="full"
          maxW="4xl"
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

          {/* Bottom bar — py={3} = 12px top/bottom padding */}
          <HStack justify="space-between" px={4} py={3} gap={3}>
            {/* Left: attach + mode — no hover background */}
            <HStack gap={2}>
              {/* Paperclip — no hover bg, no active bg */}
              <IconButton
                aria-label="Attach file"
                icon={<Paperclip size={14} />}
                variant="ghost"
                size="sm"
                color="gray.500"
                _hover={{ color: 'white' }}
                _active={{ bg: 'transparent' }}
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
                  _active={{ bg: 'transparent' }}
                  onClick={() => setModeDropdownOpen(!modeDropdownOpen)}
                  gap={1.5}
                  overflow="hidden"
                >
                  <AnimatePresence mode="popLayout">
                    <motion.span
                      key={mode}
                      initial={{ opacity: 0, y: -12 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: 12 }}
                      transition={{ duration: 0.18, ease: 'easeOut' }}
                      style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
                    >
                      {MODE_ICONS[mode]}
                      <span style={{ fontSize: '12px' }}>{t(currentModeOption.labelKey)}</span>
                    </motion.span>
                  </AnimatePresence>
                </Button>

                {/* Dropdown with animation */}
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
                                {/* Checkmark or icon */}
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

            {/* Right: donut context ring + accent send */}
            <HStack gap={2}>
              {/* Donut ring with tooltip */}
              <Box
                ref={donutRef}
                position="relative"
                display="flex"
                alignItems="center"
                justifyContent="center"
                onMouseEnter={handleTooltipEnter}
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
                        position: 'fixed',
                        top: `${tooltipPos.top}px`,
                        left: `${tooltipPos.left}px`,
                        width: '280px',
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
                        <Text fontSize="xs" color="gray.500" mt={0.5} wordBreak="break-word">
                          {t('chat.contextCompression')}
                        </Text>
                      </Box>
                    </motion.div>
                  )}
                </AnimatePresence>
                <Box
                  w="36px"
                  h="36px"
                  display="flex"
                  alignItems="center"
                  justifyContent="center"
                  borderRadius="full"
                  cursor="default"
                >
                  <svg width="28" height="28" viewBox="0 0 28 28" style={{ transform: 'rotate(-90deg)' }}>
                    {/* Track — gray */}
                    <circle
                      cx="14"
                      cy="14"
                      r="10"
                      fill="none"
                      stroke="#5b5b67"
                      strokeWidth="2.5"
                    />
                    {/* Fill — accent */}
                    <circle
                      cx="14"
                      cy="14"
                      r="10"
                      fill="none"
                      stroke={contextPercent > 0 ? ACCENT : 'transparent'}
                      strokeWidth="2.5"
                      strokeLinecap="round"
                      strokeDasharray={circumference}
                      strokeDashoffset={dashOffset}
                      style={{ transition: 'stroke-dashoffset 0.5s ease, stroke 0.3s ease' }}
                    />
                  </svg>
                </Box>
              </Box>

              {/* Send Button — accent color */}
              <IconButton
                aria-label="Send"
                icon={<ArrowUp size={16} />}
                bg={ACCENT}
                color="white"
                _hover={{ bg: ACCENT_HOVER }}
                isDisabled={!prompt.trim() || isSubmitting}
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

        {/* Disclaimer */}
        <Text mx="auto" mt={2} px={2} fontSize="11px" color="gray.600" textAlign="center" maxW="4xl">
          {t('chat.disclaimer')}
        </Text>
      </Box>
    </VStack>
  );
}
