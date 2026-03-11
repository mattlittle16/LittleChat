import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Plus } from 'lucide-react'
import { getConnection } from '../../services/signalrClient'
import type { Reaction } from '../../types'

const POPOVER_WIDTH = 160
const POPOVER_MARGIN = 6
const CLOSE_DELAY_MS = 150

interface ReactionBarProps {
  messageId: string
  roomId: string
  reactions: Reaction[]
  onOpenEmojiPicker?: (anchorRect: DOMRect) => void
}

export function ReactionBar({ messageId, roomId, reactions, onOpenEmojiPicker }: ReactionBarProps) {
  const [hoveredEmoji, setHoveredEmoji] = useState<string | null>(null)
  const [popoverPosition, setPopoverPosition] = useState<{ top: number; left: number } | null>(null)
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const plusButtonRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    return () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current)
    }
  }, [])

  if (reactions.length === 0) return null

  function cancelClose() {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current)
  }

  function scheduleClose() {
    closeTimerRef.current = setTimeout(() => {
      setHoveredEmoji(null)
      setPopoverPosition(null)
    }, CLOSE_DELAY_MS)
  }

  function startHover(emoji: string, chipRect: DOMRect) {
    cancelClose()
    const top = chipRect.top - POPOVER_MARGIN
    const left = Math.min(chipRect.left, window.innerWidth - POPOVER_WIDTH - 8)
    setHoveredEmoji(emoji)
    setPopoverPosition({ top, left })
  }

  function invokeReaction(emoji: string) {
    const connection = getConnection()
    if (connection?.state !== 'Connected') return
    connection.invoke('AddReaction', { messageId, roomId, emoji }).catch((err) => {
      if (import.meta.env.DEV) console.error('[ReactionBar] AddReaction failed:', err)
    })
  }

  const hoveredReaction = reactions.find(r => r.emoji === hoveredEmoji)

  return (
    <div className="flex flex-wrap items-center gap-1 mt-1">
      {reactions.map(r => (
        <div
          key={r.emoji}
          onMouseEnter={(e) => startHover(r.emoji, e.currentTarget.getBoundingClientRect())}
          onMouseLeave={scheduleClose}
        >
          <ReactionChip
            reaction={r}
            onClick={() => invokeReaction(r.emoji)}
          />
        </div>
      ))}

      <button
        ref={plusButtonRef}
        onClick={() => onOpenEmojiPicker?.(plusButtonRef.current!.getBoundingClientRect())}
        title="Add reaction"
        className="flex items-center justify-center rounded-full border bg-muted/40 px-2 py-0.5
                   text-xs text-muted-foreground hover:bg-muted/80 hover:text-foreground transition-colors"
      >
        <Plus className="w-3 h-3" />
      </button>

      {hoveredEmoji && popoverPosition && hoveredReaction && createPortal(
        <div
          style={{
            position: 'fixed',
            top: popoverPosition.top,
            left: popoverPosition.left,
            width: POPOVER_WIDTH,
            zIndex: 9998,
            transform: 'translateY(-100%)',
            background: 'hsl(var(--background))',
          }}
          onMouseEnter={cancelClose}
          onMouseLeave={scheduleClose}
          className="rounded-lg border shadow-md px-3 py-2 text-xs"
        >
          {hoveredReaction.users.map(name => (
            <div key={name}>{name}</div>
          ))}
        </div>,
        document.body
      )}
    </div>
  )
}

function ReactionChip({ reaction, onClick }: { reaction: Reaction; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className="flex items-center gap-1 rounded-full border bg-muted/40 px-2 py-0.5
                 text-xs hover:bg-muted/80 transition-colors"
    >
      <span>{reaction.emoji}</span>
      <span className="font-medium">{reaction.count}</span>
    </button>
  )
}
