// Frontend/src/types/ui.ts

export type WorkspaceSurface = 'sessions' | 'settings' | 'utilities'

export type AgentMode = 'default' | 'plan' | 'auto-edit' | 'yolo'

export interface AgentModeOption {
  value: AgentMode
  label: string
  description: string
}

export const AGENT_MODES: AgentModeOption[] = [
  { value: 'default', label: 'Ask permissions', description: 'Always ask before making changes' },
  { value: 'auto-edit', label: 'Auto accept edits', description: 'Automatically accept all file edits' },
  { value: 'plan', label: 'Plan mode', description: 'Create a plan before making changes' },
  { value: 'yolo', label: 'Bypass permissions', description: 'Accepts all permissions' },
]
