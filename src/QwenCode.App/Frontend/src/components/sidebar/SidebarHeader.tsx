// Frontend/src/components/sidebar/SidebarHeader.tsx
import { MessageSquarePlus, Wrench, Clock } from 'lucide-react'
import { Button } from '@/components/ui/button'
import type { WorkspaceSurface } from '@/types/ui'

interface SidebarHeaderProps {
  onNewChat: () => void
  onOpenUtilities: () => void
  surface: WorkspaceSurface
}

export function SidebarHeader({ onNewChat, onOpenUtilities, surface }: SidebarHeaderProps) {
  return (
    <div className="flex flex-col gap-1 px-2 pt-3 pb-2">
      <Button
        variant="ghost"
        className="justify-start gap-2.5 h-9 px-2.5 text-sm font-medium text-[--app-text] hover:bg-[--app-elevated] w-full"
        onClick={onNewChat}
      >
        <MessageSquarePlus size={16} className="shrink-0" />
        Новая беседа
      </Button>
      <Button
        variant="ghost"
        className={`justify-start gap-2.5 h-9 px-2.5 text-sm font-medium w-full ${
          surface === 'utilities'
            ? 'bg-[--app-elevated] text-[--app-text]'
            : 'text-[--app-text] hover:bg-[--app-elevated]'
        }`}
        onClick={onOpenUtilities}
      >
        <Wrench size={16} className="shrink-0" />
        Навыки и интеграции
      </Button>
      <Button
        variant="ghost"
        className="justify-start gap-2.5 h-9 px-2.5 text-sm font-medium text-[--app-muted] hover:bg-[--app-elevated] hover:text-[--app-text] w-full"
        disabled
      >
        <Clock size={16} className="shrink-0" />
        Автоматизации
      </Button>
    </div>
  )
}
