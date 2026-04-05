// Frontend/src/hooks/useUtilities.ts
import type { AppBootstrapPayload } from '@/types/desktop'

export function useUtilities(bootstrap: AppBootstrapPayload) {
  const tools = bootstrap.qwenNativeHost.tools
  const agents = bootstrap.qwenCompatibility.skills
  const workspaceSnapshot = bootstrap.qwenWorkspace
  const channelSnapshot = bootstrap.qwenChannels
  const channels = bootstrap.qwenChannels.channels
  const mcpServers = bootstrap.qwenMcp.servers
  const extensions = bootstrap.qwenExtensions.extensions

  return { tools, agents, channels, channelSnapshot, mcpServers, extensions, workspaceSnapshot }
}
