import {
  Badge,
  Box,
  Button,
  HStack,
  IconButton,
  Modal,
  ModalBody,
  ModalCloseButton,
  ModalContent,
  ModalHeader,
  ModalOverlay,
  Text,
  VStack,
} from '@chakra-ui/react'
import { AnimatePresence, motion } from 'framer-motion'
import { Check, Copy, Plus, RefreshCw, RadioTower, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DirectConnectServerState, DirectConnectSessionState } from '@/types/desktop'

interface DirectConnectPanelProps {
  isOpen: boolean
  onClose: () => void
}

type CopiedTarget = 'baseUrl' | 'token' | 'sse' | 'curl' | ''

const emptyServer: DirectConnectServerState = {
  enabled: false,
  listening: false,
  baseUrl: '',
  accessToken: '',
  error: '',
}

function formatShortDate(value: string): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function maskToken(token: string): string {
  if (!token) return ''
  if (token.length <= 14) return token
  return `${token.slice(0, 8)}...${token.slice(-6)}`
}

export default function DirectConnectPanel({ isOpen, onClose }: DirectConnectPanelProps) {
  const { t } = useTranslation()
  const [server, setServer] = useState<DirectConnectServerState>(emptyServer)
  const [sessions, setSessions] = useState<DirectConnectSessionState[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [copied, setCopied] = useState<CopiedTarget>('')
  const [error, setError] = useState('')

  const sseUrl = useMemo(() => {
    const sessionId = sessions[0]?.directConnectSessionId || '{directConnectSessionId}'
    return server.baseUrl
      ? `${server.baseUrl}/direct-connect/sessions/${sessionId}/events/stream?afterSequence=0`
      : ''
  }, [server.baseUrl, sessions])

  const curlExample = useMemo(() => {
    if (!server.baseUrl || !server.accessToken) return ''
    return `curl -H "Authorization: Bearer ${server.accessToken}" ${server.baseUrl}/direct-connect/sessions`
  }, [server.accessToken, server.baseUrl])

  const refresh = async () => {
    if (!window.qwenDesktop) return
    setIsLoading(true)
    setError('')
    try {
      const [nextServer, nextSessions] = await Promise.all([
        window.qwenDesktop.getDirectConnectServer(),
        window.qwenDesktop.listDirectConnectSessions(),
      ])
      setServer(nextServer)
      setSessions(nextSessions)
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : String(exception))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (!isOpen) return
    void refresh()
  }, [isOpen])

  const copyValue = async (target: CopiedTarget, value: string) => {
    if (!value) return
    try {
      await navigator.clipboard.writeText(value)
      setCopied(target)
      window.setTimeout(() => setCopied(''), 1400)
    } catch {
      setError(t('directConnect.copyFailed'))
    }
  }

  const createSession = async () => {
    if (!window.qwenDesktop) return
    setIsLoading(true)
    setError('')
    try {
      await window.qwenDesktop.createDirectConnectSession({
        preferredSessionId: '',
        workingDirectory: '',
      })
      await refresh()
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : String(exception))
      setIsLoading(false)
    }
  }

  const closeSession = async (directConnectSessionId: string) => {
    if (!window.qwenDesktop) return
    setIsLoading(true)
    setError('')
    try {
      await window.qwenDesktop.closeDirectConnectSession({ directConnectSessionId })
      await refresh()
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : String(exception))
      setIsLoading(false)
    }
  }

  const isReady = server.enabled && server.listening && Boolean(server.baseUrl && server.accessToken)

  return (
    <Modal isOpen={isOpen} onClose={onClose} isCentered size="2xl">
      <ModalOverlay bg="blackAlpha.700" backdropFilter="blur(8px)" />
      <ModalContent
        bg="#171820"
        border="1px solid"
        borderColor="whiteAlpha.200"
        borderRadius="24px"
        color="gray.100"
        overflow="hidden"
        boxShadow="0 24px 80px rgba(0, 0, 0, 0.42)"
      >
        <ModalHeader px={6} pt={5} pb={3}>
          <HStack spacing={3} align="center">
            <Box
              w="38px"
              h="38px"
              borderRadius="14px"
              bg="whiteAlpha.100"
              display="grid"
              placeItems="center"
              border="1px solid"
              borderColor="whiteAlpha.200"
            >
              <RadioTower size={18} />
            </Box>
            <Box flex={1} minW={0}>
              <HStack spacing={2}>
                <Text fontSize="lg" fontWeight="semibold">
                  {t('directConnect.title')}
                </Text>
                <Badge
                  colorScheme={isReady ? 'green' : server.enabled ? 'yellow' : 'gray'}
                  borderRadius="full"
                  px={2}
                >
                  {isReady
                    ? t('directConnect.online')
                    : server.enabled
                      ? t('directConnect.starting')
                      : t('directConnect.offline')}
                </Badge>
              </HStack>
              <Text mt={1} color="gray.500" fontSize="xs" fontWeight="normal">
                {t('directConnect.subtitle')}
              </Text>
            </Box>
            <IconButton
              aria-label={t('directConnect.refresh')}
              icon={<RefreshCw size={16} />}
              size="sm"
              variant="ghost"
              color="gray.400"
              isLoading={isLoading}
              onClick={() => void refresh()}
              _hover={{ bg: 'whiteAlpha.100', color: 'white' }}
            />
          </HStack>
        </ModalHeader>
        <ModalCloseButton top={4} right={4} color="gray.500" />

        <ModalBody px={6} pb={6}>
          <VStack spacing={4} align="stretch">
            <Box
              border="1px solid"
              borderColor="whiteAlpha.200"
              bg="blackAlpha.300"
              borderRadius="18px"
              p={4}
            >
              <VStack spacing={3} align="stretch">
                <CopyRow
                  label={t('directConnect.baseUrl')}
                  value={server.baseUrl || t('directConnect.unavailable')}
                  isDisabled={!server.baseUrl}
                  isCopied={copied === 'baseUrl'}
                  onCopy={() => copyValue('baseUrl', server.baseUrl)}
                />
                <CopyRow
                  label={t('directConnect.token')}
                  value={server.accessToken ? maskToken(server.accessToken) : t('directConnect.unavailable')}
                  isDisabled={!server.accessToken}
                  isCopied={copied === 'token'}
                  onCopy={() => copyValue('token', server.accessToken)}
                />
                <CopyRow
                  label={t('directConnect.sse')}
                  value={sseUrl || t('directConnect.unavailable')}
                  isDisabled={!sseUrl || !server.accessToken}
                  isCopied={copied === 'sse'}
                  onCopy={() => copyValue('sse', sseUrl)}
                />
                <CopyRow
                  label={t('directConnect.curl')}
                  value={curlExample || t('directConnect.unavailable')}
                  isDisabled={!curlExample}
                  isCopied={copied === 'curl'}
                  onCopy={() => copyValue('curl', curlExample)}
                />
              </VStack>
            </Box>

            {server.error && (
              <Box borderRadius="14px" bg="red.900" border="1px solid" borderColor="red.700" px={3} py={2}>
                <Text fontSize="sm" color="red.100">{server.error}</Text>
              </Box>
            )}

            {error && (
              <Box borderRadius="14px" bg="red.900" border="1px solid" borderColor="red.700" px={3} py={2}>
                <Text fontSize="sm" color="red.100">{error}</Text>
              </Box>
            )}

            <HStack justify="space-between" align="center">
              <Box>
                <Text fontSize="sm" fontWeight="semibold">{t('directConnect.sessions')}</Text>
                <Text fontSize="xs" color="gray.500">{t('directConnect.sessionsHint')}</Text>
              </Box>
              <Button
                leftIcon={<Plus size={14} />}
                size="sm"
                bg="brand.500"
                color="white"
                isDisabled={!isReady}
                isLoading={isLoading}
                onClick={() => void createSession()}
                _hover={{ bg: 'brand.600' }}
              >
                {t('directConnect.create')}
              </Button>
            </HStack>

            <VStack spacing={2} align="stretch" maxH="220px" overflowY="auto">
              <AnimatePresence initial={false}>
                {sessions.length === 0 ? (
                  <Box
                    border="1px dashed"
                    borderColor="whiteAlpha.200"
                    borderRadius="16px"
                    px={4}
                    py={5}
                    textAlign="center"
                  >
                    <Text fontSize="sm" color="gray.400">{t('directConnect.noSessions')}</Text>
                  </Box>
                ) : (
                  sessions.map((session) => (
                    <motion.div
                      key={session.directConnectSessionId}
                      initial={{ opacity: 0, y: 4 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -4 }}
                    >
                      <HStack
                        border="1px solid"
                        borderColor="whiteAlpha.200"
                        borderRadius="16px"
                        px={3}
                        py={2}
                        bg="whiteAlpha.50"
                        spacing={3}
                      >
                        <Box flex={1} minW={0}>
                          <Text fontSize="sm" color="gray.100" noOfLines={1}>
                            {session.boundSessionId || session.directConnectSessionId}
                          </Text>
                          <Text fontSize="xs" color="gray.500" noOfLines={1}>
                            {session.workingDirectory || t('directConnect.noWorkingDir')} · {formatShortDate(session.lastActivityAtUtc)}
                          </Text>
                        </Box>
                        <Badge colorScheme={session.status === 'active' ? 'green' : 'gray'} borderRadius="full">
                          {session.status}
                        </Badge>
                        <IconButton
                          aria-label={t('directConnect.closeSession')}
                          icon={<Trash2 size={14} />}
                          size="xs"
                          variant="ghost"
                          color="gray.500"
                          onClick={() => void closeSession(session.directConnectSessionId)}
                          _hover={{ bg: 'red.900', color: 'red.100' }}
                        />
                      </HStack>
                    </motion.div>
                  ))
                )}
              </AnimatePresence>
            </VStack>
          </VStack>
        </ModalBody>
      </ModalContent>
    </Modal>
  )
}

interface CopyRowProps {
  label: string
  value: string
  isDisabled: boolean
  isCopied: boolean
  onCopy: () => void
}

function CopyRow({ label, value, isDisabled, isCopied, onCopy }: CopyRowProps) {
  return (
    <HStack spacing={3} align="center">
      <Text w="82px" flexShrink={0} color="gray.500" fontSize="xs" textTransform="uppercase" letterSpacing="0.08em">
        {label}
      </Text>
      <Box
        flex={1}
        minW={0}
        border="1px solid"
        borderColor="whiteAlpha.200"
        borderRadius="12px"
        bg="#0d0e13"
        px={3}
        py={2}
      >
        <Text fontSize="sm" color={isDisabled ? 'gray.600' : 'gray.200'} noOfLines={1} fontFamily="mono">
          {value}
        </Text>
      </Box>
      <IconButton
        aria-label={label}
        icon={isCopied ? <Check size={15} /> : <Copy size={15} />}
        size="sm"
        variant="ghost"
        color={isCopied ? 'green.300' : 'gray.400'}
        isDisabled={isDisabled}
        onClick={onCopy}
        _hover={{ bg: 'whiteAlpha.100', color: 'white' }}
      />
    </HStack>
  )
}
