import { useTypingStore } from '../../stores/typingStore'

interface TypingIndicatorProps {
  roomId: string
}

export function TypingIndicator({ roomId }: TypingIndicatorProps) {
  const typingByRoom = useTypingStore(s => s.typingByRoom)
  const names = Object.values(typingByRoom[roomId] ?? {})

  if (names.length === 0) return null

  const text =
    names.length === 1
      ? `${names[0]} is typing…`
      : names.length === 2
      ? `${names[0]} and ${names[1]} are typing…`
      : 'Several people are typing…'

  return (
    <p className="px-4 py-1 text-xs text-muted-foreground animate-pulse select-none">
      {text}
    </p>
  )
}
