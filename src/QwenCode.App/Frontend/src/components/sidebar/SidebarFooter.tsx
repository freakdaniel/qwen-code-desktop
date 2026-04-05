// Frontend/src/components/sidebar/SidebarFooter.tsx
import { Settings } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { WorkspaceSurface } from '@/types/ui'

interface SidebarFooterProps {
  surface: WorkspaceSurface
  onOpenSettings: () => void
  onUpdate: () => void
}

export function SidebarFooter({ surface, onOpenSettings, onUpdate }: SidebarFooterProps) {
  return (
    <div className="flex items-center justify-between gap-2 px-2 py-2 border-t border-[--app-border] shrink-0">
      <Button
        variant="ghost"
        size="sm"
        className={`gap-2 text-xs font-medium ${
          surface === 'settings'
            ? 'text-[--app-text] bg-[--app-elevated]'
            : 'text-[--app-muted] hover:text-[--app-text] hover:bg-[--app-elevated]'
        }`}
        onClick={onOpenSettings}
      >
        <Settings size={14} />
        Настройки
      </Button>
      <Button
        variant="outline"
        size="sm"
        className="text-xs font-medium border-[--app-border] bg-transparent text-[--app-muted] hover:text-[--app-text] hover:bg-[--app-elevated]"
        onClick={onUpdate}
      >
        Обновить
      </Button>
    </div>
  )
}
