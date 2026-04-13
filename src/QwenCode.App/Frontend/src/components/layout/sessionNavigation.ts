import type { SessionPreview } from '@/types/desktop';

export type SessionNavigationMode = 'projects' | 'chats';

interface SessionScopeOptions {
  runtimeBaseDirectory?: string;
  workspaceRoot?: string;
}

export interface ProjectGroup {
  name: string;
  sessions: SessionPreview[];
}

function getPathSeparator(basePath: string): string {
  return basePath.includes('\\') ? '\\' : '/';
}

export function normalizePathKey(path: string): string {
  return path.replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase();
}

export function pathStartsWith(path: string, root: string): boolean {
  if (!path || !root) {
    return false;
  }

  const normalizedPath = normalizePathKey(path);
  const normalizedRoot = normalizePathKey(root);
  return normalizedPath === normalizedRoot || normalizedPath.startsWith(`${normalizedRoot}/`);
}

export function joinDesktopPath(basePath: string, ...segments: string[]): string {
  const separator = getPathSeparator(basePath);
  const trimmedBase = basePath.replace(/[\\/]+$/, '');
  const trimmedSegments = segments
    .map((segment) => segment.replace(/^[\\/]+|[\\/]+$/g, ''))
    .filter(Boolean);

  return [trimmedBase, ...trimmedSegments].join(separator);
}

export function getProjectlessTempDirectory(
  runtimeBaseDirectory: string,
  workspaceRoot: string,
): string {
  const baseDirectory = runtimeBaseDirectory.trim() || workspaceRoot.trim();
  return joinDesktopPath(baseDirectory, 'tmp', 'no-project');
}

export function isProjectlessWorkingDirectory(
  workingDirectory: string,
  options: SessionScopeOptions,
): boolean {
  if (!workingDirectory.trim()) {
    return false;
  }

  const projectlessRoot = getProjectlessTempDirectory(
    options.runtimeBaseDirectory ?? '',
    options.workspaceRoot ?? '',
  );

  if (projectlessRoot && pathStartsWith(workingDirectory, projectlessRoot)) {
    return true;
  }

  return /(?:^|[\\/])tmp[\\/]no-project(?:[\\/]|$)/i.test(workingDirectory) ||
    /(?:^|[\\/])(?:aionui-)?qwen-temp-[^\\/]+(?:[\\/]|$)/i.test(workingDirectory);
}

export function isProjectlessSession(
  session: SessionPreview,
  options: SessionScopeOptions,
): boolean {
  return isProjectlessWorkingDirectory(session.workingDirectory, options);
}

export function filterSessionsByNavigationMode(
  sessions: SessionPreview[],
  mode: SessionNavigationMode,
  options: SessionScopeOptions,
): SessionPreview[] {
  return sessions.filter((session) =>
    session.messageCount > 0 &&
    (mode === 'chats'
      ? isProjectlessSession(session, options)
      : !isProjectlessSession(session, options)),
  );
}

export function getProjectNameFromWorkingDirectory(
  workingDirectory: string,
  fallbackLabel: string,
): string {
  if (!workingDirectory) {
    return fallbackLabel;
  }

  const parts = workingDirectory.replace(/\\/g, '/').split('/').filter(Boolean);
  return parts[parts.length - 1] || fallbackLabel;
}

export function groupProjectSessions(
  sessions: SessionPreview[],
  fallbackLabel: string,
): ProjectGroup[] {
  const groups: Record<string, SessionPreview[]> = {};

  for (const session of sessions) {
    const project = getProjectNameFromWorkingDirectory(session.workingDirectory, fallbackLabel);
    if (!groups[project]) {
      groups[project] = [];
    }

    groups[project].push(session);
  }

  return Object.entries(groups)
    .sort((left, right) => {
      const leftLatest = Math.max(...left[1].map((session) => new Date(session.lastActivity).getTime()));
      const rightLatest = Math.max(...right[1].map((session) => new Date(session.lastActivity).getTime()));
      return rightLatest - leftLatest;
    })
    .map(([name, projectSessions]) => ({
      name,
      sessions: projectSessions.sort(
        (left, right) => new Date(right.lastActivity).getTime() - new Date(left.lastActivity).getTime(),
      ),
    }));
}
