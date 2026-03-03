import { useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import EmojiPicker, { type EmojiClickData } from 'emoji-picker-react'
import { getConnection } from '../../services/signalrClient'
import type { Reaction } from '../../types'

const PICKER_HEIGHT = 350
const PICKER_WIDTH = 300
const PICKER_MARGIN = 8

interface ReactionBarProps {
  messageId: string
  roomId: string
  reactions: Reaction[]
}

interface PickerPosition {
  top: number
  left: number
}

export function ReactionBar({ messageId, roomId, reactions }: ReactionBarProps) {
  const [pickerOpen, setPickerOpen] = useState(false)
  const [pickerPosition, setPickerPosition] = useState<PickerPosition | null>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  function invokeReaction(emoji: string) {
    const connection = getConnection()
    if (connection?.state !== 'Connected') return
    connection.invoke('AddReaction', { messageId, roomId, emoji }).catch((err) => {
      if (import.meta.env.DEV) console.error('[ReactionBar] AddReaction failed:', err)
    })
  }

  function handleOpenPicker() {
    const rect = buttonRef.current?.getBoundingClientRect()
    if (!rect) return

    // Viewport-aware vertical placement: open above by default,
    // flip to below when there isn't enough space above the button.
    const openBelow = rect.top < PICKER_HEIGHT + PICKER_MARGIN
    const top = openBelow
      ? rect.bottom + PICKER_MARGIN
      : rect.top - PICKER_HEIGHT - PICKER_MARGIN

    // Keep picker horizontally inside the viewport
    const left = Math.min(rect.left, window.innerWidth - PICKER_WIDTH - 8)

    setPickerPosition({ top, left })
    setPickerOpen(true)
  }

  function handleEmojiClick(data: EmojiClickData) {
    setPickerOpen(false)
    setPickerPosition(null)
    invokeReaction(data.emoji)
  }

  function closePicker() {
    setPickerOpen(false)
    setPickerPosition(null)
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

      {/* Add reaction button — ref lets us read its viewport position on click */}
      <button
        ref={buttonRef}
        onClick={handleOpenPicker}
        className="rounded-full border px-1.5 py-0.5 text-xs text-muted-foreground
                   hover:bg-muted/60 hover:text-foreground transition-colors"
        title="Add reaction"
      >
        +
      </button>

      {/* Picker rendered into document.body via portal — escapes overflow-y:auto clipping */}
      {pickerOpen && pickerPosition && createPortal(
        <>
          {/* Backdrop — closes picker on outside click */}
          <div
            style={{ position: 'fixed', inset: 0, zIndex: 9998 }}
            onClick={closePicker}
          />
          <div
            style={{
              position: 'fixed',
              top: pickerPosition.top,
              left: pickerPosition.left,
              zIndex: 9999,
            }}
          >
            <EmojiPicker
              onEmojiClick={handleEmojiClick}
              lazyLoadEmojis
              height={PICKER_HEIGHT}
              width={PICKER_WIDTH}
            />
          </div>
        </>,
        document.body
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
