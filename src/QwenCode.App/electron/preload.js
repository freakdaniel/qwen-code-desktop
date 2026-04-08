const { contextBridge, ipcRenderer, shell } = require('electron');

const EXTERNAL_PROTOCOLS = new Set(['http:', 'https:', 'mailto:']);

function isExternalUrl(value) {
  if (typeof value !== 'string' || !value.trim()) {
    return false;
  }

  try {
    const url = new URL(value);
    return EXTERNAL_PROTOCOLS.has(url.protocol);
  } catch {
    return false;
  }
}

function openExternalUrl(url) {
  if (!isExternalUrl(url)) {
    return Promise.resolve(false);
  }

  return shell.openExternal(url).then(() => true);
}

function installExternalLinkInterception() {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return;
  }

  const handlePointerNavigation = (event) => {
    const target = event.target;
    const anchor = target && typeof target.closest === 'function'
      ? target.closest('a[href]')
      : null;

    if (!anchor) {
      return;
    }

    const href = anchor.getAttribute('href') || anchor.href;
    if (!isExternalUrl(href)) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    void openExternalUrl(href);
  };

  window.addEventListener('click', handlePointerNavigation, true);
  window.addEventListener('auxclick', handlePointerNavigation, true);

  const originalOpen = window.open?.bind(window);
  window.open = (url, target, features) => {
    if (typeof url === 'string' && isExternalUrl(url)) {
      void openExternalUrl(url);
      return null;
    }

    return originalOpen ? originalOpen(url, target, features) : null;
  };
}

installExternalLinkInterception();

const invoke = (channel, payload) =>
  new Promise((resolve, reject) => {
    const replyChannel = `${channel}:reply`;
    const handler = (_event, raw) => {
      ipcRenderer.removeListener(replyChannel, handler);

      try {
        resolve(JSON.parse(raw));
      } catch (error) {
        reject(error);
      }
    };

    ipcRenderer.once(replyChannel, handler);
    ipcRenderer.send(channel, JSON.stringify(payload ?? {}));
  });

contextBridge.exposeInMainWorld('qwenDesktop', {
  bootstrap: () => invoke('qwen-desktop:app:bootstrap', {}),
  getSession: (request) => invoke('qwen-desktop:sessions:get', request),
  removeSession: (request) => invoke('qwen-desktop:sessions:remove', request),
  getActiveTurns: () => invoke('qwen-desktop:sessions:get-active-turns', {}),
  setLocale: (locale) => invoke('qwen-desktop:app:set-locale', { locale }),
  getAuthStatus: () => invoke('qwen-desktop:auth:status', {}),
  configureOpenAiCompatibleAuth: (request) => invoke('qwen-desktop:auth:configure-openai-compatible', request),
  configureCodingPlanAuth: (request) => invoke('qwen-desktop:auth:configure-coding-plan', request),
  configureQwenOAuth: (request) => invoke('qwen-desktop:auth:configure-qwen-oauth', request),
  startQwenOAuthDeviceFlow: (request) => invoke('qwen-desktop:auth:start-qwen-oauth-device-flow', request),
  cancelQwenOAuthDeviceFlow: (request) => invoke('qwen-desktop:auth:cancel-qwen-oauth-device-flow', request),
  disconnectAuth: (request) => invoke('qwen-desktop:auth:disconnect', request),
  getChannelPairings: (request) => invoke('qwen-desktop:channels:get-pairings', request),
  approveChannelPairing: (request) => invoke('qwen-desktop:channels:approve-pairing', request),
  getWorkspaceSnapshot: () => invoke('qwen-desktop:workspace:get', {}),
  createGitCheckpoint: (request) => invoke('qwen-desktop:workspace:create-git-checkpoint', request),
  restoreGitCheckpoint: (request) => invoke('qwen-desktop:workspace:restore-git-checkpoint', request),
  createManagedWorktree: (request) => invoke('qwen-desktop:workspace:create-managed-worktree', request),
  cleanupManagedSession: (request) => invoke('qwen-desktop:workspace:cleanup-managed-session', request),
  selectProjectDirectory: () => invoke('qwen-desktop:workspace:select-project-directory', {}),
  getExtensionSettings: (request) => invoke('qwen-desktop:extensions:get-settings', request),
  installExtension: (request) => invoke('qwen-desktop:extensions:install', request),
  setExtensionEnabled: (request) => invoke('qwen-desktop:extensions:set-enabled', request),
  setExtensionSetting: (request) => invoke('qwen-desktop:extensions:set-setting', request),
  removeExtension: (request) => invoke('qwen-desktop:extensions:remove', request),
  addMcpServer: (request) => invoke('qwen-desktop:mcp:add', request),
  removeMcpServer: (request) => invoke('qwen-desktop:mcp:remove', request),
  reconnectMcpServer: (request) => invoke('qwen-desktop:mcp:reconnect', request),
  startSessionTurn: (request) => invoke('qwen-desktop:sessions:start-turn', request),
  cancelSessionTurn: (request) => invoke('qwen-desktop:sessions:cancel-turn', request),
  resumeInterruptedTurn: (request) => invoke('qwen-desktop:sessions:resume-interrupted', request),
  dismissInterruptedTurn: (request) => invoke('qwen-desktop:sessions:dismiss-interrupted', request),
  approvePendingTool: (request) => invoke('qwen-desktop:sessions:approve-tool', request),
  answerPendingQuestion: (request) => invoke('qwen-desktop:sessions:answer-question', request),
  executeNativeTool: (request) => invoke('qwen-desktop:tools:execute-native', request),
  openExternalUrl,
  subscribeStateChanged: (callback) => {
    const channel = 'qwen-desktop:app:state-changed';
    const handler = (_event, raw) => {
      callback(JSON.parse(raw));
    };

    ipcRenderer.on(channel, handler);

    return () => {
      ipcRenderer.removeListener(channel, handler);
    };
  },
  subscribeAuthChanged: (callback) => {
    const channel = 'qwen-desktop:auth:changed';
    const handler = (_event, raw) => {
      callback(JSON.parse(raw));
    };

    ipcRenderer.on(channel, handler);

    return () => {
      ipcRenderer.removeListener(channel, handler);
    };
  },
  subscribeSessionEvents: (callback) => {
    const channel = 'qwen-desktop:sessions:event';
    const handler = (_event, raw) => {
      callback(JSON.parse(raw));
    };

    ipcRenderer.on(channel, handler);

    return () => {
      ipcRenderer.removeListener(channel, handler);
    };
  },

  // Window management
  minimizeWindow: () => {
    ipcRenderer.send('qwen-desktop:window:minimize');
  },
  maximizeWindow: () => {
    ipcRenderer.send('qwen-desktop:window:maximize');
  },
  closeWindow: () => {
    ipcRenderer.send('qwen-desktop:window:close');
  },
});
