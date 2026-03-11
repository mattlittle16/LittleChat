import { getConnection } from '../../services/signalrClient'
import type { Reaction } from '../../types'

interface ReactionBarProps {
  messageId: string
  roomId: string
  reactions: Reaction[]
}

export function ReactionBar({ messageId, roomId, reactions }: ReactionBarProps) {
  if (reactions.length === 0) return null

  function invokeReaction(emoji: string) {
    const connection = getConnection()
    if (connection?.state !== 'Connected') return
    connection.invoke('AddReaction', { messageId, roomId, emoji }).catch((err) => {
      if (import.meta.env.DEV) console.error('[ReactionBar] AddReaction failed:', err)
    })
  }

  return (
    <div className="flex flex-wrap items-center gap-1 mt-1">
      {reactions.map(r => (
        <ReactionChip
          key={r.emoji}
          reaction={r}
          onClick={() => invokeReaction(r.emoji)}
        />
      ))}
    </div>
  )
}

function ReactionChip({ reaction, onClick }: { reaction: Reaction; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      title={reaction.users.join(', ')}
      className="flex items-center gap-1 rounded-full border bg-muted/40 px-2 py-0.5
                 text-xs hover:bg-muted/80 transition-colors"
    >
      <span>{reaction.emoji}</span>
      <span className="font-medium">{reaction.count}</span>
    </button>
  )
}
