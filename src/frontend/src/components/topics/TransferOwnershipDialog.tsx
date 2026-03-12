import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { useRoomStore } from '../../stores/roomStore'
import { api } from '../../services/apiClient'

interface RoomMember {
  userId: string
  displayName: string
  avatarUrl: string | null
}

interface Props {
  roomId: string
  currentUserId: string
  onClose: () => void
  onTransferred?: () => void
}

export function TransferOwnershipDialog({ roomId, currentUserId, onClose, onTransferred }: Props) {
  const [members, setMembers] = useState<RoomMember[]>([])
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [selectedDisplayName, setSelectedDisplayName] = useState<string>('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const leaveTopic = useRoomStore(s => s.leaveTopic)

  useEffect(() => {
    api.get<RoomMember[]>(`/api/rooms/${roomId}/members`)
      .then(all => setMembers(all.filter(m => m.userId !== currentUserId)))
      .catch(() => setError('Failed to load members.'))
      .finally(() => setLoading(false))
  }, [roomId, currentUserId])

  async function handleLeave() {
    if (!selectedUserId) return
    setSubmitting(true)
    setError(null)
    try {
      await leaveTopic(roomId, selectedUserId, selectedDisplayName)
      onTransferred?.()
      onClose()
    } catch {
      setError('Failed to transfer ownership. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div
        className="w-full max-w-sm rounded-lg border bg-background shadow-xl p-6 relative"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <button
          onClick={onClose}
          className="absolute right-4 top-4 text-muted-foreground hover:text-foreground"
          aria-label="Close"
        >
          <X className="w-4 h-4" />
        </button>

        <h2 className="text-base font-semibold mb-1">Transfer Ownership</h2>
        <p className="text-xs text-muted-foreground mb-4">
          Select a new owner before leaving. A system message will be posted to the topic.
        </p>

        {loading && <p className="text-sm text-muted-foreground">Loading members…</p>}

        {!loading && error && (
          <p className="text-sm" style={{ color: 'hsl(var(--destructive))' }}>{error}</p>
        )}

        {!loading && !error && (
          <div className="flex flex-col gap-1 max-h-48 overflow-y-auto mb-4">
            {members.length === 0 && (
              <p className="text-sm text-muted-foreground">No other members to transfer to.</p>
            )}
            {members.map(m => (
              <label
                key={m.userId}
                className="flex items-center gap-2 rounded px-2 py-1.5 cursor-pointer hover:bg-muted/60"
              >
                <input
                  type="radio"
                  name="newOwner"
                  value={m.userId}
                  checked={selectedUserId === m.userId}
                  onChange={() => { setSelectedUserId(m.userId); setSelectedDisplayName(m.displayName) }}
                />
                <div className="flex items-center gap-1.5">
                  {m.avatarUrl ? (
                    <img src={m.avatarUrl} alt={m.displayName} className="w-5 h-5 rounded-full object-cover" />
                  ) : (
                    <div
                      className="w-5 h-5 rounded-full flex items-center justify-center text-xs font-semibold"
                      style={{ background: 'hsl(var(--muted))' }}
                    >
                      {m.displayName.charAt(0).toUpperCase()}
                    </div>
                  )}
                  <span className="text-sm">{m.displayName}</span>
                </div>
              </label>
            ))}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded px-3 py-1.5 text-sm"
            style={{ color: 'hsl(var(--muted-foreground))' }}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleLeave}
            disabled={!selectedUserId || submitting || loading}
            className="rounded px-3 py-1.5 text-sm font-medium disabled:opacity-50"
            style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
          >
            {submitting ? 'Leaving…' : 'Transfer & Leave'}
          </button>
        </div>
      </div>
    </div>
  )
}
