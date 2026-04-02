import type { IconName } from './appData'

export function Icon({ name }: { name: IconName }) {
  switch (name) {
    case 'menu':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M4 7h16M4 12h16M4 17h16" />
        </svg>
      )
    case 'split':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M4 6.5h16v11H4zM11.5 6.5v11" />
        </svg>
      )
    case 'back':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M14.5 6.5 9 12l5.5 5.5M9.5 12H20" />
        </svg>
      )
    case 'forward':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="m9.5 6.5 5.5 5.5-5.5 5.5M4 12h11" />
        </svg>
      )
    case 'plus':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 5v14M5 12h14" />
        </svg>
      )
    case 'search':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="m17 17 3.5 3.5M10.5 18a7.5 7.5 0 1 1 0-15 7.5 7.5 0 0 1 0 15Z" />
        </svg>
      )
    case 'customize':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M5 8.5h14v10H5zM8 8.5v-2h8v2M9 13h6" />
        </svg>
      )
    case 'chats':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M6.5 17.5 4 20V7a3 3 0 0 1 3-3h8a3 3 0 0 1 3 3v7a3 3 0 0 1-3 3H8.5l-2 0.5Z" />
        </svg>
      )
    case 'projects':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M4 8.5h16v9A2.5 2.5 0 0 1 17.5 20h-11A2.5 2.5 0 0 1 4 17.5v-9ZM9 8.5v-2A1.5 1.5 0 0 1 10.5 5h3A1.5 1.5 0 0 1 15 6.5v2" />
        </svg>
      )
    case 'folder':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M3.5 7.5a2 2 0 0 1 2-2h4l1.8 2H18.5a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2v-9Z" />
        </svg>
      )
    case 'artifacts':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="m12 3 2.4 4.8L20 10l-4 3.8 1 6.2L12 17l-5 3 1-6.2L4 10l5.6-2.2L12 3Z" />
        </svg>
      )
    case 'write':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="m5 16.5 9.8-9.8 3.5 3.5L8.5 20H5v-3.5ZM13.8 7.7l1.8-1.7a1.8 1.8 0 0 1 2.5 0l.9.9a1.8 1.8 0 0 1 0 2.5l-1.7 1.8" />
        </svg>
      )
    case 'learn':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M4 7.5 12 4l8 3.5-8 3.5L4 7.5ZM7 10.2v4.6c0 1.8 2.2 3.2 5 3.2s5-1.4 5-3.2v-4.6" />
        </svg>
      )
    case 'code':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="m8.5 7.5-4 4.5 4 4.5M15.5 7.5l4 4.5-4 4.5M13 5l-2 14" />
        </svg>
      )
    case 'spark':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 2v6M12 16v6M2 12h6M16 12h6M5 5l4 4M15 15l4 4M19 5l-4 4M9 15l-4 4" />
        </svg>
      )
    case 'ghost':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M7 18.5V9a5 5 0 0 1 10 0v9.5l-2.2-1.5-2.8 1.5-2.8-1.5L7 18.5Z" />
          <path d="M10 10h.01M14 10h.01M10 13.5c.8.7 3.2.7 4 0" />
        </svg>
      )
    case 'chevronLeft':
      return (
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="m15 6-6 6 6 6" />
        </svg>
      )
  }
}
