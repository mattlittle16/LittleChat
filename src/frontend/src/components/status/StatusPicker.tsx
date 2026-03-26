import { useState } from 'react'
import EmojiPicker, { type EmojiClickData } from 'emoji-picker-react'
import { setStatus, clearStatus } from '../../services/enrichedMessagingApiService'
import type { UserStatus } from '../../types'

const PALETTE = [
  { key: 'green',  hex: '#22c55e' },
  { key: 'yellow', hex: '#eab308' },
  { key: 'red',    hex: '#ef4444' },
  { key: 'grey',   hex: '#6b7280' },
  { key: 'blue',   hex: '#3b82f6' },
  { key: 'orange', hex: '#f97316' },
  { key: 'purple', hex: '#a855f7' },
  { key: 'pink',   hex: '#ec4899' },
]

interface Props {
  currentStatus: UserStatus | null
  onClose: () => void
  onStatusChange: (status: UserStatus | null) => void
}

export function StatusPicker({ currentStatus, onClose, onStatusChange }: Props) {
  const [emoji, setEmoji] = useState(currentStatus?.emoji ?? '')
  const [text, setText] = useState(currentStatus?.text ?? '')
  const [color, setColor] = useState<string | null>(currentStatus?.color ?? null)
  const [loading, setLoading] = useState(false)
  const [pickerOpen, setPickerOpen] = useState(false)

  const handleEmojiClick = (data: EmojiClickData) => {
    setEmoji(data.emoji)
    setPickerOpen(false)
  }

  const handleSet = async () => {
    setLoading(true)
    try {
      const result = await setStatus({ emoji: emoji || null, text: text || null, color })
      onStatusChange(result)
      onClose()
    } finally {
      setLoading(false)
    }
  }

  const handleClear = async () => {
    setLoading(true)
    try {
      await clearStatus()
      onStatusChange(null)
      onClose()
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="p-3 bg-background border border-border rounded-lg shadow-lg w-72">
      <h3 className="font-semibold text-sm mb-3">Set Status</h3>
      <div className="flex gap-2 mb-3">
        <div className="relative">
          <button
            type="button"
            onClick={() => setPickerOpen(v => !v)}
            className="w-12 h-9 border border-border rounded flex items-center justify-center text-lg hover:bg-muted/40 bg-background"
            title="Pick emoji"
          >
            {emoji || '😊'}
          </button>
          {pickerOpen && (
            <div className="absolute top-full left-0 mt-1 z-50">
              <EmojiPicker onEmojiClick={handleEmojiClick} height={350} width={280} />
            </div>
          )}
        </div>
        <div className="flex-1">
          <input
            className="w-full border border-border rounded px-2 py-1.5 text-sm bg-background"
            placeholder="What's your status?"
            value={text}
            onChange={e => setText(e.target.value.slice(0, 60))}
            maxLength={60}
          />
          <p className="text-xs text-muted-foreground text-right mt-0.5">{text.length}/60</p>
        </div>
      </div>
      <div className="flex gap-2 flex-wrap mb-3">
        {PALETTE.map(p => (
          <button
            key={p.key}
            title={p.key}
            onClick={() => setColor(color === p.key ? null : p.key)}
            className={`w-6 h-6 rounded-full transition-transform ${color === p.key ? 'ring-2 ring-foreground scale-110' : ''}`}
            style={{ backgroundColor: p.hex }}
          />
        ))}
      </div>
      <div className="flex gap-2">
        <button
          disabled={loading}
          onClick={handleSet}
          className="flex-1 bg-primary text-primary-foreground rounded px-3 py-1.5 text-sm font-medium hover:bg-primary/90 disabled:opacity-50"
        >
          Set Status
        </button>
        {currentStatus && (
          <button
            disabled={loading}
            onClick={handleClear}
            className="border border-border rounded px-3 py-1.5 text-sm hover:bg-muted/40 disabled:opacity-50"
          >
            Clear
          </button>
        )}
      </div>
    </div>
  )
}
