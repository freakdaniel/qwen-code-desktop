import { createInterface } from 'node:readline';
import { EventEmitter } from 'node:events';
import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync, unlinkSync } from 'node:fs';
import { dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

const emitter = new EventEmitter();
let channel = null;
let activeCollector = null;
let config = null;
let channelName = '';
let router = null;
const pendingPrompts = new Map();
const sessionCwds = new Map();

function writeMessage(message) {
  process.stdout.write(`${JSON.stringify(message)}\n`);
}

function writeResponse(requestId, payload) {
  writeMessage({ type: 'response', requestId, ...payload });
}

function fail(requestId, error) {
  writeResponse(requestId, { error: error instanceof Error ? error.message : String(error) });
}

class SimpleSessionRouter {
  constructor(bridge, defaultCwd, scope = 'user', persistPath = undefined) {
    this.bridge = bridge;
    this.defaultCwd = defaultCwd;
    this.defaultScope = scope;
    this.persistPath = persistPath;
    this.toSession = new Map();
    this.toTarget = new Map();
    this.toCwd = new Map();
  }

  routingKey(channelName, senderId, chatId, threadId) {
    switch (this.defaultScope) {
      case 'thread':
        return `${channelName}:${threadId || chatId}`;
      case 'single':
        return `${channelName}:__single__`;
      case 'user':
      default:
        return `${channelName}:${senderId}:${chatId}`;
    }
  }

  async resolve(channelName, senderId, chatId, threadId, cwd) {
    const key = this.routingKey(channelName, senderId, chatId, threadId);
    const existing = this.toSession.get(key);
    if (existing) {
      return existing;
    }

    const sessionCwd = cwd || this.defaultCwd;
    const sessionId = await this.bridge.newSession(sessionCwd);
    this.toSession.set(key, sessionId);
    this.toTarget.set(sessionId, { channelName, senderId, chatId, threadId });
    this.toCwd.set(sessionId, sessionCwd);
    this.persist();
    return sessionId;
  }

  getTarget(sessionId) {
    return this.toTarget.get(sessionId);
  }

  hasSession(channelName, senderId, chatId) {
    const key = chatId ? this.routingKey(channelName, senderId, chatId) : `${channelName}:${senderId}`;
    if (chatId) {
      return this.toSession.has(key);
    }

    for (const existingKey of this.toSession.keys()) {
      if (existingKey.startsWith(`${channelName}:${senderId}`)) {
        return true;
      }
    }

    return false;
  }

  removeSession(channelName, senderId, chatId) {
    const removed = [];
    if (chatId) {
      const key = this.routingKey(channelName, senderId, chatId);
      const sessionId = this.deleteByKey(key);
      if (sessionId) {
        removed.push(sessionId);
      }
    } else {
      const prefix = `${channelName}:${senderId}`;
      for (const key of [...this.toSession.keys()]) {
        if (key.startsWith(prefix)) {
          const sessionId = this.deleteByKey(key);
          if (sessionId) {
            removed.push(sessionId);
          }
        }
      }
    }

    if (removed.length > 0) {
      this.persist();
    }

    return removed;
  }

  deleteByKey(key) {
    const sessionId = this.toSession.get(key);
    if (!sessionId) {
      return null;
    }

    this.toSession.delete(key);
    this.toTarget.delete(sessionId);
    this.toCwd.delete(sessionId);
    return sessionId;
  }

  async restoreSessions() {
    if (!this.persistPath || !existsSync(this.persistPath)) {
      return { restored: 0, failed: 0 };
    }

    let entries;
    try {
      entries = JSON.parse(readFileSync(this.persistPath, 'utf8'));
    } catch {
      return { restored: 0, failed: 0 };
    }

    let restored = 0;
    let failed = 0;
    for (const [key, entry] of Object.entries(entries)) {
      try {
        const sessionId = await this.bridge.loadSession(entry.sessionId, entry.cwd);
        this.toSession.set(key, sessionId);
        this.toTarget.set(sessionId, entry.target);
        this.toCwd.set(sessionId, entry.cwd);
        restored++;
      } catch {
        failed++;
      }
    }

    if (failed > 0) {
      this.persist();
    }

    return { restored, failed };
  }

  persist() {
    if (!this.persistPath) {
      return;
    }

    const data = {};
    for (const [key, sessionId] of this.toSession.entries()) {
      const target = this.toTarget.get(sessionId);
      if (!target) {
        continue;
      }

      data[key] = {
        sessionId,
        target,
        cwd: this.toCwd.get(sessionId) || this.defaultCwd,
      };
    }

    writeFileSync(this.persistPath, JSON.stringify(data, null, 2), 'utf8');
  }

  clearAll() {
    this.toSession.clear();
    this.toTarget.clear();
    this.toCwd.clear();
    if (this.persistPath && existsSync(this.persistPath)) {
      try {
        unlinkSync(this.persistPath);
      } catch {
      }
    }
  }
}

const bridge = {
  availableCommands: [],
  on(eventName, handler) {
    emitter.on(eventName, handler);
  },
  off(eventName, handler) {
    emitter.off(eventName, handler);
  },
  async newSession(cwd) {
    const sessionId = randomUUID();
    sessionCwds.set(sessionId, cwd || config?.workingDirectory || process.cwd());
    return sessionId;
  },
  async loadSession(sessionId, cwd) {
    sessionCwds.set(sessionId, cwd || config?.workingDirectory || process.cwd());
    return sessionId;
  },
  async cancelSession(sessionId) {
    writeMessage({ type: 'cancel', sessionId });
  },
  async prompt(sessionId, text, options = {}) {
    const requestId = randomUUID();
    const cwd = sessionCwds.get(sessionId) || config?.workingDirectory || process.cwd();
    writeMessage({
      type: 'prompt',
      requestId,
      sessionId,
      cwd,
      text,
      options,
    });

    return await new Promise((resolve, reject) => {
      pendingPrompts.set(requestId, { resolve, reject, sessionId });
    });
  },
};

async function initialize(message) {
  const moduleUrl = pathToFileURL(message.entryPath).href;
  const imported = await import(moduleUrl);
  const plugin = imported?.plugin;
  if (!plugin || typeof plugin.createChannel !== 'function') {
    throw new Error('Channel entry point does not export a valid plugin object');
  }

  if (plugin.channelType !== message.config.type) {
    throw new Error(`channelType mismatch: expected "${message.config.type}", got "${plugin.channelType}"`);
  }

  for (const field of plugin.requiredConfigFields || []) {
    if (!message.config[field]) {
      throw new Error(`Channel "${message.channelName}" (${plugin.channelType}) requires "${field}"`);
    }
  }

  config = message.config;
  channelName = message.channelName;
  const persistPath = joinSessionsPath(message.channelName);
  router = new SimpleSessionRouter(bridge, config.workingDirectory || process.cwd(), config.sessionScope || 'user', persistPath);
  channel = plugin.createChannel(message.channelName, message.config, bridge, { router });

  if (!channel || typeof channel.connect !== 'function' || typeof channel.handleInbound !== 'function') {
    throw new Error('Channel plugin did not create a valid channel instance');
  }

  const originalSendMessage = channel.sendMessage?.bind(channel);
  if (originalSendMessage) {
    channel.sendMessage = async (chatId, text) => {
      if (activeCollector) {
        activeCollector.push({ chatId, text });
      }

      writeMessage({ type: 'outbound', channelName, chatId, text });
      return await originalSendMessage(chatId, text);
    };
  }

  await channel.connect();
  await router.restoreSessions();

  return {
    channelType: plugin.channelType,
    displayName: plugin.displayName || message.channelName,
    requiredConfigFields: plugin.requiredConfigFields || [],
  };
}

function joinSessionsPath(name) {
  const home = process.env.USERPROFILE || process.env.HOME || process.cwd();
  return `${home}\\.qwen\\channels\\plugin-${name}-sessions.json`;
}

async function handleEnvelope(message) {
  if (!channel) {
    throw new Error('Channel plugin host is not initialized');
  }

  const collector = [];
  activeCollector = collector;
  try {
    await channel.handleInbound(message.envelope);
  } finally {
    activeCollector = null;
  }

  const assistantSummary = collector.length > 0 ? collector[collector.length - 1].text : '';
  return { assistantSummary };
}

async function disconnect() {
  try {
    if (channel && typeof channel.disconnect === 'function') {
      await channel.disconnect();
    }
  } finally {
    router?.clearAll?.();
  }
}

const rl = createInterface({ input: process.stdin, crlfDelay: Infinity });
rl.on('line', async (line) => {
  if (!line) {
    return;
  }

  let message;
  try {
    message = JSON.parse(line);
  } catch (error) {
    return;
  }

  const requestId = message.requestId;
  try {
    switch (message.type) {
      case 'init':
        writeResponse(requestId, await initialize(message));
        break;
      case 'handleEnvelope':
        writeResponse(requestId, await handleEnvelope(message));
        break;
      case 'disconnect':
        await disconnect();
        writeResponse(requestId, { disconnected: true });
        process.exit(0);
        break;
      case 'promptResult': {
        const pending = pendingPrompts.get(message.requestId);
        if (pending) {
          pendingPrompts.delete(message.requestId);
          pending.resolve(message.response || '');
        }
        break;
      }
      case 'promptError': {
        const pending = pendingPrompts.get(message.requestId);
        if (pending) {
          pendingPrompts.delete(message.requestId);
          pending.reject(new Error(message.error || 'Prompt failed'));
        }
        break;
      }
      case 'textChunk': {
        const sessionId = message.sessionId;
        emitter.emit('textChunk', sessionId, message.chunk || '');
        break;
      }
    }
  } catch (error) {
    if (requestId) {
      fail(requestId, error);
    }
  }
});
