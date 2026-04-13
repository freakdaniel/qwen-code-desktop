import { Box } from '@chakra-ui/react'

const handles = [
  { edge: 'top-left', cursor: 'nwse-resize', top: 0, left: 0, width: '10px', height: '10px' },
  { edge: 'top', cursor: 'ns-resize', top: 0, left: '10px', right: '10px', height: '6px' },
  { edge: 'top-right', cursor: 'nesw-resize', top: 0, right: 0, width: '10px', height: '10px' },
  { edge: 'right', cursor: 'ew-resize', top: '10px', right: 0, bottom: '10px', width: '6px' },
  { edge: 'bottom-right', cursor: 'nwse-resize', right: 0, bottom: 0, width: '10px', height: '10px' },
  { edge: 'bottom', cursor: 'ns-resize', right: '10px', bottom: 0, left: '10px', height: '6px' },
  { edge: 'bottom-left', cursor: 'nesw-resize', bottom: 0, left: 0, width: '10px', height: '10px' },
  { edge: 'left', cursor: 'ew-resize', top: '10px', bottom: '10px', left: 0, width: '6px' },
] as const

export default function WindowResizeHandles() {
  const beginResize = (edge: string) => {
    window.qwenDesktop?.beginWindowResize?.(edge)
  }

  return (
    <>
      {handles.map((handle) => (
        <Box
          key={handle.edge}
          position="absolute"
          zIndex={40}
          onPointerDown={(event) => {
            if (event.button !== 0) {
              return
            }

            event.preventDefault()
            event.stopPropagation()
            beginResize(handle.edge)
          }}
          {...handle}
        />
      ))}
    </>
  )
}
