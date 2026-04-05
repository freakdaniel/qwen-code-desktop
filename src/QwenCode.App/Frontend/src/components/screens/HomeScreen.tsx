// Frontend/src/components/screens/HomeScreen.tsx
import qwenLogo from '@/assets/qwen-logo.svg'

interface HomeScreenProps {
  projectName: string
}

export function HomeScreen({ projectName }: HomeScreenProps) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-4 select-none">
      <img
        src={qwenLogo}
        alt="Qwen"
        className="h-12 w-12 opacity-90"
        draggable={false}
      />
      <div className="flex flex-col items-center gap-1.5">
        <h1 className="text-2xl font-semibold tracking-tight text-[--app-text]">Let's build</h1>
        <p className="text-sm text-[--app-muted]">{projectName}</p>
      </div>
    </div>
  )
}
