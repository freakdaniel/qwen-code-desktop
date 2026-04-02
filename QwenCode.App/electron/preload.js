const { contextBridge, ipcRenderer } = require('electron');

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
  getActiveTurns: () => invoke('qwen-desktop:sessions:get-active-turns', {}),
  setLocale: (locale) => invoke('qwen-desktop:app:set-locale', { locale }),
  getAuthStatus: () => invoke('qwen-desktop:auth:status', {}),
  configureOpenAiCompatibleAuth: (request) => invoke('qwen-desktop:auth:configure-openai-compatible', request),
  configureCodingPlanAuth: (request) => invoke('qwen-desktop:auth:configure-coding-plan', request),
  configureQwenOAuth: (request) => invoke('qwen-desktop:auth:configure-qwen-oauth', request),
  startQwenOAuthDeviceFlow: (request) => invoke('qwen-desktop:auth:start-qwen-oauth-device-flow', request),
  cancelQwenOAuthDeviceFlow: (request) => invoke('qwen-desktop:auth:cancel-qwen-oauth-device-flow', request),
  disconnectAuth: (request) => invoke('qwen-desktop:auth:disconnect', request),
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
});
