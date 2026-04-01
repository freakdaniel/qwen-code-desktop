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
  setMode: (mode) => invoke('qwen-desktop:app:set-mode', { mode }),
  setLocale: (locale) => invoke('qwen-desktop:app:set-locale', { locale }),
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
