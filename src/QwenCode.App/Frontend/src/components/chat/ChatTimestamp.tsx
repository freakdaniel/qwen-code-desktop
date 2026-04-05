import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'

interface ChatTimestampProps {
  value?: string
  align?: 'start' | 'center' | 'end'
  className?: string
}

function formatShortTime(date: Date) {
  return new Intl.DateTimeFormat(undefined, {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

function formatDetailedTime(date: Date) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'medium',
  }).format(date)
}

export function ChatTimestamp({ value, align = 'start', className = '' }: ChatTimestampProps) {
  if (!value) {
    return null
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return (
      <span className={`text-[11px] text-[--app-muted] ${className}`.trim()}>
        {value}
      </span>
    )
  }

  const shortValue = formatShortTime(parsed)
  const detailedValue = formatDetailedTime(parsed)

  return (
    <TooltipProvider delay={150}>
      <Tooltip>
        <TooltipTrigger
          render={
            <button
              type="button"
              className={`inline-flex items-center rounded-full border border-[--app-border] bg-[--app-panel] px-2 py-0.5 text-[11px] text-[--app-muted] transition-colors hover:text-[--app-text] ${className}`.trim()}
            />
          }
        >
          {shortValue}
        </TooltipTrigger>
        <TooltipContent
          side="top"
          align={align}
          className="border-[--app-border] bg-[--app-elevated] text-[--app-text] text-xs"
        >
          {detailedValue}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}
