'use strict';

module.exports = {
  onStartup() {
    const { app } = require('electron');

    app.setName('Qwen Code Desktop');
    process.title = 'Qwen Code Desktop';

    if (process.platform === 'win32') {
      app.setAppUserModelId('io.github.freakdaniel.QwenCodeDesktop');
    }
  },
};

