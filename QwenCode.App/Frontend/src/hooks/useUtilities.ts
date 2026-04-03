// Frontend/src/hooks/useUtilities.ts
import type { AppBootstrapPayload } from '@/types/desktop'

export function useUtilities(bootstrap: AppBootstrapPayload) {
  const tools = bootstrap.qwenNativeHost.tools
  const agents = bootstrap.qwenCompatibility.skills
  const mcpServers = bootstrap.qwenMcp.servers

  return { tools, agents, mcpServers }
}
