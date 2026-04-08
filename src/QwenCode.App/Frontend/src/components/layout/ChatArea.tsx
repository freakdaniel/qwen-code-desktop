import { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import {
  Box,
  VStack,
  HStack,
  Flex,
  IconButton,
  Button,
  Input,
  Portal,
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
import type { DesktopSessionDetail, DesktopSessionEntry, SessionPreview } from '@/types/desktop';
import qwenLogo from '@/assets/qwen-logo.svg';

interface ChatAreaProps {
  onToggleSidebar?: () => void;
  isSidebarOpen: boolean;
  selectedSessionId?: string;
  onSelectSession?: (sessionId: string) => void;
}

interface ProjectOption {
  name: string;
  path: string;
  lastActivity: string;
}

const ACCENT = '#615CED';
const ACCENT_HOVER = '#4e49d9';
const CHAT_MAX_WIDTH = '4xl';
const LIVE_TOOL_SOURCE = '__live_tool__';

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

function isLiveToolEntry(entry: DesktopSessionEntry): boolean {
  return entry.type === 'tool' && entry.sourcePath === LIVE_TOOL_SOURCE;
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

function getSessionDisplayTitle(session: SessionPreview): string {
  const title = session.title?.trim();
  if (title) return title;
  return basename(session.workingDirectory) || session.sessionId;
}

function normalizePathKey(path: string): string {
  return path.replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase();
}

function pathStartsWith(path: string, root: string): boolean {
  if (!path || !root) return false;

  const normalizedPath = normalizePathKey(path);
  const normalizedRoot = normalizePathKey(root);
  return normalizedPath === normalizedRoot || normalizedPath.startsWith(`${normalizedRoot}/`);
}

function joinDesktopPath(basePath: string, ...segments: string[]): string {
  const separator = basePath.includes('\\') ? '\\' : '/';
  const trimmedBase = basePath.replace(/[\\/]+$/, '');
  const trimmedSegments = segments
    .map((segment) => segment.replace(/^[\\/]+|[\\/]+$/g, ''))
    .filter(Boolean);

  return [trimmedBase, ...trimmedSegments].join(separator);
}

function getProjectDisplayName(workingDirectory: string, locale: string): string {
  if (!workingDirectory) {
    return locale.startsWith('ru') ? 'Без проекта' : 'No project';
  }

  return basename(workingDirectory) || workingDirectory;
}

function getProjectPickerSearchPlaceholder(locale: string): string {
  if (locale.startsWith('ru')) return 'Поиск проектов';
  if (locale.startsWith('ja')) return 'プロジェクトを検索';
  if (locale.startsWith('ko')) return '프로젝트 검색';
  if (locale.startsWith('pt')) return 'Pesquisar projetos';
  if (locale.startsWith('zh')) return '搜索项目';
  return 'Search projects';
}

function getContinueWithoutProjectLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Продолжить без проекта' : 'Continue without project';
}

function getAddProjectLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Добавить новый проект' : 'Add new project';
}

function getNoProjectsLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Проекты не найдены' : 'No projects found';
}

function getProjectlessTempDirectory(runtimeBaseDirectory: string, workspaceRoot: string): string {
  const baseDirectory = runtimeBaseDirectory.trim() || workspaceRoot.trim();
  return joinDesktopPath(baseDirectory, 'tmp', 'no-project');
}

void getSessionDisplayTitle;
void getSessionPickerPlaceholder;
void getSessionPickerSearchPlaceholder;
void getNoSessionsLabel;
void getNoSessionMatchesLabel;

function parseObjectArguments(argumentsJson: string): Record<string, unknown> | null {
  if (!argumentsJson) return null;

  try {
    const parsed = JSON.parse(argumentsJson);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return null;
    }

    return parsed as Record<string, unknown>;
  } catch {
    return null;
  }
}

interface TodoItemSummary {
  id: string;
  content: string;
  status: string;
}

interface TodoSummary {
  items: TodoItemSummary[];
  completedCount: number;
  totalCount: number;
}

function parseTodoSummary(argumentsJson: string): TodoSummary | null {
  const parsed = parseObjectArguments(argumentsJson);
  if (!parsed || !Array.isArray(parsed.todos)) {
    return null;
  }

  const items = parsed.todos
    .filter((item): item is Record<string, unknown> => !!item && typeof item === 'object' && !Array.isArray(item))
    .map((item) => ({
      id: typeof item.id === 'string' ? item.id : `${item.content ?? 'todo'}`,
      content: typeof item.content === 'string' ? item.content : '',
      status: typeof item.status === 'string' ? item.status : 'pending',
    }))
    .filter((item) => item.content);

  if (items.length === 0) {
    return null;
  }

  return {
    items,
    completedCount: items.filter((item) => item.status === 'completed').length,
    totalCount: items.length,
  };
}

function formatShellArgumentLines(argumentsJson: string): string[] {
  const parsed = parseObjectArguments(argumentsJson);
  if (!parsed) {
    return argumentsJson
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean);
  }

  const lines: string[] = [];
  const command = typeof parsed.command === 'string' ? parsed.command.trim() : '';
  const description = typeof parsed.description === 'string' ? parsed.description.trim() : '';

  if (description || command) {
    lines.push(description && command ? `${description}: ${command}` : description || command);
  }

  if (typeof parsed.timeout === 'number') {
    lines.push(`timeout: ${parsed.timeout} ms`);
  }

  if (typeof parsed.is_background === 'boolean') {
    lines.push(parsed.is_background ? 'background: true' : 'background: false');
  }

  if (typeof parsed.workdir === 'string' && parsed.workdir.trim()) {
    lines.push(`workdir: ${parsed.workdir.trim()}`);
  }

  if (lines.length > 0) {
    return lines;
  }

  return Object.entries(parsed).map(([key, value]) =>
    `${key}: ${typeof value === 'string' ? value : JSON.stringify(value)}`,
  );
}

function getShellSummary(argumentsJson: string): string {
  const parsed = parseObjectArguments(argumentsJson);
  const command = typeof parsed?.command === 'string' ? parsed.command.trim() : '';
  if (command) {
    return trunc(command);
  }

  const [firstLine] = formatShellArgumentLines(argumentsJson);
  return firstLine ? trunc(firstLine) : '';
}

function estimateSessionTokens(detail: DesktopSessionDetail | null): number {
  if (!detail) return 0;

  const totalCharacters = detail.entries.reduce(
    (sum, entry) =>
      sum +
      (entry.body?.length ?? 0) +
      (entry.thinkingBody?.length ?? 0) +
      (entry.arguments?.length ?? 0),
    0,
  );

  return Math.ceil(totalCharacters / 4);
}

function createStreamingAssistantEntry(
  sessionId: string,
  workingDirectory: string,
  gitBranch: string,
  content: string,
  timestamp: string,
): DesktopSessionEntry {
  return {
    id: `streaming-${sessionId}`,
    type: 'assistant',
    timestamp,
    workingDirectory,
    gitBranch,
    title: '',
    body: content,
    thinkingBody: '',
    status: 'streaming',
    toolName: '',
    approvalState: '',
    exitCode: null,
    arguments: '',
    scope: '',
    sourcePath: '',
    resolutionStatus: '',
    resolvedAt: '',
    changedFiles: [],
    questions: [],
    answers: [],
  };
}

function createUserEntry(
  entryId: string,
  workingDirectory: string,
  gitBranch: string,
  content: string,
  timestamp: string,
): DesktopSessionEntry {
  return {
    id: entryId,
    type: 'user',
    timestamp,
    workingDirectory,
    gitBranch,
    title: '',
    body: content,
    thinkingBody: '',
    status: 'completed',
    toolName: '',
    approvalState: '',
    exitCode: null,
    arguments: '',
    scope: '',
    sourcePath: '',
    resolutionStatus: '',
    resolvedAt: '',
    changedFiles: [],
    questions: [],
    answers: [],
  };
}

function createOptimisticSessionPreview(
  sessionId: string,
  workingDirectory: string,
  title: string,
  timestamp: string,
  gitBranch: string,
): SessionPreview {
  return {
    sessionId,
    title,
    lastActivity: timestamp,
    startedAt: timestamp,
    lastUpdatedAt: timestamp,
    category: 'recent',
    mode: 'code',
    status: 'active',
    workingDirectory,
    gitBranch,
    messageCount: 1,
    transcriptPath: '',
    metadataPath: '',
  };
}

function createOptimisticSessionDetail(
  session: SessionPreview,
  userEntry: DesktopSessionEntry,
): DesktopSessionDetail {
  return {
    session,
    transcriptPath: session.transcriptPath,
    entryCount: 1,
    windowOffset: 0,
    windowSize: 1,
    hasOlderEntries: false,
    hasNewerEntries: false,
    summary: {
      userCount: 1,
      assistantCount: 0,
      commandCount: 0,
      toolCount: 0,
      pendingApprovalCount: 0,
      pendingQuestionCount: 0,
      completedToolCount: 0,
      failedToolCount: 0,
      lastTimestamp: userEntry.timestamp,
    },
    entries: [userEntry],
  };
}

function upsertOptimisticUserEntry(
  detail: DesktopSessionDetail,
  userEntry: DesktopSessionEntry,
): DesktopSessionDetail {
  const lastEntry = detail.entries[detail.entries.length - 1];
  if (
    lastEntry?.type === 'user' &&
    lastEntry.body === userEntry.body &&
    lastEntry.timestamp === userEntry.timestamp
  ) {
    return detail;
  }

  const entries = [...detail.entries, userEntry];
  return {
    ...detail,
    session: {
      ...detail.session,
      lastActivity: userEntry.timestamp,
      lastUpdatedAt: userEntry.timestamp,
      messageCount: Math.max(detail.session.messageCount + 1, entries.length),
      status: 'active',
    },
    entryCount: detail.entryCount + 1,
    windowSize: detail.windowSize + 1,
    summary: {
      ...detail.summary,
      userCount: detail.summary.userCount + 1,
      lastTimestamp: userEntry.timestamp,
    },
    entries,
  };
}

interface LiveToolCallSnapshot {
  id: string;
  groupId: string;
  toolName: string;
  argumentsJson: string;
  status: string;
  timestamp: string;
  updatedAt: string;
  workingDirectory: string;
  gitBranch: string;
}

function normalizeToolLifecycleStatus(kind: string, status: string): string {
  const normalizedStatus = (status || '').trim().toLowerCase();
  if (normalizedStatus) {
    return normalizedStatus;
  }

  switch (kind) {
    case 'toolCompleted':
      return 'completed';
    case 'toolFailed':
      return 'error';
    case 'toolBlocked':
      return 'blocked';
    case 'toolApprovalRequired':
      return 'approval-required';
    case 'userInputRequired':
      return 'input-required';
    default:
      return 'requested';
  }
}

function isToolLifecycleEvent(event: import('@/types/desktop').DesktopSessionEvent): boolean {
  if (!event.toolName?.trim()) {
    return false;
  }

  return (
    event.kind === 'toolApprovalRequired' ||
    event.kind === 'userInputRequired' ||
    event.kind === 'toolCompleted' ||
    event.kind === 'toolBlocked' ||
    event.kind === 'toolFailed' ||
    (event.kind === 'assistantGenerating' && normalizeToolLifecycleStatus(event.kind, event.status) === 'requested')
  );
}

function isToolPendingStatus(status: string): boolean {
  const normalized = status.trim().toLowerCase();
  return (
    normalized === 'requested' ||
    normalized === 'running' ||
    normalized === 'streaming' ||
    normalized === 'approval-required' ||
    normalized === 'input-required'
  );
}

function buildLiveToolEntries(
  events: import('@/types/desktop').DesktopSessionEvent[],
  workingDirectory: string,
  gitBranch: string,
): DesktopSessionEntry[] {
  const calls: LiveToolCallSnapshot[] = [];

  for (const event of events) {
    if (!isToolLifecycleEvent(event)) {
      continue;
    }

    const normalizedStatus = normalizeToolLifecycleStatus(event.kind, event.status);
    let call =
      (event.toolCallId
        ? calls.find((item) => item.id === event.toolCallId)
        : undefined) ??
      [...calls].reverse().find((item) =>
        item.toolName === event.toolName && isToolPendingStatus(item.status),
      );

    if (!call) {
      call = {
        id: event.toolCallId || `live-tool-${calls.length}-${event.toolName}-${event.timestampUtc}`,
        groupId: event.toolCallGroupId || `live-tool-group-${calls.length}`,
        toolName: event.toolName,
        argumentsJson: event.toolArgumentsJson || '{}',
        status: normalizedStatus,
        timestamp: event.timestampUtc,
        updatedAt: event.timestampUtc,
        workingDirectory: event.workingDirectory || workingDirectory,
        gitBranch: event.gitBranch || gitBranch,
      };
      calls.push(call);
    }

    call.toolName = event.toolName || call.toolName;
    call.groupId = event.toolCallGroupId || call.groupId;
    call.argumentsJson = event.toolArgumentsJson || call.argumentsJson || '{}';
    call.status = normalizedStatus;
    call.updatedAt = event.timestampUtc;
    call.workingDirectory = event.workingDirectory || call.workingDirectory || workingDirectory;
    call.gitBranch = event.gitBranch || call.gitBranch || gitBranch;
  }

  return calls.map((call) => ({
    id: call.id,
    type: 'tool',
    timestamp: call.updatedAt,
    workingDirectory: call.workingDirectory || workingDirectory,
    gitBranch: call.gitBranch || gitBranch,
    title: call.toolName,
    body: '',
    thinkingBody: '',
    status: call.status,
    toolName: call.toolName,
    approvalState: '',
    exitCode: null,
    arguments: call.argumentsJson || '{}',
    scope: call.groupId,
    sourcePath: LIVE_TOOL_SOURCE,
    resolutionStatus: 'live',
    resolvedAt: '',
    changedFiles: [],
    questions: [],
    answers: [],
  }));
}

function formatThinkingDuration(locale: string, durationMs: number): string {
  if (durationMs < 1_000) {
    return locale.startsWith('ru')
      ? `${Math.max(1, Math.round(durationMs))} мс`
      : `${Math.max(1, Math.round(durationMs))} ms`;
  }

  if (durationMs < 60_000) {
    const seconds = durationMs / 1_000;
    const formatted = seconds < 10 ? seconds.toFixed(1).replace(/\.0$/, '') : Math.round(seconds).toString();
    return locale.startsWith('ru') ? `${formatted} с` : `${formatted}s`;
  }

  const minutes = Math.floor(durationMs / 60_000);
  const seconds = Math.round((durationMs % 60_000) / 1_000);
  if (locale.startsWith('ru')) {
    return seconds > 0 ? `${minutes} мин ${seconds} с` : `${minutes} мин`;
  }

  return seconds > 0 ? `${minutes}m ${seconds}s` : `${minutes}m`;
}

function getThinkingStatusLabel(locale: string, durationMs: number): string {
  if (durationMs > 0) {
    return locale.startsWith('ru')
      ? `Думал в течение ${formatThinkingDuration(locale, durationMs)}`
      : `Thought for ${formatThinkingDuration(locale, durationMs)}`;
  }

  return locale.startsWith('ru') ? 'Думаю' : 'Thinking';
}

function normalizeWittyLoadingPhrases(value: unknown, fallback: string): string[] {
  if (!Array.isArray(value)) {
    return [fallback];
  }

  const phrases = value
    .filter((item): item is string => typeof item === 'string')
    .map((item) => item.trim())
    .filter(Boolean);

  return phrases.length > 0 ? phrases : [fallback];
}

function pickWittyLoadingPhrase(phrases: readonly string[], fallback: string, previous = ''): string {
  if (phrases.length === 0) {
    return fallback;
  }

  if (phrases.length === 1) {
    return phrases[0] || fallback;
  }

  const candidates = phrases.filter((phrase) => phrase !== previous);
  const pool = candidates.length > 0 ? candidates : phrases;
  return pool[Math.floor(Math.random() * pool.length)] || fallback;
}

function getSessionPickerPlaceholder(locale: string): string {
  return locale.startsWith('ru') ? 'Выберите чат' : 'Select a chat';
}

function getSessionPickerSearchPlaceholder(locale: string): string {
  return locale.startsWith('ru') ? 'Поиск чатов' : 'Search chats';
}

function getNoSessionsLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Чаты пока не найдены' : 'No chats available yet';
}

function getNoSessionMatchesLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Ничего не найдено' : 'No matching chats';
}

function getTodoStatusLabel(locale: string, status: string): string {
  const normalized = status.toLowerCase();
  if (normalized === 'completed' || normalized === 'done') {
    return locale.startsWith('ru') ? 'Выполнено' : 'Completed';
  }

  if (normalized === 'in_progress' || normalized === 'in-progress') {
    return locale.startsWith('ru') ? 'В работе' : 'In progress';
  }

  return locale.startsWith('ru') ? 'Ожидает' : 'Pending';
}

function AnimatedThinkingLabel({
  label,
  color = '#a1a1aa',
  dotColor = '#a1a1aa',
  fontSize = '12px',
}: {
  label: string;
  color?: string;
  dotColor?: string;
  fontSize?: string;
}) {
  return (
    <HStack spacing={0.5} align="center">
      <Text fontSize={fontSize} color={color} fontWeight="medium" whiteSpace="nowrap">
        {label}
      </Text>
      <HStack as="span" spacing={0.5} align="center">
        {[0, 1, 2].map((index) => (
          <motion.span
            key={index}
            animate={{ opacity: [0.18, 1, 0.18] }}
            transition={{
              duration: 1.15,
              repeat: Number.POSITIVE_INFINITY,
              ease: 'easeInOut',
              delay: index * 0.18,
            }}
            style={{
              color: dotColor,
              fontSize,
              lineHeight: 1,
              display: 'inline-block',
              minWidth: '0.18em',
              textAlign: 'center',
            }}
          >
            .
          </motion.span>
        ))}
      </HStack>
    </HStack>
  );
}

function getToolStatusColor(status: string): string {
  const normalized = status.trim().toLowerCase();
  if (normalized === 'completed') {
    return '#86efac';
  }

  if (normalized === 'error' || normalized === 'failed' || normalized === 'blocked') {
    return '#fca5a5';
  }

  if (normalized === 'approval-required' || normalized === 'input-required') {
    return '#fcd34d';
  }

  return '#60a5fa';
}

// Tool-specific argument summary for display
function getToolArgSummary(entry: DesktopSessionEntry): string {
  if (!entry.arguments) return '';
  const toolKey = (entry.toolName || entry.title || '').toLowerCase();
  try {
    const a = JSON.parse(entry.arguments) as Record<string, unknown>;
    const str = (k: string) => typeof a[k] === 'string' ? (a[k] as string) : '';

    if (toolKey.includes('web_fetch') || toolKey.includes('fetch')) {
      const url = str('url') || str('uri') || str('href');
      return url ? trunc(url, 88) : '';
    }

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
    if (toolKey.includes('todo')) {
      const todoSummary = parseTodoSummary(entry.arguments);
      return todoSummary ? `${todoSummary.completedCount}/${todoSummary.totalCount}` : '';
    }
    if (toolKey.includes('bash') || toolKey.includes('execute') || toolKey.includes('shell') || toolKey.includes('run')) {
      return getShellSummary(entry.arguments);
    }
    // generic fallback: first meaningful string value
    const v = str('description') || str('command') || str('pattern') || str('file_path') || str('path') || str('query') || str('prompt');
    return v ? trunc(v) : '';
  } catch {
    return trunc(entry.arguments);
  }
}

function getLiveToolLabel(locale: string): string {
  if (locale.startsWith('ru')) {
    return 'Сейчас';
  }

  return 'Live';
}

export default function ChatArea({ selectedSessionId, onSelectSession }: ChatAreaProps) {
  const { t } = useTranslation();
  const {
    bootstrap,
    sessionCache,
    loadSessionDetail,
    setBootstrap,
    setSessionCache,
    activeTurnSessions,
    liveSessionEvents,
    streamingSnapshots,
    latestSessionEvent,
  } = useBootstrap();
  const sessions = bootstrap?.recentSessions ?? [];
  const locale = bootstrap?.currentLocale ?? 'en';
  const selectedSession = sessions.find(s => s.sessionId === selectedSessionId);
  const runtimeTempRoot = useMemo(
    () => joinDesktopPath(bootstrap?.qwenRuntime?.runtimeBaseDirectory ?? bootstrap?.workspaceRoot ?? '', 'tmp'),
    [bootstrap?.qwenRuntime?.runtimeBaseDirectory, bootstrap?.workspaceRoot],
  );

  const [mode, setMode] = useState<AgentMode>('default');
  const [prompt, setPrompt] = useState('');
  const [pendingTurnSessionIds, setPendingTurnSessionIds] = useState<Record<string, true>>({});
  const [usedTokens, setUsedTokens] = useState(0);
  const [modeDropdownOpen, setModeDropdownOpen] = useState(false);
  const [showContextTooltip, setShowContextTooltip] = useState(false);
  const [projectPickerOpen, setProjectPickerOpen] = useState(false);
  const [projectPickerQuery, setProjectPickerQuery] = useState('');
  const [selectedProjectMode, setSelectedProjectMode] = useState<'project' | 'no-project'>('project');
  const [selectedProjectPath, setSelectedProjectPath] = useState('');
  const [customProjectPaths, setCustomProjectPaths] = useState<string[]>([]);
  const [loadingPhrase, setLoadingPhrase] = useState('');
  const [projectPickerPosition, setProjectPickerPosition] = useState({ top: 0, left: 0, width: 320, maxHeight: 320 });

  // Session data from IPC
  const [sessionDetail, setSessionDetail] = useState<DesktopSessionDetail | null>(null);
  const [isLoadingSession, setIsLoadingSession] = useState(false);

  const donutRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const modeBtnRef = useRef<HTMLButtonElement>(null);
  const modeMenuRef = useRef<HTMLDivElement>(null);
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const projectPickerButtonRef = useRef<HTMLButtonElement>(null);
  const projectPickerMenuRef = useRef<HTMLDivElement>(null);
  const shouldStickToBottomRef = useRef(true);
  const scrollFrameRef = useRef<number | null>(null);

  const selectedModel = useMemo(() => {
    const models = bootstrap?.qwenModels?.availableModels ?? [];
    return (
      models.find((model) => model.isDefaultModel) ??
      models.find((model) => model.id === bootstrap?.qwenRuntime?.modelName) ??
      models.find((model) => model.id === bootstrap?.qwenAuth?.model) ??
      null
    );
  }, [bootstrap]);

  const totalTokens = selectedModel?.contextWindowSize ?? 131_072;
  const currentModeOption = AGENT_MODES.find((m) => m.value === mode) ?? AGENT_MODES[0];
  const hasSession = !!selectedSessionId;
  const streamingSnapshot = selectedSessionId ? streamingSnapshots[selectedSessionId] ?? '' : '';
  const isSessionStreaming = !!selectedSessionId && !!activeTurnSessions[selectedSessionId];
  const isPendingSelectedSession = !!selectedSessionId && !!pendingTurnSessionIds[selectedSessionId];
  const isComposerBusy = isPendingSelectedSession || isSessionStreaming;
  const isAwaitingAssistantText = hasSession && isComposerBusy && !streamingSnapshot.trim();
  const defaultThinkingLabel = t('tools.thinking');
  const plainThinkingLabel = getThinkingStatusLabel(locale, 0);
  const wittyLoadingPhrases = useMemo(
    () => normalizeWittyLoadingPhrases(
      t('tools.wittyLoadingPhrases', { returnObjects: true, defaultValue: [] }),
      defaultThinkingLabel,
    ),
    [defaultThinkingLabel, t],
  );
  const liveToolEntries = useMemo(
    () =>
      selectedSessionId && isSessionStreaming
        ? buildLiveToolEntries(
          liveSessionEvents[selectedSessionId] ?? [],
          selectedSession?.workingDirectory ?? sessionDetail?.session.workingDirectory ?? '',
          selectedSession?.gitBranch ?? sessionDetail?.session.gitBranch ?? '',
        )
        : [],
    [
      isSessionStreaming,
      liveSessionEvents,
      selectedSession?.gitBranch,
      selectedSession?.workingDirectory,
      selectedSessionId,
      sessionDetail?.session.gitBranch,
      sessionDetail?.session.workingDirectory,
    ],
  );

  const projectOptions = useMemo(() => {
    const projectMap = new Map<string, ProjectOption>();
    const appendProject = (path: string, lastActivity: string) => {
      if (!path || pathStartsWith(path, runtimeTempRoot)) {
        return;
      }

      const key = normalizePathKey(path);
      const existing = projectMap.get(key);
      const nextActivity = lastActivity || existing?.lastActivity || '';
      projectMap.set(key, {
        name: getProjectDisplayName(path, locale),
        path,
        lastActivity: nextActivity,
      });
    };

    appendProject(bootstrap?.workspaceRoot ?? '', '');
    customProjectPaths.forEach((path) => appendProject(path, ''));
    sessions
      .slice()
      .sort((left, right) => Date.parse(right.lastActivity) - Date.parse(left.lastActivity))
      .forEach((session) => appendProject(session.workingDirectory, session.lastActivity));

    return Array.from(projectMap.values()).sort((left, right) => {
      if (!left.lastActivity && !right.lastActivity) return left.name.localeCompare(right.name);
      if (!left.lastActivity) return 1;
      if (!right.lastActivity) return -1;
      return Date.parse(right.lastActivity) - Date.parse(left.lastActivity);
    });
  }, [bootstrap?.workspaceRoot, customProjectPaths, locale, runtimeTempRoot, sessions]);

  const topProjectOptions = useMemo(() => projectOptions.slice(0, 3), [projectOptions]);

  const filteredProjectOptions = useMemo(() => {
    const query = projectPickerQuery.trim().toLowerCase();
    if (!query) {
      return topProjectOptions;
    }

    return projectOptions.filter((project) =>
      project.name.toLowerCase().includes(query) ||
      project.path.toLowerCase().includes(query),
    );
  }, [projectOptions, projectPickerQuery, topProjectOptions]);

  const selectedProjectLabel = selectedProjectMode === 'no-project'
    ? getContinueWithoutProjectLabel(locale)
    : getProjectDisplayName(selectedProjectPath, locale);
  const selectedProjectWorkingDirectory = selectedProjectMode === 'no-project'
    ? getProjectlessTempDirectory(bootstrap?.qwenRuntime?.runtimeBaseDirectory ?? '', bootstrap?.workspaceRoot ?? '')
    : selectedProjectPath;
  const projectListHeight = useMemo(() => {
    const visibleRowCount = Math.min(3, Math.max(filteredProjectOptions.length, 1));
    const desiredHeight = 16 + (visibleRowCount * 40) + ((visibleRowCount - 1) * 4);
    return Math.min(desiredHeight, Math.max(120, projectPickerPosition.maxHeight - 126));
  }, [filteredProjectOptions.length, projectPickerPosition.maxHeight]);
  const updateProjectPickerPosition = useCallback(() => {
    const buttonRect = projectPickerButtonRef.current?.getBoundingClientRect();
    if (!buttonRect) {
      return;
    }

    const menuWidth = Math.min(320, window.innerWidth - 32);
    const desiredLeft = buttonRect.left + (buttonRect.width / 2) - (menuWidth / 2);
    const clampedLeft = Math.min(
      window.innerWidth - 16 - menuWidth,
      Math.max(16, desiredLeft),
    );
    const preferredTop = buttonRect.bottom + 12;
    const maxMenuHeight = 360;
    const minTop = 24;
    const finalTop = Math.max(minTop, Math.min(preferredTop, window.innerHeight - maxMenuHeight - 24));
    setProjectPickerPosition({
      top: finalTop,
      left: clampedLeft,
      width: menuWidth,
      maxHeight: Math.max(220, window.innerHeight - finalTop - 24),
    });
  }, []);

  const formatTimestamp = useCallback((ts: string): string => {
    if (!ts) return '';
    try {
      return new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch {
      return '';
    }
  }, []);

  const displaySessionDetail = useMemo(() => {
    if (!sessionDetail || !selectedSessionId || !isSessionStreaming) {
      return sessionDetail;
    }

    const syntheticEntries: DesktopSessionEntry[] = [...liveToolEntries];
    const hasStreamingAssistant = streamingSnapshot.trim().length > 0;
    const lastNonSystemEntry = [...sessionDetail.entries]
      .reverse()
      .find((entry) => entry.type !== 'system' && entry.type !== 'tool_result');

    if (
      hasStreamingAssistant &&
      !(lastNonSystemEntry?.type === 'assistant' && (lastNonSystemEntry.body ?? '') === streamingSnapshot)
    ) {
      const timestamp = latestSessionEvent?.sessionId === selectedSessionId
        ? latestSessionEvent.timestampUtc
        : new Date().toISOString();
      syntheticEntries.push(
        createStreamingAssistantEntry(
          selectedSessionId,
          selectedSession?.workingDirectory ?? sessionDetail.session.workingDirectory,
          selectedSession?.gitBranch ?? sessionDetail.session.gitBranch,
          streamingSnapshot,
          timestamp,
        ),
      );
    }

    if (syntheticEntries.length === 0) {
      return sessionDetail;
    }

    const syntheticAssistantCount = syntheticEntries.filter((entry) => entry.type === 'assistant').length;
    const syntheticToolCount = syntheticEntries.filter((entry) => entry.type === 'tool').length;
    const lastTimestamp = syntheticEntries[syntheticEntries.length - 1]?.timestamp ?? sessionDetail.summary.lastTimestamp;

    return {
      ...sessionDetail,
      entryCount: sessionDetail.entryCount + syntheticEntries.length,
      windowSize: sessionDetail.windowSize + syntheticEntries.length,
      summary: {
        ...sessionDetail.summary,
        assistantCount: sessionDetail.summary.assistantCount + syntheticAssistantCount,
        toolCount: sessionDetail.summary.toolCount + syntheticToolCount,
        lastTimestamp,
      },
      entries: [...sessionDetail.entries, ...syntheticEntries],
    };
  }, [
    liveToolEntries,
    isSessionStreaming,
    latestSessionEvent,
    selectedSession?.gitBranch,
    selectedSession?.workingDirectory,
    selectedSessionId,
    sessionDetail,
    streamingSnapshot,
  ]);

  const updateStickToBottomState = useCallback(() => {
    const container = scrollContainerRef.current;
    if (!container) {
      return;
    }

    const distanceToBottom = container.scrollHeight - container.scrollTop - container.clientHeight;
    shouldStickToBottomRef.current = distanceToBottom <= 72;
  }, []);

  const scrollToBottom = useCallback((force = false) => {
    const container = scrollContainerRef.current;
    if (!container || (!force && !shouldStickToBottomRef.current)) {
      return;
    }

    if (scrollFrameRef.current !== null) {
      window.cancelAnimationFrame(scrollFrameRef.current);
    }

    scrollFrameRef.current = window.requestAnimationFrame(() => {
      const currentContainer = scrollContainerRef.current;
      if (!currentContainer) {
        scrollFrameRef.current = null;
        return;
      }

      currentContainer.scrollTop = currentContainer.scrollHeight;
      shouldStickToBottomRef.current = true;
      scrollFrameRef.current = null;
    });
  }, []);

  useEffect(() => () => {
    if (scrollFrameRef.current !== null) {
      window.cancelAnimationFrame(scrollFrameRef.current);
    }
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [displaySessionDetail?.entries.length, isAwaitingAssistantText, scrollToBottom, streamingSnapshot]);

  useEffect(() => {
    setUsedTokens(estimateSessionTokens(displaySessionDetail));
  }, [displaySessionDetail]);

  useEffect(() => {
    shouldStickToBottomRef.current = true;
    scrollToBottom(true);
  }, [scrollToBottom, selectedSessionId]);

  useEffect(() => {
    if (!selectedSessionId || !activeTurnSessions[selectedSessionId]) {
      return;
    }

    setPendingTurnSessionIds((current) => {
      if (!(selectedSessionId in current)) {
        return current;
      }

      const next = { ...current };
      delete next[selectedSessionId];
      return next;
    });
  }, [activeTurnSessions, selectedSessionId]);

  useEffect(() => {
    if (!projectPickerOpen) return;

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node | null;
      if (!target) return;

      if (projectPickerMenuRef.current?.contains(target) || projectPickerButtonRef.current?.contains(target)) {
        return;
      }

      setProjectPickerOpen(false);
    };

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, [projectPickerOpen]);

  useEffect(() => {
    if (!projectPickerOpen) return;

    updateProjectPickerPosition();
    window.addEventListener('resize', updateProjectPickerPosition);
    window.addEventListener('scroll', updateProjectPickerPosition, true);
    return () => {
      window.removeEventListener('resize', updateProjectPickerPosition);
      window.removeEventListener('scroll', updateProjectPickerPosition, true);
    };
  }, [projectPickerOpen, updateProjectPickerPosition]);

  useEffect(() => {
    if (!modeDropdownOpen) return;

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node | null;
      if (!target) return;

      if (modeMenuRef.current?.contains(target) || modeBtnRef.current?.contains(target)) {
        return;
      }

      setModeDropdownOpen(false);
    };

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, [modeDropdownOpen]);

  useEffect(() => {
    if (selectedProjectPath) return;

    const fallbackProjectPath =
      bootstrap?.workspaceRoot ??
      sessions.find((session) => !pathStartsWith(session.workingDirectory, runtimeTempRoot))?.workingDirectory ??
      '';

    if (fallbackProjectPath) {
      setSelectedProjectPath(fallbackProjectPath);
    }
  }, [bootstrap?.workspaceRoot, runtimeTempRoot, selectedProjectPath, sessions]);

  useEffect(() => {
    if (!selectedSessionId) {
      return;
    }

    const cachedDetail = sessionCache[selectedSessionId];
    if (!cachedDetail) {
      return;
    }

    setSessionDetail(cachedDetail);
    setUsedTokens(estimateSessionTokens(cachedDetail));
    setIsLoadingSession(false);
  }, [selectedSessionId, sessionCache]);

  // Load session
  useEffect(() => {
    if (!selectedSessionId) {
      setSessionDetail(null);
      setUsedTokens(0);
      setIsLoadingSession(false);
      return;
    }

    const cachedDetail = sessionCache[selectedSessionId];

    if (cachedDetail) {
      setSessionDetail(cachedDetail);
      setUsedTokens(estimateSessionTokens(cachedDetail));
      setIsLoadingSession(false);
      return;
    }

    let cancelled = false;
    setSessionDetail(null);
    setUsedTokens(0);
    setIsLoadingSession(true);

    const loadSession = async () => {
      try {
        const detail = await loadSessionDetail(selectedSessionId, { limit: 200 });
        if (!cancelled && detail) {
          setSessionDetail(detail);
          setUsedTokens(estimateSessionTokens(detail));
        }
      } catch (err) {
        console.error('Failed to load session:', err);
      } finally {
        if (!cancelled) setIsLoadingSession(false);
      }
    };

    void loadSession();
    return () => { cancelled = true; };
  }, [loadSessionDetail, selectedSessionId]);

  useEffect(() => {
    if (!isAwaitingAssistantText) {
      setLoadingPhrase('');
      return;
    }

    const firstPhrase = pickWittyLoadingPhrase(wittyLoadingPhrases, defaultThinkingLabel);
    setLoadingPhrase(firstPhrase);

    const intervalId = window.setInterval(() => {
      setLoadingPhrase((current) => pickWittyLoadingPhrase(wittyLoadingPhrases, defaultThinkingLabel, current));
    }, 2600);

    return () => window.clearInterval(intervalId);
  }, [defaultThinkingLabel, isAwaitingAssistantText, wittyLoadingPhrases]);

  const handleSubmit = useCallback(async () => {
    const trimmedPrompt = prompt.trim();
    const targetSessionId = selectedSessionId || window.crypto?.randomUUID?.() || `session-${Date.now()}`;
    const requestTimestamp = new Date().toISOString();
    const workingDirectory = selectedSession?.workingDirectory
      ?? (selectedProjectMode === 'no-project'
        ? joinDesktopPath(
          getProjectlessTempDirectory(bootstrap?.qwenRuntime?.runtimeBaseDirectory ?? '', bootstrap?.workspaceRoot ?? ''),
          window.crypto?.randomUUID?.() ?? `chat-${Date.now()}`,
        )
        : selectedProjectWorkingDirectory);

    if (!trimmedPrompt || !workingDirectory || !window.qwenDesktop) {
      return;
    }

    const gitBranch = selectedSession?.gitBranch ?? '';
    const userEntry = createUserEntry(
      window.crypto?.randomUUID?.() ?? `user-${Date.now()}`,
      workingDirectory,
      gitBranch,
      trimmedPrompt,
      requestTimestamp,
    );
    const optimisticPreview = createOptimisticSessionPreview(
      targetSessionId,
      workingDirectory,
      trimmedPrompt,
      requestTimestamp,
      gitBranch,
    );

    setPrompt('');
    setProjectPickerOpen(false);
    setProjectPickerQuery('');
    shouldStickToBottomRef.current = true;
    setPendingTurnSessionIds((current) => ({ ...current, [targetSessionId]: true }));
    setBootstrap((current) => ({
      ...current,
      recentSessions: [optimisticPreview, ...current.recentSessions.filter((session) => session.sessionId !== targetSessionId)]
        .sort((left, right) => Date.parse(right.lastActivity) - Date.parse(left.lastActivity)),
    }));
    setSessionCache((current) => {
      const existingDetail = current[targetSessionId];
      const nextDetail = existingDetail
        ? upsertOptimisticUserEntry(existingDetail, userEntry)
        : createOptimisticSessionDetail(optimisticPreview, userEntry);
      return {
        ...current,
        [targetSessionId]: nextDetail,
      };
    });
    onSelectSession?.(targetSessionId);

    void window.qwenDesktop.startSessionTurn({
        sessionId: targetSessionId,
        prompt: trimmedPrompt,
        workingDirectory,
        toolName: '',
        toolArgumentsJson: '{}',
        approveToolExecution: false,
      })
      .then(async (result) => {
        if (!result?.session?.sessionId) {
          throw new Error('Desktop session turn did not return a valid session.');
        }

        setBootstrap((current) => ({
          ...current,
          recentSessions: [result.session, ...current.recentSessions.filter((session) => session.sessionId !== result.session.sessionId)]
            .sort((left, right) => Date.parse(right.lastActivity) - Date.parse(left.lastActivity)),
        }));
        await loadSessionDetail(result.session.sessionId, { force: true, limit: 200 });
      })
      .catch((error) => {
        console.error('Failed to submit prompt:', error);
      })
      .finally(() => {
        setPendingTurnSessionIds((current) => {
          if (!(targetSessionId in current)) {
            return current;
          }

          const next = { ...current };
          delete next[targetSessionId];
          return next;
        });
      });
  }, [
    loadSessionDetail,
    onSelectSession,
    prompt,
    selectedProjectMode,
    selectedProjectWorkingDirectory,
    selectedSession?.gitBranch,
    selectedSession?.workingDirectory,
    selectedSessionId,
    setBootstrap,
    setSessionCache,
    bootstrap?.qwenRuntime?.runtimeBaseDirectory,
    bootstrap?.workspaceRoot,
  ]);

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
    if (!displaySessionDetail?.entries) return [];

    type Block =
      | { type: 'user'; entries: DesktopSessionEntry[] }
      | { type: 'assistant'; entries: DesktopSessionEntry[] }
      | { type: 'tool-group'; entries: DesktopSessionEntry[] }
      | { type: 'thought'; entries: DesktopSessionEntry[] };

    const blocks: Block[] = [];
    let currentBlock: Block | null = null;

    for (const entry of displaySessionDetail.entries) {
      if (entry.type === 'system' || entry.type === 'tool_result') continue;

      const isUser = entry.type === 'user';
      const isTool = entry.type === 'tool' || !!entry.toolName;
      const isThought = isThinkingEntry(entry);

      const blockType: Block['type'] =
        isThought ? 'thought' : isTool ? 'tool-group' : isUser ? 'user' : 'assistant';

      const shouldSplitToolGroup =
        blockType === 'tool-group' &&
        currentBlock?.type === 'tool-group' &&
        ((currentBlock.entries[currentBlock.entries.length - 1]?.scope || '') !== (entry.scope || '')) &&
        ((currentBlock.entries[currentBlock.entries.length - 1]?.scope || '') || (entry.scope || ''));

      if (!currentBlock || currentBlock.type !== blockType || shouldSplitToolGroup) {
        if (currentBlock) blocks.push(currentBlock);
        currentBlock = { type: blockType, entries: [entry] };
      } else {
        currentBlock.entries.push(entry);
      }
    }

    if (currentBlock) blocks.push(currentBlock);
    return blocks;
  }, [displaySessionDetail?.entries]);

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
      <Box
        ref={scrollContainerRef}
        flex={1}
        overflowY="scroll"
        onScroll={updateStickToBottomState}
        sx={{
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
          ) : displaySessionDetail && displaySessionDetail.entries.length > 0 ? (
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
                      const hasLiveEntries = block.entries.some((entry) => isLiveToolEntry(entry));

                      // ── Single tool: show inline, no expand needed ──
                      if (count === 1) {
                        const entry = block.entries[0];
                        const isLiveEntry = isLiveToolEntry(entry);
                        const info = getToolInfo(entry.toolName || entry.title || '');
                        const ToolIcon = info.Icon;
                        const label = t(info.labelKey);
                        const todoSummary = parseTodoSummary(entry.arguments);
                        const summary = getToolArgSummary(entry);
                        const isCollapsed = collapsedBlocks[blockKey] !== false;
                        const hasTodoDetail = !!todoSummary;
                        const canExpand = hasTodoDetail;
                        const files = entry.changedFiles ?? [];

                        return (
                          <Box key={blockKey} py={0.5}>
                            <HStack
                              spacing={2}
                              px={2}
                              h="26px"
                              color="gray.500"
                              cursor={canExpand ? 'pointer' : 'default'}
                              onClick={canExpand ? () => toggleBlock(blockKey) : undefined}
                              _hover={canExpand ? { color: 'gray.300' } : undefined}
                              role={canExpand ? 'button' : undefined}
                            >
                              {canExpand && (
                                <motion.span animate={{ rotate: isCollapsed ? 0 : 90 }} transition={{ duration: 0.18 }} style={{ display: 'flex' }}>
                                  <ChevronRight size={11} />
                                </motion.span>
                              )}
                              <Box color="gray.500" flexShrink={0}><ToolIcon size={12} /></Box>
                              <Text fontSize="xs" color="gray.400" fontWeight="medium" flexShrink={0}>{label}</Text>
                              {isLiveEntry && isToolPendingStatus(entry.status) && (
                                <HStack
                                  spacing={1.5}
                                  px={1.5}
                                  py="2px"
                                  borderRadius="full"
                                  bg="rgba(97,92,237,0.10)"
                                  border="1px solid rgba(97,92,237,0.22)"
                                  flexShrink={0}
                                >
                                  <motion.span
                                    animate={{ scale: [0.92, 1.18, 0.92], opacity: [0.45, 1, 0.45] }}
                                    transition={{ duration: 1.1, repeat: Number.POSITIVE_INFINITY, ease: 'easeInOut' }}
                                    style={{
                                      display: 'inline-flex',
                                      width: '6px',
                                      height: '6px',
                                      borderRadius: '9999px',
                                      background: '#a5b4fc',
                                      boxShadow: '0 0 12px rgba(165,180,252,0.7)',
                                    }}
                                  />
                                  <Text fontSize="10px" color="#c7d2fe" fontWeight="semibold" letterSpacing="0.04em" textTransform="uppercase">
                                    {getLiveToolLabel(locale)}
                                  </Text>
                                </HStack>
                              )}
                              {summary && (
                                <HStack spacing={2} flex={1} minW={0}>
                                  <Box
                                    boxSize="5px"
                                    borderRadius="full"
                                    bg={getToolStatusColor(entry.status)}
                                    flexShrink={0}
                                    boxShadow={isToolPendingStatus(entry.status) ? `0 0 10px ${getToolStatusColor(entry.status)}` : 'none'}
                                  />
                                  <Text fontSize="xs" color="gray.600" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap" minW={0}>
                                    {summary}
                                  </Text>
                                </HStack>
                              )}
                              {!summary && (
                                <Box
                                  boxSize="5px"
                                  borderRadius="full"
                                  bg={getToolStatusColor(entry.status)}
                                  flexShrink={0}
                                  boxShadow={isToolPendingStatus(entry.status) ? `0 0 10px ${getToolStatusColor(entry.status)}` : 'none'}
                                />
                              )}
                            </HStack>
                            <AnimatePresence initial={false}>
                              {canExpand && !isCollapsed && (
                                <motion.div key="sh" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.18, ease: 'easeOut' }} style={{ overflow: 'hidden' }}>
                                  <Box ml={7} mt={0.5} mb={1} px={2} py={1.5} bg="gray.900" borderRadius="lg">
                                    {hasTodoDetail && todoSummary && (
                                      <VStack spacing={1.5} align="stretch">
                                        <Text fontSize="xs" color="gray.400">
                                          {locale.startsWith('ru')
                                            ? `Выполнено ${todoSummary.completedCount} из ${todoSummary.totalCount}`
                                            : `${todoSummary.completedCount} of ${todoSummary.totalCount} completed`}
                                        </Text>
                                        {todoSummary.items.map((item) => {
                                          const normalizedStatus = item.status.toLowerCase();
                                          const isCompleted = normalizedStatus === 'completed' || normalizedStatus === 'done';
                                          return (
                                            <HStack key={item.id} spacing={2} align="start">
                                              <Box mt="2px" color={isCompleted ? 'green.400' : 'gray.500'}>
                                                <CheckSquare size={12} />
                                              </Box>
                                              <Box flex={1} minW={0}>
                                                <Text fontSize="xs" color={isCompleted ? 'gray.200' : 'gray.300'} textDecoration={isCompleted ? 'line-through' : 'none'} whiteSpace="pre-wrap" wordBreak="break-word">
                                                  {item.content}
                                                </Text>
                                                <Text fontSize="10px" color="gray.500">
                                                  {getTodoStatusLabel(locale, item.status)}
                                                </Text>
                                              </Box>
                                            </HStack>
                                          );
                                        })}
                                      </VStack>
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
                      const pendingCount = block.entries.filter((entry) => isToolPendingStatus(entry.status)).length;
                      const failedCount = block.entries.filter((entry) => {
                        const normalized = entry.status.trim().toLowerCase();
                        return normalized === 'error' || normalized === 'failed' || normalized === 'blocked';
                      }).length;
                      const livePendingCount = block.entries.filter((entry) => isLiveToolEntry(entry) && isToolPendingStatus(entry.status)).length;
                      const headerStatus = pendingCount > 0
                        ? 'requested'
                        : failedCount > 0
                          ? 'error'
                          : 'completed';
                      const isCollapsed = hasLiveEntries
                        ? collapsedBlocks[blockKey] === true
                        : collapsedBlocks[blockKey] !== false;

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
                            <Box
                              as="span"
                              boxSize="6px"
                              borderRadius="full"
                              bg={getToolStatusColor(headerStatus)}
                              flexShrink={0}
                              boxShadow={livePendingCount > 0 ? `0 0 10px ${getToolStatusColor(headerStatus)}` : 'none'}
                            />
                          </HStack>

                          {/* Expanded: timeline list */}
                          <AnimatePresence initial={false}>
                            {!isCollapsed && (
                              <motion.div key="tg" initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }} transition={{ duration: 0.2, ease: 'easeOut' }} style={{ overflow: 'hidden' }}>
                                <Box ml={3} mt={1} mb={1}>
                                  {block.entries.map((entry, entryInnerIdx) => {
                                    const isLastEntry = entryInnerIdx === block.entries.length - 1;
                                    const isLiveEntry = isLiveToolEntry(entry);
                                    const info = getToolInfo(entry.toolName || entry.title || '');
                                    const ToolIcon = info.Icon;
                                    const label = t(info.labelKey);
                                    const isShell = info.labelKey === 'tools.shell';
                                    const todoSummary = parseTodoSummary(entry.arguments);
                                    const summary = getToolArgSummary(entry);
                                    const files = entry.changedFiles ?? [];

                                    return (
                                      <motion.div
                                        key={entry.id}
                                        initial={isLiveEntry ? { opacity: 0, y: 8 } : false}
                                        animate={{ opacity: 1, y: 0 }}
                                        transition={{ duration: 0.2, ease: 'easeOut' }}
                                      >
                                      <Box position="relative" pl="22px" py="1px">
                                        {/* Vertical line: full height for non-last, half for last (stops at mid-row) */}
                                        <Box
                                          position="absolute"
                                          left="2px"
                                          top="0"
                                          bottom={isLastEntry ? '50%' : '0'}
                                          width="1.5px"
                                          bg="gray.700"
                                        />
                                        {/* Horizontal arm connecting vertical line to content */}
                                        <Box
                                          position="absolute"
                                          left="2px"
                                          top="50%"
                                          width="14px"
                                          height="1.5px"
                                          bg="gray.700"
                                          style={{ transform: 'translateY(-50%)' }}
                                        />
                                        <HStack spacing={2} minH="22px">
                                          <Box color={isLiveEntry && isToolPendingStatus(entry.status) ? '#a5b4fc' : 'gray.500'} flexShrink={0}><ToolIcon size={12} /></Box>
                                          <Text fontSize="xs" color="gray.300" fontWeight="medium" flexShrink={0}>{label}</Text>
                                          {isLiveEntry && isToolPendingStatus(entry.status) && (
                                            <motion.span
                                              animate={{ scale: [0.92, 1.18, 0.92], opacity: [0.45, 1, 0.45] }}
                                              transition={{ duration: 1.1, repeat: Number.POSITIVE_INFINITY, ease: 'easeInOut' }}
                                              style={{
                                                display: 'inline-flex',
                                                width: '6px',
                                                height: '6px',
                                                borderRadius: '9999px',
                                                background: '#a5b4fc',
                                                boxShadow: '0 0 12px rgba(165,180,252,0.7)',
                                                flexShrink: 0,
                                              }}
                                            />
                                          )}
                                          {summary && (
                                            <HStack spacing={2} flex={1} minW={0}>
                                              <Box
                                                boxSize="5px"
                                                borderRadius="full"
                                                bg={getToolStatusColor(entry.status)}
                                                flexShrink={0}
                                                boxShadow={isToolPendingStatus(entry.status) ? `0 0 10px ${getToolStatusColor(entry.status)}` : 'none'}
                                              />
                                              <Text fontSize="xs" color="gray.600" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap" minW={0}>
                                                {summary}
                                              </Text>
                                            </HStack>
                                          )}
                                        </HStack>
                                        {!isShell && todoSummary && (
                                          <Box ml={5} mt={0.5} px={2} py={1.5} bg="gray.900" borderRadius="md">
                                            <Text fontSize="xs" color="gray.400" mb={1}>
                                              {locale.startsWith('ru')
                                                ? `Выполнено ${todoSummary.completedCount} из ${todoSummary.totalCount}`
                                                : `${todoSummary.completedCount} of ${todoSummary.totalCount} completed`}
                                            </Text>
                                            <VStack spacing={1.5} align="stretch">
                                              {todoSummary.items.map((item) => {
                                                const normalizedStatus = item.status.toLowerCase();
                                                const isCompleted = normalizedStatus === 'completed' || normalizedStatus === 'done';
                                                return (
                                                  <HStack key={item.id} spacing={2} align="start">
                                                    <Box mt="2px" color={isCompleted ? 'green.400' : 'gray.500'}>
                                                      <CheckSquare size={12} />
                                                    </Box>
                                                    <Box flex={1} minW={0}>
                                                      <Text fontSize="xs" color={isCompleted ? 'gray.200' : 'gray.300'} textDecoration={isCompleted ? 'line-through' : 'none'} whiteSpace="pre-wrap" wordBreak="break-word">
                                                        {item.content}
                                                      </Text>
                                                      <Text fontSize="10px" color="gray.500">
                                                        {getTodoStatusLabel(locale, item.status)}
                                                      </Text>
                                                    </Box>
                                                  </HStack>
                                                );
                                              })}
                                            </VStack>
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
                                      </motion.div>
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
                      const thinkingDurationMs = block.entries.reduce((total, entry) => total + (entry.thinkingDurationMs ?? 0), 0);
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
                              <Text fontSize="xs" color="gray.500">{getThinkingStatusLabel(locale, thinkingDurationMs)}</Text>
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
                                <Box px={2} ml={2} mt={1}>
                                  {block.entries.map((entry) => (
                                    <Text key={entry.id} fontSize="xs" color="gray.500" whiteSpace="pre-wrap" wordBreak="break-word" lineHeight="1.6">
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
                                  <Text fontSize="xs" color="gray.500">{getThinkingStatusLabel(locale, entry.thinkingDurationMs ?? 0)}</Text>
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
                                    <Box px={2} ml={2} mt={1} mb={text ? 1 : 0}>
                                      <Text fontSize="xs" color="gray.500" whiteSpace="pre-wrap" wordBreak="break-word" lineHeight="1.6">
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
                  <AnimatePresence initial={false}>
                    {isAwaitingAssistantText && (
                      <motion.div
                        key="assistant-processing"
                        initial={{ opacity: 0, y: 4 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, y: 2 }}
                        transition={{ duration: 0.18, ease: 'easeOut' }}
                      >
                        <Flex justify="flex-start" py={2} px={2}>
                          <AnimatedThinkingLabel
                            label={plainThinkingLabel}
                            color="#9ca3af"
                            dotColor="#9ca3af"
                            fontSize="12px"
                          />
                        </Flex>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </VStack>
              </Box>
            </Box>
          ) : (
            <Center h="full">
              <Text fontSize="sm" color="gray.600">No messages in this session</Text>
            </Center>
          )
        ) : (
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
            <Box mt={2} position="relative">
              <Button
                ref={projectPickerButtonRef}
                onClick={() => {
                  setProjectPickerQuery('');
                  if (!projectPickerOpen) {
                    updateProjectPickerPosition();
                  }

                  setProjectPickerOpen((current) => !current);
                }}
                variant="unstyled"
                h="auto"
                px={0}
                display="inline-flex"
                flexDirection="column"
                alignItems="stretch"
                justifyContent="center"
                gap={1}
                color="gray.500"
                _hover={{ color: 'gray.300' }}
                _active={{ color: 'gray.300' }}
              >
                <Text fontSize="2xl" lineHeight="1" fontWeight="medium" color="inherit" textAlign="center">
                  {selectedProjectLabel}
                </Text>
                <Box
                  h="0"
                  borderBottom="1px dashed"
                  borderColor="currentColor"
                  opacity={projectPickerOpen ? 0.9 : 0.65}
                />
              </Button>

            </Box>
            <Portal>
              <AnimatePresence>
                {projectPickerOpen && (
                  <motion.div
                    initial={{ opacity: 0, y: 8, scale: 0.98 }}
                    animate={{ opacity: 1, y: 0, scale: 1 }}
                    exit={{ opacity: 0, y: 8, scale: 0.98 }}
                    transition={{ duration: 0.16, ease: 'easeOut' }}
                    style={{
                      position: 'fixed',
                      top: `${projectPickerPosition.top}px`,
                      left: `${projectPickerPosition.left}px`,
                      width: `${projectPickerPosition.width}px`,
                      zIndex: 2000,
                    }}
                  >
                    <Box
                      ref={projectPickerMenuRef}
                      display="flex"
                      flexDirection="column"
                      maxH={`${projectPickerPosition.maxHeight}px`}
                      bg="gray.800"
                      border="1px solid"
                      borderColor="gray.700"
                      borderRadius="2xl"
                      shadow="2xl"
                      overflow="hidden"
                    >
                      <HStack px={3} py={1.5} spacing={3} minH="38px">
                        <Search size={13} color="#9494a2" />
                        <Input
                          value={projectPickerQuery}
                          onChange={(e) => setProjectPickerQuery(e.target.value)}
                          placeholder={getProjectPickerSearchPlaceholder(locale)}
                          bg="transparent"
                          border="none"
                          color="white"
                          fontSize="sm"
                          fontWeight="normal"
                          h="20px"
                          minH="20px"
                          p={0}
                          _placeholder={{ color: 'gray.500' }}
                          _focusVisible={{ boxShadow: 'none' }}
                        />
                      </HStack>

                      <Box borderTop="1px solid" borderColor="gray.700" />

                      <Box
                        h={`${projectListHeight}px`}
                        overflowY="auto"
                        px={2}
                        py={2}
                        sx={{
                          '&::-webkit-scrollbar': { width: '8px' },
                          '&::-webkit-scrollbar-track': { background: 'rgba(255,255,255,0.04)' },
                          '&::-webkit-scrollbar-thumb': { background: '#5b5b67', borderRadius: '999px' },
                          '&::-webkit-scrollbar-thumb:hover': { background: '#72727f' },
                        }}
                      >
                        <VStack spacing={1} align="stretch">
                          {filteredProjectOptions.length > 0 ? (
                            filteredProjectOptions.map((project) => (
                              <Button
                                key={project.path}
                                variant="ghost"
                                justifyContent="space-between"
                                h="40px"
                                px={3}
                                borderRadius="xl"
                                color="gray.200"
                                fontWeight="normal"
                                _hover={{ bg: 'gray.700' }}
                                onClick={() => {
                                  setSelectedProjectMode('project');
                                  setSelectedProjectPath(project.path);
                                  setProjectPickerOpen(false);
                                  setProjectPickerQuery('');
                                }}
                              >
                                <HStack spacing={3} minW={0}>
                                  <FolderOpen size={14} />
                                  <Text fontSize="sm" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap">
                                    {project.name}
                                  </Text>
                                </HStack>
                                {selectedProjectMode === 'project' && normalizePathKey(project.path) === normalizePathKey(selectedProjectPath) && (
                                  <Check size={16} color={ACCENT} />
                                )}
                              </Button>
                            ))
                          ) : (
                            <Center py={4}>
                              <Text fontSize="sm" color="gray.500">
                                {getNoProjectsLabel(locale)}
                              </Text>
                            </Center>
                          )}
                        </VStack>
                      </Box>

                      <Box borderTop="1px solid" borderColor="gray.700" />

                      <VStack spacing={1} align="stretch" px={2} py={2}>
                        <Button
                          variant="ghost"
                          justifyContent="space-between"
                          h="40px"
                          px={3}
                          borderRadius="xl"
                          color="gray.200"
                          fontWeight="normal"
                          _hover={{ bg: 'gray.700' }}
                          onClick={() => {
                            setSelectedProjectMode('no-project');
                            setProjectPickerOpen(false);
                            setProjectPickerQuery('');
                          }}
                        >
                          <HStack spacing={3} minW={0}>
                            <MessageCircle size={14} />
                            <Text fontSize="sm" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap">
                              {getContinueWithoutProjectLabel(locale)}
                            </Text>
                          </HStack>
                          {selectedProjectMode === 'no-project' && <Check size={16} color={ACCENT} />}
                        </Button>

                        <Button
                          variant="ghost"
                          justifyContent="space-between"
                          h="40px"
                          px={3}
                          borderRadius="xl"
                          color="gray.200"
                          fontWeight="normal"
                          _hover={{ bg: 'gray.700' }}
                          onClick={async () => {
                            const result = await window.qwenDesktop?.selectProjectDirectory?.();
                            if (!result || result.cancelled || !result.selectedPath) {
                              return;
                            }

                            setCustomProjectPaths((current) =>
                              current.some((path) => normalizePathKey(path) === normalizePathKey(result.selectedPath))
                                ? current
                                : [result.selectedPath, ...current],
                            );
                            setSelectedProjectMode('project');
                            setSelectedProjectPath(result.selectedPath);
                            setProjectPickerOpen(false);
                            setProjectPickerQuery('');
                          }}
                        >
                          <HStack spacing={3} minW={0}>
                            <FilePlus size={14} />
                            <Text fontSize="sm" overflow="hidden" textOverflow="ellipsis" whiteSpace="nowrap">
                              {getAddProjectLabel(locale)}
                            </Text>
                          </HStack>
                          <Box boxSize="16px" flexShrink={0} />
                        </Button>
                      </VStack>
                    </Box>
                  </motion.div>
                )}
              </AnimatePresence>
            </Portal>
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

        <Box mx="auto" w="full" maxW={CHAT_MAX_WIDTH}>
          <Box minH="24px" px={2} mb={2} display="flex" alignItems="center">
            <AnimatePresence mode="wait" initial={false}>
              {isAwaitingAssistantText && (
                <motion.div
                  key={loadingPhrase || 'loading-phrase'}
                  initial={{ opacity: 0, y: 4 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -4 }}
                  transition={{ duration: 0.18, ease: 'easeOut' }}
                  style={{ width: '100%' }}
                >
                  <Text
                    fontSize="xs"
                    color="gray.500"
                    pl={1}
                    whiteSpace="nowrap"
                    overflow="hidden"
                    textOverflow="ellipsis"
                  >
                    {loadingPhrase || defaultThinkingLabel}
                  </Text>
                </motion.div>
              )}
            </AnimatePresence>
          </Box>

          <Box
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
                                borderRadius="2xl"
                                onClick={() => { setMode(m.value); setModeDropdownOpen(false); }}
                                bg="transparent"
                                _hover={{ bg: 'gray.900', borderRadius: '2xl' }}
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
                        <Text fontSize="xs" color={contextPercent >= 70 ? 'orange.400' : 'gray.500'} mt={1}>
                          {t('chat.contextCompression')}
                        </Text>
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
                isDisabled={!prompt.trim() || isComposerBusy || (!selectedSession && !selectedProjectWorkingDirectory)}
                isLoading={isPendingSelectedSession}
                onClick={handleSubmit}
                borderRadius="full"
                w="36px"
                h="36px"
                minW="36px"
              />
            </HStack>
          </HStack>
        </Box>
        </Box>

        {/* Disclaimer — always visible */}
        <Text mx="auto" mt={2} px={2} fontSize="11px" color="gray.600" textAlign="center" maxW={CHAT_MAX_WIDTH}>
          {t('chat.disclaimer')}
        </Text>
      </Box>
    </VStack>
  );
}
