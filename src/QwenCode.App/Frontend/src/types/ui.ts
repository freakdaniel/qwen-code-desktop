// Frontend/src/types/ui.ts

export type WorkspaceSurface = 'sessions' | 'settings' | 'utilities'

export type AgentMode = 'default' | 'plan' | 'auto-edit' | 'yolo'

export interface AgentModeOption {
  value: AgentMode
  labelKey: string
  descriptionKey: string
}

export const AGENT_MODES: AgentModeOption[] = [
  { value: 'default', labelKey: 'modes.askPermissions', descriptionKey: 'modes.askPermissionsDesc' },
  { value: 'auto-edit', labelKey: 'modes.autoAcceptEdits', descriptionKey: 'modes.autoAcceptEditsDesc' },
  { value: 'plan', labelKey: 'modes.planMode', descriptionKey: 'modes.planModeDesc' },
  { value: 'yolo', labelKey: 'modes.bypassPermissions', descriptionKey: 'modes.bypassPermissionsDesc' },
]
