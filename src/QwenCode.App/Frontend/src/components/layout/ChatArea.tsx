import { useEffect, useRef, useState, useCallback, useMemo, Children, isValidElement, type ReactNode } from 'react';
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
  Square,
  Copy,
  FileSpreadsheet,
} from 'lucide-react';
import { AnimatePresence, motion } from 'framer-motion';
import { Highlight, themes, type Language } from 'prism-react-renderer';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { AGENT_MODES } from '@/types/ui';
import type { AgentMode } from '@/types/ui';
import { useBootstrap } from '@/hooks/useBootstrap';
import { useTranslation } from 'react-i18next';
import type { DesktopSessionDetail, DesktopSessionEntry, SessionPreview } from '@/types/desktop';
import qwenLogo from '@/assets/qwen-logo.svg';
import type { SessionNavigationMode } from './sessionNavigation';

interface ChatAreaProps {
  onToggleSidebar?: () => void;
  isSidebarOpen: boolean;
  selectedSessionId?: string;
  sidebarMode?: SessionNavigationMode;
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
  if (name.includes('task_')) return { labelKey: 'tools.todo', Icon: CheckSquare };
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

interface TaskItemSummary {
  id: string;
  subject: string;
  status: string;
  description: string;
  owner: string;
  blockedBy: string[];
}

interface TaskSummary {
  items: TaskItemSummary[];
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

function normalizeTaskStatus(status: string): string {
  return status.trim().toLowerCase().replace(/-/g, '_');
}

function finalizeTaskSummary(items: TaskItemSummary[]): TaskSummary | null {
  const normalizedItems = items.filter((item) => item.subject.trim().length > 0);
  if (normalizedItems.length === 0) {
    return null;
  }

  return {
    items: normalizedItems,
    completedCount: normalizedItems.filter((item) => normalizeTaskStatus(item.status) === 'completed').length,
    totalCount: normalizedItems.length,
  };
}

function parseTaskListBody(body: string): TaskSummary | null {
  if (!body || /no tasks found\./i.test(body)) {
    return null;
  }

  const items = body
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.startsWith('- #'))
    .map((line) => {
      const match = /^- #(?<id>[^\s]+)\s+\[(?<status>[^\]]+)\]\s+(?<subject>.*?)(?:\s+\((?<owner>[^)]+)\))?(?:\s+blocked by \[(?<blocked>[^\]]+)\])?$/i.exec(line);
      if (!match?.groups) {
        return null;
      }

      return {
        id: match.groups.id,
        subject: match.groups.subject.trim(),
        status: match.groups.status.trim(),
        description: '',
        owner: match.groups.owner?.trim() ?? '',
        blockedBy: (match.groups.blocked ?? '')
          .split(',')
          .map((value) => value.trim())
          .filter(Boolean),
      } satisfies TaskItemSummary;
    })
    .filter((item): item is TaskItemSummary => item !== null);

  return finalizeTaskSummary(items);
}

function parseTaskDetailBody(body: string): TaskSummary | null {
  if (!body || /task not found\./i.test(body)) {
    return null;
  }

  const lines = body
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  const titleLine = lines.find((line) => /^task #/i.test(line));
  if (!titleLine) {
    return null;
  }

  const titleMatch = /^Task #(?<id>[^:]+):\s*(?<subject>.+)$/i.exec(titleLine);
  if (!titleMatch?.groups) {
    return null;
  }

  const fields = new Map<string, string>();
  for (const line of lines.slice(1)) {
    const separatorIndex = line.indexOf(':');
    if (separatorIndex <= 0) {
      continue;
    }

    const key = line.slice(0, separatorIndex).trim().toLowerCase();
    const value = line.slice(separatorIndex + 1).trim();
    if (value) {
      fields.set(key, value);
    }
  }

  return finalizeTaskSummary([
    {
      id: titleMatch.groups.id.trim(),
      subject: titleMatch.groups.subject.trim(),
      status: fields.get('status') ?? 'pending',
      description: fields.get('description') ?? '',
      owner: fields.get('owner') ?? '',
      blockedBy: (fields.get('blocked by') ?? '')
        .split(',')
        .map((value) => value.trim())
        .filter(Boolean),
    },
  ]);
}

function parseTaskMutationBody(body: string): TaskSummary | null {
  if (!body || /task not found\./i.test(body)) {
    return null;
  }

  const match = /^(?:Created|Updated|Stopped) task #(?<id>[^:]+):\s*(?<subject>.+?)\s+\[(?<status>[^\]]+)\]\./i.exec(body.trim());
  if (!match?.groups) {
    return null;
  }

  return finalizeTaskSummary([
    {
      id: match.groups.id.trim(),
      subject: match.groups.subject.trim(),
      status: match.groups.status.trim(),
      description: '',
      owner: '',
      blockedBy: [],
    },
  ]);
}

function parseTaskSummary(entry: DesktopSessionEntry): TaskSummary | null {
  const toolKey = (entry.toolName || entry.title || '').toLowerCase();
  if (!toolKey.includes('task_')) {
    return null;
  }

  if (toolKey.includes('task_list')) {
    return parseTaskListBody(entry.body || '');
  }

  if (toolKey.includes('task_get')) {
    return parseTaskDetailBody(entry.body || '');
  }

  if (
    toolKey.includes('task_create') ||
    toolKey.includes('task_update') ||
    toolKey.includes('task_stop')
  ) {
    const fromBody = parseTaskMutationBody(entry.body || '');
    if (fromBody) {
      return fromBody;
    }
  }

  const parsed = parseObjectArguments(entry.arguments);
  if (!parsed) {
    return null;
  }

  const subject = typeof parsed.subject === 'string' ? parsed.subject.trim() : '';
  const description = typeof parsed.description === 'string' ? parsed.description.trim() : '';
  const taskId = typeof parsed.task_id === 'string'
    ? parsed.task_id.trim()
    : typeof parsed.taskId === 'string'
      ? parsed.taskId.trim()
      : '';
  const status = typeof parsed.status === 'string' ? parsed.status.trim() : 'pending';
  const owner = typeof parsed.owner === 'string' ? parsed.owner.trim() : '';

  if (!subject && !taskId) {
    return null;
  }

  return finalizeTaskSummary([
    {
      id: taskId || 'task',
      subject: subject || `#${taskId}`,
      status,
      description,
      owner,
      blockedBy: [],
    },
  ]);
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
  title: string | null,
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

const ASSISTANT_MARKDOWN_SX = {
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
  'pre': { my: '0.75em' },
  'table': { width: '100%', borderCollapse: 'separate', borderSpacing: 0 },
  'thead tr': { bg: 'rgba(255,255,255,0.05)' },
  'tbody tr': { bg: 'transparent' },
  'tbody tr:hover': { bg: 'rgba(255,255,255,0.02)' },
  'th,td': { borderBottom: '1px solid', borderColor: 'gray.700', px: 4, py: 3, textAlign: 'left', verticalAlign: 'top' },
  'th': { color: 'white', fontWeight: 'semibold', fontSize: 'sm' },
  'td': { color: 'gray.200', fontSize: 'sm' },
  'tbody tr:last-of-type td': { borderBottom: 'none' },
  'hr': { borderColor: 'gray.700', my: '1em' },
} as const;

const INLINE_CODE_COLOR = '#8583f6';
const INLINE_CODE_BACKGROUND = 'rgba(133,131,246,0.12)';
const CODE_THEME = themes.vsDark;
const SUPPORTED_HIGHLIGHT_LANGUAGES = new Set<Language>([
  'bash',
  'c',
  'cpp',
  'csharp',
  'css',
  'diff',
  'go',
  'java',
  'javascript',
  'json',
  'jsx',
  'kotlin',
  'markdown',
  'markup',
  'objectivec',
  'powershell',
  'python',
  'regex',
  'rust',
  'sql',
  'swift',
  'tsx',
  'typescript',
  'yaml',
]);

function flattenReactText(node: ReactNode): string {
  return Children.toArray(node)
    .map((child) => {
      if (typeof child === 'string' || typeof child === 'number') {
        return String(child);
      }

      if (isValidElement(child)) {
        const props = child.props as { children?: ReactNode };
        return flattenReactText(props.children);
      }

      return '';
    })
    .join('');
}

function normalizeCodeLanguage(language: string): Language | null {
  const normalized = language.trim().toLowerCase();
  if (!normalized) {
    return null;
  }

  const aliases: Record<string, Language> = {
    cs: 'csharp',
    'c#': 'csharp',
    sh: 'bash',
    shell: 'bash',
    zsh: 'bash',
    ps1: 'powershell',
    pwsh: 'powershell',
    js: 'javascript',
    mjs: 'javascript',
    cjs: 'javascript',
    ts: 'typescript',
    yml: 'yaml',
    html: 'markup',
    xml: 'markup',
    svg: 'markup',
    md: 'markdown',
    py: 'python',
    rs: 'rust',
  };

  const resolved = aliases[normalized] ?? (normalized as Language);
  return SUPPORTED_HIGHLIGHT_LANGUAGES.has(resolved) ? resolved : null;
}

function getMarkdownNodeTextContent(node: unknown): string {
  if (!node || typeof node !== 'object') {
    return '';
  }

  const candidate = node as { value?: unknown; children?: unknown[] };
  if (typeof candidate.value === 'string') {
    return candidate.value;
  }

  if (Array.isArray(candidate.children)) {
    return candidate.children.map((child) => getMarkdownNodeTextContent(child)).join('');
  }

  return '';
}

function extractMarkdownTableRows(node: unknown): string[][] {
  const rows: string[][] = [];

  const visit = (candidate: unknown) => {
    if (!candidate || typeof candidate !== 'object') {
      return;
    }

    const element = candidate as { tagName?: string; children?: unknown[] };
    if (element.tagName === 'tr') {
      const cells = (element.children ?? [])
        .filter((child): child is { tagName?: string } => !!child && typeof child === 'object')
        .filter((child) => child.tagName === 'th' || child.tagName === 'td')
        .map((child) => getMarkdownNodeTextContent(child).trim());

      if (cells.length > 0) {
        rows.push(cells);
      }
    }

    for (const child of element.children ?? []) {
      visit(child);
    }
  };

  visit(node);
  return rows;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll('\'', '&#39;');
}

function buildCsvContent(rows: string[][]): string {
  return rows
    .map((row) => row.map((cell) => `"${cell.replaceAll('"', '""')}"`).join(','))
    .join('\r\n');
}

function buildExcelContent(rows: string[][]): string {
  const [headerRow = [], ...bodyRows] = rows;
  const thead = headerRow.length > 0
    ? `<thead><tr>${headerRow.map((cell) => `<th>${escapeHtml(cell)}</th>`).join('')}</tr></thead>`
    : '';
  const tbody = `<tbody>${bodyRows
    .map((row) => `<tr>${row.map((cell) => `<td>${escapeHtml(cell)}</td>`).join('')}</tr>`)
    .join('')}</tbody>`;

  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
</head>
<body>
  <table>${thead}${tbody}</table>
</body>
</html>`;
}

function downloadTextContent(filename: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

async function copyTextToClipboard(text: string) {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', 'true');
  textarea.style.position = 'absolute';
  textarea.style.left = '-9999px';
  document.body.appendChild(textarea);
  textarea.select();
  document.execCommand('copy');
  document.body.removeChild(textarea);
}

function MarkdownInlineCode({ children }: { children?: ReactNode }) {
  return (
    <Box
      as="code"
      display="inline-flex"
      alignItems="center"
      px="0.38em"
      py="0.14em"
      borderRadius="8px"
      fontFamily="mono"
      fontSize="0.85em"
      lineHeight="1.4"
      color={INLINE_CODE_COLOR}
      bg={INLINE_CODE_BACKGROUND}
      border="1px solid rgba(133,131,246,0.14)"
    >
      {flattenReactText(children)}
    </Box>
  );
}

function MarkdownCodeBlock({
  className,
  children,
}: {
  className?: string;
  children?: ReactNode;
}) {
  const [copied, setCopied] = useState(false);
  const code = flattenReactText(children).replace(/\n$/, '');
  const language = className?.match(/language-([\w-]+)/)?.[1] ?? 'text';
  const highlightLanguage = normalizeCodeLanguage(language);
  const lines = code.split('\n');

  const handleCopy = useCallback(async () => {
    if (!code.trim()) {
      return;
    }

    try {
      await copyTextToClipboard(code);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch (error) {
      console.error('Failed to copy code block:', error);
    }
  }, [code]);

  return (
    <Box
      my={4}
      border="1px solid"
      borderColor="rgba(255,255,255,0.08)"
      borderRadius="20px"
      overflow="hidden"
      bg="#24242b"
      boxShadow="0 16px 48px -32px rgba(0,0,0,0.9)"
    >
      <HStack
        justify="space-between"
        align="center"
        px={4}
        py={0}
        h="30px"
        bg="rgba(255,255,255,0.04)"
        borderBottom="1px solid"
        borderColor="rgba(255,255,255,0.06)"
      >
        <Box
          display="flex"
          alignItems="center"
          h="full"
          minW={0}
        >
          <Text
            fontSize="11px"
            fontWeight="semibold"
            fontFamily="mono"
            color="gray.100"
            textTransform="lowercase"
            letterSpacing="0.02em"
            lineHeight="1"
            mb={0}
          >
            {language}
          </Text>
        </Box>
        <IconButton
          aria-label={copied ? 'Copied' : 'Copy code'}
          size="xs"
          variant="ghost"
          color={copied ? '#d8d7ff' : 'gray.300'}
          icon={copied ? <Check size={13} /> : <Copy size={13} />}
          minW="20px"
          w="20px"
          h="20px"
          _hover={{ bg: 'rgba(255,255,255,0.06)', color: 'white' }}
          onClick={() => { void handleCopy(); }}
        />
      </HStack>

      <Box overflowX="auto" bg="#24242b" pt={2} pb={1}>
        {highlightLanguage ? (
          <Highlight theme={CODE_THEME} code={code} language={highlightLanguage}>
            {({ className: highlightClassName, style, tokens, getLineProps, getTokenProps }) => (
              <Box
                as="pre"
                className={highlightClassName}
                m={0}
                px={0}
                py={0}
                bg="transparent"
                style={{ ...style, background: 'transparent', margin: 0 }}
              >
                {tokens.map((lineTokens, index) => {
                  const isLastEmptyLine = index === tokens.length - 1 && lineTokens.length === 1 && lineTokens[0]?.empty;
                  if (isLastEmptyLine) {
                    return null;
                  }

                  return (
                    <HStack
                      key={`code-line-${index + 1}`}
                      {...getLineProps({ line: lineTokens })}
                      align="stretch"
                      spacing={0}
                      fontFamily="mono"
                      fontSize="13px"
                      lineHeight="1.9"
                      minW="fit-content"
                      bg="transparent"
                    >
                      <Box
                        w="52px"
                        flexShrink={0}
                        px={4}
                        py={0}
                        textAlign="right"
                        color="gray.500"
                        borderRight="1px solid"
                        borderColor="rgba(255,255,255,0.06)"
                        userSelect="none"
                      >
                        {index + 1}
                      </Box>
                      <Box
                        px={4}
                        py={0}
                        whiteSpace="pre"
                        color="gray.100"
                      >
                        {lineTokens.length > 0
                          ? lineTokens.map((token, tokenIndex) => (
                            <Box
                              as="span"
                              key={`code-token-${index + 1}-${tokenIndex}`}
                              {...getTokenProps({ token })}
                            />
                          ))
                          : ' '}
                      </Box>
                    </HStack>
                  );
                })}
              </Box>
            )}
          </Highlight>
        ) : (
          <Box as="pre" m={0} px={0} py={0} bg="transparent">
            {lines.map((line, index) => (
              <HStack
                key={`code-line-${index + 1}`}
                align="stretch"
                spacing={0}
                fontFamily="mono"
                fontSize="13px"
                lineHeight="1.9"
                minW="fit-content"
              >
                <Box
                  w="52px"
                  flexShrink={0}
                  px={4}
                  py={0}
                  textAlign="right"
                  color="gray.500"
                  borderRight="1px solid"
                  borderColor="rgba(255,255,255,0.06)"
                  userSelect="none"
                >
                  {index + 1}
                </Box>
                <Box
                  px={4}
                  py={0}
                  whiteSpace="pre"
                  color="gray.100"
                >
                  {line || ' '}
                </Box>
              </HStack>
            ))}
          </Box>
        )}
      </Box>
    </Box>
  );
}

function MarkdownPre({ children }: { children?: ReactNode }) {
  const firstChild = Children.toArray(children)[0];
  if (!isValidElement(firstChild)) {
    return <>{children}</>;
  }

  const childProps = firstChild.props as { className?: string; children?: ReactNode };
  return (
    <MarkdownCodeBlock className={childProps.className}>
      {childProps.children}
    </MarkdownCodeBlock>
  );
}

function MarkdownCode({ children }: { children?: ReactNode }) {
  return <MarkdownInlineCode>{children}</MarkdownInlineCode>;
}

function MarkdownTable({
  node,
  children,
}: {
  node?: unknown;
  children?: ReactNode;
}) {
  const rows = useMemo(() => extractMarkdownTableRows(node), [node]);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement | null>(null);

  const exportAsCsv = useCallback(() => {
    if (rows.length === 0) {
      return;
    }

    downloadTextContent(
      `table-${Date.now()}.csv`,
      buildCsvContent(rows),
      'text/csv;charset=utf-8',
    );
    setMenuOpen(false);
  }, [rows]);

  const exportAsExcel = useCallback(() => {
    if (rows.length === 0) {
      return;
    }

    downloadTextContent(
      `table-${Date.now()}.xls`,
      buildExcelContent(rows),
      'application/vnd.ms-excel;charset=utf-8',
    );
    setMenuOpen(false);
  }, [rows]);

  useEffect(() => {
    if (!menuOpen) {
      return;
    }

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node | null;
      if (!target) {
        return;
      }

      if (menuRef.current?.contains(target)) {
        return;
      }

      setMenuOpen(false);
    };

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, [menuOpen]);

  return (
    <Box my={4} position="relative">
      <Box
        overflowX="auto"
        border="1px solid"
        borderColor="rgba(255,255,255,0.08)"
        borderRadius="20px"
        bg="#24242b"
        boxShadow="0 12px 40px -28px rgba(0,0,0,0.9)"
      >
        <Box as="table" w="full">
          {children}
        </Box>
      </Box>

      <Box
        position="absolute"
        top="24px"
        right={2}
        transform="translateY(-50%)"
        zIndex={1}
        ref={menuRef}
      >
        <IconButton
          aria-label="Export table"
          size="xs"
          variant="ghost"
          icon={<Download size={13} />}
          minW="28px"
          w="28px"
          h="28px"
          color="gray.300"
          _hover={{ bg: 'rgba(255,255,255,0.06)', color: 'white' }}
          onClick={() => setMenuOpen((current) => !current)}
        />

        <AnimatePresence>
          {menuOpen && (
            <motion.div
              initial={{ opacity: 0, y: -4, scale: 0.96 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: -4, scale: 0.96 }}
              transition={{ duration: 0.14, ease: 'easeOut' }}
              style={{
                position: 'absolute',
                top: 'calc(100% + 6px)',
                right: 0,
              }}
            >
              <Box
                minW="112px"
                bg="#2f2f37"
                border="1px solid"
                borderColor="rgba(255,255,255,0.08)"
                borderRadius="14px"
                boxShadow="0 18px 42px -26px rgba(0,0,0,0.95)"
                p={1}
              >
                <Button
                  size="sm"
                  variant="ghost"
                  justifyContent="flex-start"
                  w="full"
                  h="30px"
                  px={2.5}
                  borderRadius="10px"
                  color="gray.200"
                  fontWeight="normal"
                  leftIcon={<Download size={12} />}
                  _hover={{ bg: 'rgba(255,255,255,0.06)', color: 'white' }}
                  onClick={exportAsCsv}
                >
                  CSV
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  justifyContent="flex-start"
                  w="full"
                  h="30px"
                  px={2.5}
                  borderRadius="10px"
                  color="gray.200"
                  fontWeight="normal"
                  leftIcon={<FileSpreadsheet size={12} />}
                  _hover={{ bg: 'rgba(255,255,255,0.06)', color: 'white' }}
                  onClick={exportAsExcel}
                >
                  Excel
                </Button>
              </Box>
            </motion.div>
          )}
        </AnimatePresence>
      </Box>
    </Box>
  );
}

function AssistantMarkdownBody({ text }: { text: string }) {
  return (
    <Box
      color="gray.100"
      fontSize="sm"
      lineHeight="1.75"
      sx={ASSISTANT_MARKDOWN_SX}
    >
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          pre: MarkdownPre,
          code: MarkdownCode,
          table: MarkdownTable,
        }}
      >
        {text}
      </ReactMarkdown>
    </Box>
  );
}

function StreamingAssistantBody({
  text,
  isStreaming,
}: {
  text: string;
  isStreaming: boolean;
}) {
  void isStreaming;
  return <AssistantMarkdownBody text={text} />;
}

type PendingApprovalDecision = 'allow-once' | 'always-allow' | 'deny';

interface PendingApprovalPresentation {
  pendingEntry: DesktopSessionEntry;
  suggestedRule: string;
  signature: string;
}

function isApprovalPlaceholderText(text: string): boolean {
  return /tool '([^']+)' is waiting for approval(?:[^.]*)\.?$/i.test(text.trim());
}

function splitFirstCommandSegment(command: string): string {
  let inSingleQuote = false;
  let inDoubleQuote = false;

  for (let index = 0; index < command.length; index++) {
    const character = command[index];

    if (character === '\'' && !inDoubleQuote) {
      inSingleQuote = !inSingleQuote;
      continue;
    }

    if (character === '"' && !inSingleQuote) {
      inDoubleQuote = !inDoubleQuote;
      continue;
    }

    if (inSingleQuote || inDoubleQuote) {
      continue;
    }

    for (const token of ['&&', '||', ';;', '|&', '|', ';']) {
      if (command.slice(index, index + token.length) === token) {
        return command.slice(0, index).trim();
      }
    }
  }

  return command.trim();
}

function extractExecutableFromCommandSegment(segment: string): string {
  const trimmed = segment.trim();
  if (!trimmed) {
    return '';
  }

  let token = trimmed;
  if (trimmed[0] === '"' || trimmed[0] === '\'') {
    const quote = trimmed[0];
    const endQuote = trimmed.indexOf(quote, 1);
    token = endQuote > 0 ? trimmed.slice(1, endQuote) : trimmed.slice(1);
  } else {
    const whitespaceIndex = trimmed.search(/\s/);
    token = whitespaceIndex < 0 ? trimmed : trimmed.slice(0, whitespaceIndex);
  }

  if (!token) {
    return '';
  }

  if (token.includes('/') || token.includes('\\')) {
    const lastSlash = Math.max(token.lastIndexOf('/'), token.lastIndexOf('\\'));
    token = token.slice(lastSlash + 1);
    token = token.replace(/\.(cmd|exe|bat|ps1)$/i, '');
  }

  return token.trim();
}

function stripMatchingQuotes(value: string): string {
  const trimmed = value.trim();
  if (trimmed.length >= 2) {
    const first = trimmed[0];
    const last = trimmed[trimmed.length - 1];
    if ((first === '"' && last === '"') || (first === '\'' && last === '\'')) {
      return trimmed.slice(1, -1);
    }
  }

  return trimmed;
}

function tokenizeShellSegment(segment: string): string[] {
  const tokens: string[] = [];
  let current = '';
  let inSingleQuote = false;
  let inDoubleQuote = false;
  let escaped = false;

  for (const character of segment) {
    if (escaped) {
      current += character;
      escaped = false;
      continue;
    }

    if (character === '\\' && !inSingleQuote) {
      escaped = true;
      continue;
    }

    if (character === '\'' && !inDoubleQuote) {
      inSingleQuote = !inSingleQuote;
      continue;
    }

    if (character === '"' && !inSingleQuote) {
      inDoubleQuote = !inDoubleQuote;
      continue;
    }

    if (!inSingleQuote && !inDoubleQuote && /\s/.test(character)) {
      if (current) {
        tokens.push(current);
        current = '';
      }

      continue;
    }

    current += character;
  }

  if (current) {
    tokens.push(current);
  }

  return tokens;
}

function extractFirstHttpUrl(tokens: string[]): string {
  for (const token of tokens) {
    const candidate = stripMatchingQuotes(token);
    if (/^https?:\/\//i.test(candidate)) {
      return candidate;
    }
  }

  return '';
}

function extractPrimaryShellPath(tokens: string[]): string {
  for (const token of tokens.slice(1)) {
    const candidate = stripMatchingQuotes(token);
    if (!candidate || candidate === '--') {
      continue;
    }

    if (candidate.startsWith('-')) {
      continue;
    }

    if (/^https?:\/\//i.test(candidate)) {
      continue;
    }

    return candidate;
  }

  return '';
}

function suggestVirtualShellRule(command: string, projectRoot: string): string {
  const firstSegment = splitFirstCommandSegment(command);
  const executable = extractExecutableFromCommandSegment(firstSegment).toLowerCase();
  if (!executable) {
    return '';
  }

  const tokens = tokenizeShellSegment(firstSegment);
  if (tokens.length === 0) {
    return '';
  }

  if (['curl', 'wget', 'fetch'].includes(executable)) {
    const url = extractFirstHttpUrl(tokens);
    if (!url) {
      return '';
    }

    try {
      const host = new URL(url).hostname.trim();
      return host ? `WebFetch(domain:${host})` : '';
    } catch {
      return '';
    }
  }

  if (['cat', 'tac', 'nl', 'head', 'tail', 'less', 'more', 'type'].includes(executable)) {
    const filePath = extractPrimaryShellPath(tokens);
    return filePath ? `Read(${resolveApprovalRulePath(filePath, projectRoot)})` : '';
  }

  if (['ls', 'dir', 'tree', 'find'].includes(executable)) {
    const directoryPath = extractPrimaryShellPath(tokens) || '.';
    return `Read(${resolveApprovalRulePath(directoryPath, projectRoot).replace(/\/?$/, '/**')})`;
  }

  return '';
}

function resolveApprovalRulePath(candidatePath: string, projectRoot: string): string {
  if (!candidatePath) {
    return '';
  }

  const normalizedProjectRoot = normalizePathKey(projectRoot);
  const normalizedCandidate = normalizePathKey(candidatePath);
  const resolvedPath = normalizedCandidate.startsWith(normalizedProjectRoot)
    ? normalizedCandidate
    : normalizePathKey(joinDesktopPath(projectRoot || '', candidatePath));

  if (normalizedProjectRoot && (resolvedPath === normalizedProjectRoot || resolvedPath.startsWith(`${normalizedProjectRoot}/`))) {
    const relative = resolvedPath.slice(normalizedProjectRoot.length).replace(/^\/+/, '');
    return relative ? `/${relative}` : '/';
  }

  return `//${resolvedPath}`;
}

function suggestProjectAllowRule(entry: DesktopSessionEntry, projectRoot: string): string {
  const parsed = parseObjectArguments(entry.arguments) ?? {};
  const toolName = (entry.toolName || '').trim().toLowerCase();

  if (toolName.includes('shell') || toolName.includes('bash') || toolName.includes('run_command') || toolName.includes('terminal') || toolName.includes('execute')) {
    const command = typeof parsed.command === 'string' ? parsed.command.trim() : '';
    if (!command) {
      return 'Bash';
    }

    const virtualRule = suggestVirtualShellRule(command, projectRoot);
    if (virtualRule) {
      return virtualRule;
    }

    const firstSegment = splitFirstCommandSegment(command);
    const executable = extractExecutableFromCommandSegment(firstSegment);
    if (!executable) {
      return `Bash(${firstSegment || command})`;
    }

    return firstSegment === executable ? `Bash(${executable})` : `Bash(${executable} *)`;
  }

  const rawPath =
    (typeof parsed.file_path === 'string' && parsed.file_path.trim()) ||
    (typeof parsed.path === 'string' && parsed.path.trim()) ||
    (typeof parsed.directory === 'string' && parsed.directory.trim()) ||
    '';

  if (toolName.includes('edit')) {
    return rawPath ? `Edit(${resolveApprovalRulePath(rawPath, projectRoot)})` : 'Edit';
  }

  if (toolName.includes('write') || toolName.includes('create_file')) {
    return rawPath ? `Write(${resolveApprovalRulePath(rawPath, projectRoot)})` : 'Write';
  }

  if (
    toolName.includes('read') ||
    toolName.includes('grep') ||
    toolName.includes('search_files') ||
    toolName.includes('glob') ||
    toolName.includes('find_files')
  ) {
    return rawPath ? `Read(${resolveApprovalRulePath(rawPath, projectRoot)})` : 'Read';
  }

  if (toolName.includes('list') || toolName.includes('directory')) {
    const pathRule = rawPath ? resolveApprovalRulePath(rawPath, projectRoot).replace(/\/?$/, '/**') : '';
    return pathRule ? `Read(${pathRule})` : 'Read';
  }

  if (toolName.includes('mcp')) {
    const serverName = typeof parsed.server_name === 'string' ? parsed.server_name.trim() : '';
    const tool = typeof parsed.tool_name === 'string' ? parsed.tool_name.trim() : '';
    return serverName && tool ? `mcp__${serverName}__${tool}` : 'mcp-tool';
  }

  if (toolName.includes('agent')) {
    const agentType = typeof parsed.agent_type === 'string' ? parsed.agent_type.trim() : '';
    return agentType ? `Agent(${agentType})` : 'Agent';
  }

  if (toolName.includes('skill')) {
    const skillName = typeof parsed.skill_name === 'string' ? parsed.skill_name.trim() : '';
    return skillName ? `Skill(${skillName})` : 'Skill';
  }

  return entry.toolName || 'Tool';
}

function getPendingApprovalSignature(entry: DesktopSessionEntry, projectRoot: string): string {
  const rule = suggestProjectAllowRule(entry, projectRoot).trim();
  return rule || (entry.toolName || '').trim().toLowerCase() || entry.id;
}

function buildPendingApprovalPresentations(
  entries: DesktopSessionEntry[],
  projectRoot: string,
): PendingApprovalPresentation[] {
  const presentations: PendingApprovalPresentation[] = [];

  for (const entry of entries) {
    const isPendingApproval =
      entry.type === 'tool' &&
      entry.status.trim().toLowerCase() === 'approval-required' &&
      !entry.resolutionStatus?.trim();

    if (!isPendingApproval) {
      continue;
    }

    presentations.push({
      pendingEntry: entry,
      suggestedRule: suggestProjectAllowRule(entry, projectRoot),
      signature: getPendingApprovalSignature(entry, projectRoot),
    });
  }

  return presentations;
}

function getApprovalCardTitle(locale: string): string {
  return locale.startsWith('ru') ? 'Требуется подтверждение' : 'Approval required';
}

function getApprovalAllowOnceLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Разрешить один раз' : 'Allow once';
}

function getApprovalAlwaysAllowLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Всегда разрешать' : 'Always allow';
}

function getApprovalFeedbackLabel(locale: string): string {
  return locale.startsWith('ru') ? 'Сообщить Qwen что он должен сделать' : 'Tell Qwen what it should do instead';
}

function getApprovalFeedbackPlaceholder(locale: string): string {
  return locale.startsWith('ru')
    ? 'Например: сначала прочитай файл и только потом предлагай изменения'
    : 'For example: read the file first, then suggest a safer change';
}

function getPendingApprovalReason(entry: DesktopSessionEntry): string {
  const body = (entry.body || '').trim();
  if (!body || body.toLowerCase() === 'ask') {
    return '';
  }

  if (/requires confirmation/i.test(body) || /waiting for approval/i.test(body)) {
    return '';
  }

  return body;
}

function getPendingApprovalDetailLines(entry: DesktopSessionEntry): string[] {
  const toolKey = (entry.toolName || entry.title || '').toLowerCase();
  if (toolKey.includes('shell') || toolKey.includes('bash') || toolKey.includes('run_command') || toolKey.includes('terminal') || toolKey.includes('execute')) {
    return formatShellArgumentLines(entry.arguments).slice(0, 4);
  }

  const parsed = parseObjectArguments(entry.arguments);
  if (!parsed) {
    return entry.arguments ? [trunc(entry.arguments, 160)] : [];
  }

  const preferredKeys = ['file_path', 'path', 'directory', 'pattern', 'query', 'url', 'server_name', 'tool_name'];
  const lines = preferredKeys
    .filter((key) => typeof parsed[key] === 'string' && `${parsed[key]}`.trim().length > 0)
    .slice(0, 4)
    .map((key) => `${key}: ${String(parsed[key]).trim()}`);

  if (lines.length > 0) {
    return lines;
  }

  return Object.entries(parsed)
    .slice(0, 4)
    .map(([key, value]) => `${key}: ${typeof value === 'string' ? value : JSON.stringify(value)}`);
}

function PendingApprovalCard({
  entry,
  locale,
  feedbackValue,
  onFeedbackChange,
  onAllowOnce,
  onAlwaysAllow,
  onSubmitFeedback,
}: {
  entry: DesktopSessionEntry;
  locale: string;
  feedbackValue: string;
  onFeedbackChange: (value: string) => void;
  onAllowOnce: () => void;
  onAlwaysAllow: () => void;
  onSubmitFeedback: () => void;
}) {
  const reason = getPendingApprovalReason(entry);
  const detailLines = getPendingApprovalDetailLines(entry);

  return (
    <Box maxW="560px" w="full">
      <Box
        border="1px solid"
        borderColor="gray.700"
        bg="rgba(39,39,46,0.94)"
        borderRadius="xl"
        px={3}
        py={2.5}
      >
        <Text fontSize="10px" color="gray.500" textTransform="uppercase" letterSpacing="0.14em">
          {getApprovalCardTitle(locale)}
        </Text>

        {reason && (
          <Text mt={1.5} fontSize="xs" color="gray.300" whiteSpace="pre-wrap" wordBreak="break-word">
            {reason}
          </Text>
        )}

        {detailLines.length > 0 && (
          <Box
            as="pre"
            mt={2}
            mb={0}
            px={2.5}
            py={2}
            bg="gray.900"
            border="1px solid"
            borderColor="gray.700"
            borderRadius="lg"
            color="gray.200"
            fontSize="11px"
            fontFamily="mono"
            overflowX="auto"
            whiteSpace="pre-wrap"
          >
            {detailLines.join('\n')}
          </Box>
        )}
      </Box>

      <HStack mt={2} spacing={2} align="stretch" flexWrap="wrap">
        <Button
          onClick={onAllowOnce}
          bg={ACCENT}
          color="white"
          _hover={{ bg: ACCENT_HOVER }}
          _active={{ bg: ACCENT_HOVER }}
          borderRadius="full"
          h="32px"
          px={3.5}
          fontSize="xs"
        >
          {getApprovalAllowOnceLabel(locale)}
        </Button>
        <Button
          onClick={onAlwaysAllow}
          bg="white"
          color="gray.900"
          _hover={{ bg: 'gray.100' }}
          _active={{ bg: 'gray.200' }}
          borderRadius="full"
          h="32px"
          px={3.5}
          fontSize="xs"
        >
          {getApprovalAlwaysAllowLabel(locale)}
        </Button>
        <Input
          value={feedbackValue}
          onChange={(event) => onFeedbackChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              onSubmitFeedback();
            }
          }}
          placeholder={getApprovalFeedbackLabel(locale)}
          bg="gray.900"
          border="1px solid"
          borderColor="gray.700"
          color="white"
          borderRadius="full"
          h="32px"
          px={3.5}
          fontSize="xs"
          flex="1 1 240px"
          minW="220px"
          _placeholder={{ color: 'gray.500' }}
          _hover={{ borderColor: 'gray.600' }}
          _focusVisible={{ borderColor: 'brand.400', boxShadow: '0 0 0 1px rgba(97,92,237,0.35)' }}
        />
        <IconButton
          aria-label={getApprovalFeedbackPlaceholder(locale)}
          icon={<ArrowUp size={15} />}
          onClick={onSubmitFeedback}
          isDisabled={!feedbackValue.trim()}
          bg="gray.800"
          color="white"
          border="1px solid"
          borderColor="gray.700"
          borderRadius="full"
          minW="32px"
          w="32px"
          h="32px"
          _hover={{ bg: 'gray.700' }}
          _active={{ bg: 'gray.700' }}
        />
      </HStack>
    </Box>
  );
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
  body: string;
  approvalState: string;
  timestamp: string;
  updatedAt: string;
  workingDirectory: string;
  gitBranch: string;
  changedFiles: string[];
  questions: import('@/types/desktop').DesktopQuestionPrompt[];
  answers: import('@/types/desktop').DesktopQuestionAnswer[];
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
      const nextCall: LiveToolCallSnapshot = {
        id: event.toolCallId || `live-tool-${calls.length}-${event.toolName}-${event.timestampUtc}`,
        groupId: event.toolCallGroupId || `live-tool-group-${calls.length}`,
        toolName: event.toolName,
        argumentsJson: event.toolArgumentsJson || '{}',
        status: normalizedStatus,
        body: event.toolOutput || event.message || '',
        approvalState: event.approvalState || '',
        timestamp: event.timestampUtc,
        updatedAt: event.timestampUtc,
        workingDirectory: event.workingDirectory || workingDirectory,
        gitBranch: event.gitBranch || gitBranch,
        changedFiles: event.changedFiles ?? [],
        questions: event.questions ?? [],
        answers: event.answers ?? [],
      };
      call = nextCall;
      calls.push(nextCall);
    }

    if (!call) {
      continue;
    }

    call.toolName = event.toolName || call.toolName;
    call.groupId = event.toolCallGroupId || call.groupId;
    call.argumentsJson = event.toolArgumentsJson || call.argumentsJson || '{}';
    call.status = normalizedStatus;
    call.body = event.toolOutput || event.message || call.body || '';
    call.approvalState = event.approvalState || call.approvalState || '';
    call.updatedAt = event.timestampUtc;
    call.workingDirectory = event.workingDirectory || call.workingDirectory || workingDirectory;
    call.gitBranch = event.gitBranch || call.gitBranch || gitBranch;
    call.changedFiles = event.changedFiles ?? call.changedFiles ?? [];
    call.questions = event.questions ?? call.questions ?? [];
    call.answers = event.answers ?? call.answers ?? [];
  }

  return calls.map((call) => ({
    id: call.id,
    type: 'tool',
    timestamp: call.updatedAt,
    workingDirectory: call.workingDirectory || workingDirectory,
    gitBranch: call.gitBranch || gitBranch,
    title: call.toolName,
    body: call.body || '',
    thinkingBody: '',
    status: call.status,
    toolName: call.toolName,
    approvalState: call.approvalState || '',
    exitCode: null,
    arguments: call.argumentsJson || '{}',
    scope: call.groupId,
    sourcePath: LIVE_TOOL_SOURCE,
    resolutionStatus: 'live',
    resolvedAt: '',
    changedFiles: call.changedFiles ?? [],
    questions: call.questions ?? [],
    answers: call.answers ?? [],
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

function getTaskStatusLabel(locale: string, status: string): string {
  const normalized = normalizeTaskStatus(status);
  if (normalized === 'completed' || normalized === 'done') {
    return locale.startsWith('ru') ? 'Выполнено' : 'Completed';
  }

  if (normalized === 'in_progress') {
    return locale.startsWith('ru') ? 'В работе' : 'In progress';
  }

  if (normalized === 'cancelled') {
    return locale.startsWith('ru') ? 'Остановлено' : 'Stopped';
  }

  return locale.startsWith('ru') ? 'Ожидает' : 'Pending';
}

function getTaskProgressLabel(locale: string, completedCount: number, totalCount: number): string {
  return locale.startsWith('ru')
    ? `Выполнено ${completedCount} из ${totalCount}`
    : `${completedCount} of ${totalCount} completed`;
}

function renderTaskSummaryContent(taskSummary: TaskSummary, locale: string) {
  return (
    <VStack spacing={1.5} align="stretch">
      <Text fontSize="xs" color="gray.400">
        {getTaskProgressLabel(locale, taskSummary.completedCount, taskSummary.totalCount)}
      </Text>
      {taskSummary.items.map((item) => {
        const normalizedStatus = normalizeTaskStatus(item.status);
        const isCompleted = normalizedStatus === 'completed' || normalizedStatus === 'done';
        const isCancelled = normalizedStatus === 'cancelled';
        const supportingBits = [
          getTaskStatusLabel(locale, item.status),
          item.owner ? (locale.startsWith('ru') ? `Ответственный: ${item.owner}` : `Owner: ${item.owner}`) : '',
          item.blockedBy.length > 0
            ? locale.startsWith('ru')
              ? `Блокирует: ${item.blockedBy.join(', ')}`
              : `Blocked by: ${item.blockedBy.join(', ')}`
            : '',
        ].filter(Boolean);

        return (
          <HStack key={item.id} spacing={2} align="start">
            <Box mt="2px" color={isCompleted ? 'green.400' : isCancelled ? 'gray.600' : 'gray.500'}>
              <CheckSquare size={12} />
            </Box>
            <Box flex={1} minW={0}>
              <Text
                fontSize="xs"
                color={isCompleted ? 'gray.200' : 'gray.300'}
                textDecoration={isCompleted || isCancelled ? 'line-through' : 'none'}
                whiteSpace="pre-wrap"
                wordBreak="break-word"
              >
                {item.subject}
              </Text>
              {item.description && (
                <Text fontSize="10px" color="gray.500" whiteSpace="pre-wrap" wordBreak="break-word">
                  {item.description}
                </Text>
              )}
              {supportingBits.length > 0 && (
                <Text fontSize="10px" color="gray.500" whiteSpace="pre-wrap" wordBreak="break-word">
                  {supportingBits.join(' • ')}
                </Text>
              )}
            </Box>
          </HStack>
        );
      })}
    </VStack>
  );
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
    if (toolKey.includes('task_')) {
      const subject = str('subject');
      if (subject) {
        return trunc(subject);
      }

      const taskId = str('task_id') || str('taskId');
      if (taskId) {
        return `#${taskId}`;
      }

      const status = str('status');
      return status ? trunc(status) : '';
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

export default function ChatArea({
  selectedSessionId,
  sidebarMode = 'projects',
  onSelectSession,
}: ChatAreaProps) {
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
  const [retainedStreamingSnapshot, setRetainedStreamingSnapshot] = useState('');
  const [projectPickerPosition, setProjectPickerPosition] = useState({ top: 0, left: 0, width: 320, maxHeight: 320 });
  const [approvalFeedbackById, setApprovalFeedbackById] = useState<Record<string, string>>({});
  const [resolvingApprovalIds, setResolvingApprovalIds] = useState<Record<string, true>>({});

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
  const effectiveStreamingSnapshot = streamingSnapshot.trim() || retainedStreamingSnapshot.trim();
  const isSessionStreaming = !!selectedSessionId && !!activeTurnSessions[selectedSessionId];
  const isPendingSelectedSession = !!selectedSessionId && !!pendingTurnSessionIds[selectedSessionId];
  const isComposerBusy = isPendingSelectedSession || isSessionStreaming;
  const canStopActiveTurn = !!selectedSessionId && isComposerBusy;
  const isAwaitingAssistantText = hasSession && isComposerBusy && !effectiveStreamingSnapshot;
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

  const selectedProjectLabel = getProjectDisplayName(selectedProjectPath, locale);
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

  useEffect(() => {
    if (!selectedSessionId) {
      setRetainedStreamingSnapshot('');
      return;
    }

    if (streamingSnapshot.trim()) {
      setRetainedStreamingSnapshot(streamingSnapshot);
      return;
    }

    if (isSessionStreaming) {
      return;
    }

    const lastAssistantBody = [...(sessionDetail?.entries ?? [])]
      .reverse()
      .find((entry) => entry.type === 'assistant' && !!entry.body?.trim())
      ?.body
      ?.trim() ?? '';

    if (!retainedStreamingSnapshot.trim()) {
      return;
    }

    if (lastAssistantBody === retainedStreamingSnapshot.trim()) {
      setRetainedStreamingSnapshot('');
    }
  }, [isSessionStreaming, retainedStreamingSnapshot, selectedSessionId, sessionDetail, streamingSnapshot]);

  const displaySessionDetail = useMemo(() => {
    if (!sessionDetail || !selectedSessionId) {
      return sessionDetail;
    }

    const syntheticEntries: DesktopSessionEntry[] = isSessionStreaming ? [...liveToolEntries] : [];
    const hasStreamingAssistant = effectiveStreamingSnapshot.length > 0;
    const lastNonSystemEntry = [...sessionDetail.entries]
      .reverse()
      .find((entry) => entry.type !== 'system' && entry.type !== 'tool_result');

    if (
      hasStreamingAssistant &&
      !(lastNonSystemEntry?.type === 'assistant' && (lastNonSystemEntry.body ?? '') === effectiveStreamingSnapshot)
    ) {
      const timestamp = latestSessionEvent?.sessionId === selectedSessionId
        ? latestSessionEvent.timestampUtc
        : new Date().toISOString();
      syntheticEntries.push(
        createStreamingAssistantEntry(
          selectedSessionId,
          selectedSession?.workingDirectory ?? sessionDetail.session.workingDirectory,
          selectedSession?.gitBranch ?? sessionDetail.session.gitBranch,
          effectiveStreamingSnapshot,
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
    effectiveStreamingSnapshot,
  ]);

  const sessionProjectRoot = bootstrap?.workspaceRoot ?? displaySessionDetail?.session.workingDirectory ?? '';
  const pendingApprovalPresentations = useMemo(
    () => buildPendingApprovalPresentations(displaySessionDetail?.entries ?? [], sessionProjectRoot),
    [displaySessionDetail?.entries, sessionProjectRoot],
  );
  const visiblePendingApprovalPresentations = useMemo(
    () => pendingApprovalPresentations.filter((presentation) => !resolvingApprovalIds[presentation.pendingEntry.id]),
    [pendingApprovalPresentations, resolvingApprovalIds],
  );
  const activePendingApprovalPresentation = visiblePendingApprovalPresentations[0] ?? null;

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
  }, [displaySessionDetail?.entries.length, effectiveStreamingSnapshot, isAwaitingAssistantText, scrollToBottom]);

  useEffect(() => {
    setUsedTokens(estimateSessionTokens(displaySessionDetail));
  }, [displaySessionDetail]);

  useEffect(() => {
    shouldStickToBottomRef.current = true;
    scrollToBottom(true);
  }, [scrollToBottom, selectedSessionId]);

  useEffect(() => {
    setApprovalFeedbackById({});
    setResolvingApprovalIds({});
  }, [selectedSessionId]);

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
    if (sidebarMode === 'chats' && projectPickerOpen) {
      setProjectPickerOpen(false);
    }
  }, [projectPickerOpen, sidebarMode]);

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
    if (selectedSession?.workingDirectory) {
      if (pathStartsWith(selectedSession.workingDirectory, getProjectlessTempDirectory(bootstrap?.qwenRuntime?.runtimeBaseDirectory ?? '', bootstrap?.workspaceRoot ?? ''))) {
        if (selectedProjectMode !== 'no-project') {
          setSelectedProjectMode('no-project');
        }

        return;
      }

      if (selectedProjectMode !== 'project') {
        setSelectedProjectMode('project');
      }

      if (normalizePathKey(selectedProjectPath) !== normalizePathKey(selectedSession.workingDirectory)) {
        setSelectedProjectPath(selectedSession.workingDirectory);
      }

      return;
    }

    if (sidebarMode === 'chats') {
      if (selectedProjectMode !== 'no-project') {
        setSelectedProjectMode('no-project');
      }

      return;
    }

    if (selectedProjectMode !== 'project') {
      setSelectedProjectMode('project');
    }
  }, [
    bootstrap?.qwenRuntime?.runtimeBaseDirectory,
    bootstrap?.workspaceRoot,
    selectedProjectMode,
    selectedProjectPath,
    selectedSession?.workingDirectory,
    sidebarMode,
  ]);

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
      selectedSession?.title ?? null,
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
          recentSessions: [{
            ...result.session,
            title:
              result.createdNewSession &&
              current.recentSessions.find((session) => session.sessionId === result.session.sessionId)?.title === null
                ? null
                : result.session.title,
          }, ...current.recentSessions.filter((session) => session.sessionId !== result.session.sessionId)]
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

  const handleStopGeneration = useCallback(async () => {
    if (!selectedSessionId || !window.qwenDesktop) {
      return;
    }

    try {
      await window.qwenDesktop.cancelSessionTurn({ sessionId: selectedSessionId });
    } catch (error) {
      console.error('Failed to cancel active turn:', error);
    } finally {
      setPendingTurnSessionIds((current) => {
        if (!(selectedSessionId in current)) {
          return current;
        }

        const next = { ...current };
        delete next[selectedSessionId];
        return next;
      });
    }
  }, [selectedSessionId]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && e.ctrlKey) {
      e.preventDefault();
      if (isComposerBusy) {
        void handleStopGeneration();
      } else {
        handleSubmit();
      }
    }
  };

  const clearApprovalState = useCallback((entryIds: string[]) => {
    setApprovalFeedbackById((current) => {
      const next = { ...current };
      for (const entryId of entryIds) {
        delete next[entryId];
      }

      return next;
    });

    setResolvingApprovalIds((current) => {
      const next = { ...current };
      for (const entryId of entryIds) {
        delete next[entryId];
      }

      return next;
    });
  }, []);

  const markApprovalsResolving = useCallback((entryIds: string[]) => {
    setResolvingApprovalIds((current) => {
      const next = { ...current };
      for (const entryId of entryIds) {
        next[entryId] = true;
      }

      return next;
    });
  }, []);

  const handlePendingApprovalDecision = useCallback(async (
    presentation: PendingApprovalPresentation,
    decision: PendingApprovalDecision,
    feedback = '',
  ) => {
    if (!selectedSessionId || !window.qwenDesktop) {
      return;
    }

    const trimmedFeedback = feedback.trim();
    const matchingPresentations = decision === 'always-allow'
      ? pendingApprovalPresentations.filter((candidate) =>
        candidate.pendingEntry.id !== presentation.pendingEntry.id &&
        candidate.signature === presentation.signature)
      : [];
    const optimisticEntryIds = [
      presentation.pendingEntry.id,
      ...matchingPresentations.map((candidate) => candidate.pendingEntry.id),
    ];

    markApprovalsResolving(optimisticEntryIds);
    setPendingTurnSessionIds((current) => ({
      ...current,
      [selectedSessionId]: true,
    }));

    try {
      await window.qwenDesktop.approvePendingTool({
        sessionId: selectedSessionId,
        entryId: presentation.pendingEntry.id,
        decision,
        feedback: trimmedFeedback,
      });

      if (decision === 'always-allow') {
        const normalizedRule = presentation.suggestedRule.trim();
        if (normalizedRule) {
          setBootstrap((current) => {
            const existingRules = current.qwenRuntime.approvalProfile.allowRules;
            if (existingRules.some((rule) => rule.toLowerCase() === normalizedRule.toLowerCase())) {
              return current;
            }

            return {
              ...current,
              qwenRuntime: {
                ...current.qwenRuntime,
                approvalProfile: {
                  ...current.qwenRuntime.approvalProfile,
                  allowRules: [...existingRules, normalizedRule],
                },
              },
            };
          });
        }
      }

      await loadSessionDetail(selectedSessionId, { force: true, limit: 200 });
      clearApprovalState(optimisticEntryIds);
    } catch (error) {
      console.error('Failed to resolve pending tool approval:', error);
      clearApprovalState(optimisticEntryIds);
      setPendingTurnSessionIds((current) => {
        if (!(selectedSessionId in current)) {
          return current;
        }

        const next = { ...current };
        delete next[selectedSessionId];
        return next;
      });
    }
  }, [
    clearApprovalState,
    loadSessionDetail,
    markApprovalsResolving,
    pendingApprovalPresentations,
    selectedSessionId,
    setPendingTurnSessionIds,
    setBootstrap,
  ]);

  const renderPendingApprovalInline = useCallback((entry: DesktopSessionEntry, marginLeft = 0) => {
    if (activePendingApprovalPresentation?.pendingEntry.id !== entry.id) {
      return null;
    }

    return (
      <Box mt={2} ml={marginLeft}>
        <PendingApprovalCard
          entry={entry}
          locale={locale}
          feedbackValue={approvalFeedbackById[entry.id] ?? ''}
          onFeedbackChange={(value) => {
            setApprovalFeedbackById((current) => ({
              ...current,
              [entry.id]: value,
            }));
          }}
          onAllowOnce={() => {
            void handlePendingApprovalDecision(activePendingApprovalPresentation, 'allow-once');
          }}
          onAlwaysAllow={() => {
            void handlePendingApprovalDecision(activePendingApprovalPresentation, 'always-allow');
          }}
          onSubmitFeedback={() => {
            const feedback = approvalFeedbackById[entry.id] ?? '';
            if (!feedback.trim()) {
              return;
            }

            void handlePendingApprovalDecision(activePendingApprovalPresentation, 'deny', feedback);
          }}
        />
      </Box>
    );
  }, [
    activePendingApprovalPresentation,
    approvalFeedbackById,
    handlePendingApprovalDecision,
    locale,
  ]);

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
      if (
        entry.type === 'tool' &&
        entry.status.trim().toLowerCase() === 'approval-required' &&
        !!entry.resolutionStatus?.trim()
      ) {
        continue;
      }

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
                            <motion.div
                              initial={{ opacity: 0, y: 10, scale: 0.985 }}
                              animate={{ opacity: 1, y: 0, scale: 1 }}
                              transition={{ duration: 0.18, ease: 'easeOut' }}
                              style={{ maxWidth: '80%' }}
                            >
                              <Box
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
                            </motion.div>
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
                        const info = getToolInfo(entry.toolName || entry.title || '');
                        const ToolIcon = info.Icon;
                        const label = t(info.labelKey);
                        const todoSummary = parseTodoSummary(entry.arguments);
                        const taskSummary = parseTaskSummary(entry);
                        const summary = getToolArgSummary(entry);
                        const isCollapsed = collapsedBlocks[blockKey] !== false;
                        const hasTodoDetail = !!todoSummary;
                        const hasTaskDetail = !!taskSummary;
                        const canExpand = hasTodoDetail || hasTaskDetail;
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
                                    {hasTaskDetail && taskSummary && renderTaskSummaryContent(taskSummary, locale)}
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
                            {renderPendingApprovalInline(entry, 7)}
                          </Box>
                        );
                      }

                      // ── Multiple tools: collapsible with timeline ──
                      const containsActivePendingApproval = !!activePendingApprovalPresentation &&
                        block.entries.some((entry) => entry.id === activePendingApprovalPresentation.pendingEntry.id);
                      const isCollapsed = containsActivePendingApproval
                        ? false
                        : hasLiveEntries
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
                                    const taskSummary = parseTaskSummary(entry);
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
                                        {!isShell && taskSummary && (
                                          <Box ml={5} mt={0.5} px={2} py={1.5} bg="gray.900" borderRadius="md">
                                            {renderTaskSummaryContent(taskSummary, locale)}
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
                                        {renderPendingApprovalInline(entry, 5)}
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
                      const isStreamingEntry = entry.status === 'streaming';
                      // Skip assistant entries that have no content (e.g. pure orchestrator turns)
                      if (!text && !thinking) return null;
                      if (isApprovalPlaceholderText(text)) return null;
                      // Show timestamp only on the last entry of the final assistant block in each AI turn
                      const isLastEntry = entryIdx === block.entries.length - 1;
                      const showTime = isLastEntry && finalAssistantBlockIndices.has(blockIdx) && !!entry.timestamp;

                      const thinkKey = `think-${entry.id}`;
                      const isThinkCollapsed = collapsedBlocks[thinkKey] !== false;

                      return (
                        <motion.div
                          key={entry.id}
                          layout="position"
                          transition={{ duration: 0.16, ease: 'easeOut' }}
                        >
                          <Box py={2}>
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
                            <StreamingAssistantBody text={text} isStreaming={isStreamingEntry} />
                          )}

                          {showTime && (
                            <Text fontSize="10px" color="gray.700" mt={1}>{formatTimestamp(entry.timestamp)}</Text>
                          )}
                          </Box>
                        </motion.div>
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
              {sidebarMode === 'chats' ? t('chat.chatModeWelcomeTitle') : t('chat.welcomeTitle')}
            </Text>
            {sidebarMode === 'projects' && (
              <>
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
              </>
            )}
          </Flex>
        )}
      </Box>

      {/* Input Area — always visible */}
      <Box px={4} pb={4} pt={3} position="relative" bg="gray.900" flexShrink={0}>
        {/* Fade gradient mask above input */}
        <Box
          position="absolute"
          top="-24px"
          left={0}
          right={0}
          h="24px"
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
              placeholder={sidebarMode === 'chats' ? t('chat.chatModePromptPlaceholder') : t('chat.promptPlaceholder')}
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
                aria-label={canStopActiveTurn ? 'Stop' : 'Send'}
                icon={(
                  <AnimatePresence mode="wait" initial={false}>
                    <motion.span
                      key={canStopActiveTurn ? 'stop' : 'send'}
                      initial={{ opacity: 0, scale: 0.72, rotate: canStopActiveTurn ? -18 : 18 }}
                      animate={{ opacity: 1, scale: 1, rotate: 0 }}
                      exit={{ opacity: 0, scale: 0.72, rotate: canStopActiveTurn ? 18 : -18 }}
                      transition={{ duration: 0.18, ease: 'easeOut' }}
                      style={{ display: 'flex' }}
                    >
                      {canStopActiveTurn ? <Square size={13} fill="currentColor" /> : <ArrowUp size={16} />}
                    </motion.span>
                  </AnimatePresence>
                )}
                bg={canStopActiveTurn ? 'rgba(239,68,68,0.92)' : ACCENT}
                color="white"
                _hover={{ bg: canStopActiveTurn ? 'rgba(220,38,38,0.98)' : ACCENT_HOVER }}
                isDisabled={canStopActiveTurn
                  ? false
                  : (!prompt.trim() || (!selectedSession && !selectedProjectWorkingDirectory))}
                onClick={canStopActiveTurn ? () => { void handleStopGeneration(); } : handleSubmit}
                borderRadius="full"
                w="36px"
                h="36px"
                minW="36px"
                transition="background-color 0.18s ease, transform 0.18s ease"
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
