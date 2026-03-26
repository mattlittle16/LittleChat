import { Star, Trash2 } from 'lucide-react'
import type { Highlight } from '../../types'
import { removeHighlight } from '../../services/enrichedMessagingApiService'
import { useHighlightStore } from '../../stores/highlightStore'

interface Props {
  roomId: string
  onJumpTo: (messageId: string) => void
}

export function HighlightsTab({ roomId, onJumpTo }: Props) {
  const { highlights, removeHighlight: removeFromStore } = useHighlightStore()
  const entries: Highlight[] = highlights[roomId] ?? []

  const handleRemove = async (highlightId: string) => {
    try {
      await removeHighlight(roomId, highlightId)
      removeFromStore(roomId, highlightId)
    } catch { /* ignore */ }
  }

  if (entries.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground p-8 text-center">
        <Star size={32} className="mb-3 opacity-30" />
        <p className="text-sm">No highlights yet — highlight messages to surface them here.</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2 p-3 overflow-y-auto">
      {entries.map((h) => (
        <div key={h.id} className="border border-border rounded-lg p-3 bg-card">
          <div className="flex items-start justify-between gap-2">
            <div className="flex-1 min-w-0">
              <p className="text-xs text-muted-foreground mb-1">
                Highlighted by <span className="font-medium text-foreground">{h.highlightedByDisplayName}</span>
              </p>
              {h.isDeleted ? (
                <p className="text-sm italic text-muted-foreground">Original message deleted</p>
              ) : (
                <button
                  className="text-sm text-left hover:underline text-foreground"
                  onClick={() => !h.isDeleted && onJumpTo(h.messageId)}
                >
                  {h.contentPreview ?? '—'}
                </button>
              )}
              <p className="text-xs text-muted-foreground mt-1">
                {new Date(h.highlightedAt).toLocaleString()}
              </p>
            </div>
            <button
              onClick={() => handleRemove(h.id)}
              className="text-muted-foreground hover:text-destructive flex-shrink-0"
            >
              <Trash2 size={14} />
            </button>
          </div>
        </div>
      ))}
    </div>
  )
}
