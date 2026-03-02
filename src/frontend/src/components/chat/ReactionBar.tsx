import { useState } from 'react'
import EmojiPicker, { type EmojiClickData } from 'emoji-picker-react'
import { getConnection } from '../../services/signalrClient'
import type { Reaction } from '../../types'

interface ReactionBarProps {
  messageId: string
  roomId: string
  reactions: Reaction[]
}

export function ReactionBar({ messageId, roomId, reactions }: ReactionBarProps) {
  const [pickerOpen, setPickerOpen] = useState(false)

  function invokeReaction(emoji: string) {
    const connection = getConnection()
    if (connection?.state !== 'Connected') return
    connection.invoke('AddReaction', { messageId, roomId, emoji }).catch(() => {
      // Not fatal — reaction will be retried on next click
    })
  }

  function handleEmojiClick(data: EmojiClickData) {
    setPickerOpen(false)
    invokeReaction(data.emoji)
  }

  return (
    <div className="relative flex flex-wrap items-center gap-1 mt-1">
      {reactions.map(r => (
        <ReactionChip
          key={r.emoji}
          reaction={r}
          onClick={() => invokeReaction(r.emoji)}
        />
      ))}

      {/* Add reaction button */}
      <button
        onClick={() => setPickerOpen(p => !p)}
        className="rounded-full border px-1.5 py-0.5 text-xs text-muted-foreground
                   hover:bg-muted/60 hover:text-foreground transition-colors"
        title="Add reaction"
      >
        +
      </button>

      {pickerOpen && (
        <div className="absolute bottom-full left-0 mb-1 z-50">
          <EmojiPicker
            onEmojiClick={handleEmojiClick}
            lazyLoadEmojis
            height={350}
            width={300}
          />
          {/* Dismiss backdrop */}
          <div
            className="fixed inset-0 -z-10"
            onClick={() => setPickerOpen(false)}
          />
        </div>
      )}
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
