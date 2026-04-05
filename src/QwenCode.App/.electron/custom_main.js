'use strict';

let diagnosticsInstalled = false;

function log(message, details) {
  if (typeof details === 'undefined') {
    console.log(`custom_main → ${message}`);
    return;
  }

  console.log(`custom_main → ${message}`, details);
}

function logError(message, error) {
  if (!error) {
    console.error(`custom_main → ${message}`);
    return;
  }

  if (error instanceof Error) {
    console.error(`custom_main → ${message}: ${error.stack || error.message}`);
    return;
  }

  console.error(`custom_main → ${message}:`, error);
}

function installDiagnostics(app, BrowserWindow) {
  if (diagnosticsInstalled) {
    return;
  }

  diagnosticsInstalled = true;

  app.setName('Qwen Code Desktop');
  process.title = 'Qwen Code Desktop';

  if (process.platform === 'win32') {
    app.setAppUserModelId('io.github.freakdaniel.QwenCodeDesktop');
  }

  process.on('uncaughtException', (error) => {
    logError('Electron main uncaughtException', error);
  });

  process.on('unhandledRejection', (reason) => {
    logError('Electron main unhandledRejection', reason);
  });

  process.on('warning', (warning) => {
    logError('Electron main warning', warning);
  });

  process.on('beforeExit', (code) => {
    log(`Electron main beforeExit (${code})`);
  });

  process.on('exit', (code) => {
    log(`Electron main exit (${code})`);
  });

  app.on('browser-window-created', (event, window) => {
    const windowId = window.id;
    log(`BrowserWindow #${windowId} created`);

    window.on('close', () => log(`BrowserWindow #${windowId} close`));
    window.on('closed', () => log(`BrowserWindow #${windowId} closed`));
    window.on('unresponsive', () => log(`BrowserWindow #${windowId} unresponsive`));
    window.on('responsive', () => log(`BrowserWindow #${windowId} responsive`));

    const contents = window.webContents;
    contents.on('did-fail-load', (loadEvent, errorCode, errorDescription, validatedUrl, isMainFrame) => {
      log(`WebContents #${windowId} did-fail-load`, {
        errorCode,
        errorDescription,
        validatedUrl,
        isMainFrame,
      });
    });

    contents.on('render-process-gone', (goneEvent, details) => {
      log(`WebContents #${windowId} render-process-gone`, details);
    });

    contents.on('console-message', (consoleEvent, level, message, line, sourceId) => {
      if (level < 2 &&
        !/error|warning|failed|exception/i.test(message)) {
        return;
      }

      log(`Renderer console #${windowId} [${level}] ${sourceId}:${line} ${message}`);
    });
  });

  app.on('quit', (event, exitCode) => {
    log(`App quit (${exitCode})`);
  });

  app.on('window-all-closed', () => {
    const windows = BrowserWindow.getAllWindows().map((window) => ({
      id: window.id,
      destroyed: window.isDestroyed(),
      visible: window.isVisible(),
      focused: window.isFocused(),
      title: window.getTitle(),
    }));

    log('App window-all-closed', windows);
  });

  app.once('ready', () => {
    log('App ready');
  });
}

module.exports = {
  onStartup() {
    const { app, BrowserWindow } = require('electron');
    installDiagnostics(app, BrowserWindow);
  },
};
