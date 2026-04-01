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
  setLocale: (locale) => invoke('qwen-desktop:app:set-locale', { locale }),
  startSessionTurn: (request) => invoke('qwen-desktop:sessions:start-turn', request),
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
});
