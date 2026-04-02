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
  startSessionTurn: (request) => invoke('qwen-desktop:sessions:start-turn', request),
  cancelSessionTurn: (request) => invoke('qwen-desktop:sessions:cancel-turn', request),
  resumeInterruptedTurn: (request) => invoke('qwen-desktop:sessions:resume-interrupted', request),
  dismissInterruptedTurn: (request) => invoke('qwen-desktop:sessions:dismiss-interrupted', request),
  approvePendingTool: (request) => invoke('qwen-desktop:sessions:approve-tool', request),
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
