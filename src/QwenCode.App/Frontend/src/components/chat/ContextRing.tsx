import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'

interface ContextRingProps {
  usedTokens: number
  totalTokens: number
}

export function ContextRing({ usedTokens, totalTokens }: ContextRingProps) {
  if (!totalTokens) return null

  const percent = Math.min(100, Math.round((usedTokens / totalTokens) * 100))
  const circumference = 2 * Math.PI * 10

  const color =
    percent >= 90 ? '#ef4444'
    : percent >= 70 ? '#f97316'
    : '#8a8f98'

  return (
    <TooltipProvider delay={200}>
      <Tooltip>
        <TooltipTrigger
          render={
            <button
              type="button"
              className="inline-flex h-9 w-9 cursor-default items-center justify-center rounded-full border border-[--app-border] bg-[--app-panel]"
              aria-label={`Context window: ${percent}% used`}
            />
          }
        >
          <svg width="28" height="28" viewBox="0 0 28 28" className="-rotate-90">
            <circle
              cx="14"
              cy="14"
              r="10"
              fill="none"
              stroke="#2b2b31"
              strokeWidth="2"
            />
            <circle
              cx="14"
              cy="14"
              r="10"
              fill="none"
              stroke={color}
              strokeWidth="2"
              strokeLinecap="round"
              strokeDasharray={circumference}
              strokeDashoffset={circumference - (percent / 100) * circumference}
              style={{ transition: 'stroke-dashoffset 0.5s ease, stroke 0.3s ease' }}
            />
          </svg>
        </TooltipTrigger>
        <TooltipContent
          side="top"
          align="center"
          className="border-[--app-border] bg-[--app-elevated] text-[--app-text] text-xs"
        >
          <div className="flex flex-col gap-1 p-1">
            <p className="font-medium">Context window</p>
            <p className="text-[--app-muted]">{percent}% used</p>
            <p className="text-[--app-muted]">
              {(usedTokens / 1000).toFixed(0)}k / {(totalTokens / 1000).toFixed(0)}k tokens used
            </p>
            {percent >= 70 && (
              <p className="text-orange-400">Agent will auto-compress context</p>
            )}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}
