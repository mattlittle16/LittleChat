import { useEffect, useRef, useState } from 'react'
import { api } from '../../services/apiClient'
import { useRoomStore } from '../../stores/roomStore'
import type { Room, UserSearchResult } from '../../types'

interface ComposeDialogProps {
  onClose: () => void
}

export function ComposeDialog({ onClose }: ComposeDialogProps) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<UserSearchResult[]>([])
  const [loading, setLoading] = useState(false)
  const { setActiveRoom, loadRooms } = useRoomStore()
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const timeout = setTimeout(async () => {
      setLoading(true)
      try {
        const params = query.trim() ? `?q=${encodeURIComponent(query.trim())}` : ''
        const users = await api.get<UserSearchResult[]>(`/api/users${params}`)
        setResults(users)
      } catch {
        // ignore
      } finally {
        setLoading(false)
      }
    }, 200)

    return () => {
      clearTimeout(timeout)
      controller.abort()
    }
  }, [query])

  async function openDm(userId: string) {
    try {
      const room = await api.post<Room>('/api/rooms/dm', { targetUserId: userId })
      await loadRooms()
      setActiveRoom(room.id)
      onClose()
    } catch {
      // ignore — room may already exist
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="w-96 rounded-lg border bg-background shadow-xl">
        <div className="flex items-center justify-between border-b px-4 py-3">
          <span className="font-semibold text-sm">New Direct Message</span>
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">✕</button>
        </div>

        <div className="px-3 py-2 border-b">
          <input
            ref={inputRef}
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Search people…"
            className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          />
        </div>

        <ul className="max-h-64 overflow-y-auto py-1">
          {loading && (
            <li className="px-4 py-2 text-xs text-muted-foreground">Searching…</li>
          )}
          {!loading && results.length === 0 && (
            <li className="px-4 py-2 text-xs text-muted-foreground">No users found</li>
          )}
          {results.map(user => (
            <li key={user.id}>
              <button
                onClick={() => openDm(user.id)}
                className="w-full flex items-center gap-3 px-4 py-2 hover:bg-muted/60 text-left"
              >
                <div className="relative flex-shrink-0">
                  {user.avatarUrl ? (
                    <img src={user.avatarUrl} alt={user.displayName}
                      className="w-8 h-8 rounded-full object-cover" />
                  ) : (
                    <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center text-sm font-semibold">
                      {user.displayName.charAt(0).toUpperCase()}
                    </div>
                  )}
                  {user.isOnline && (
                    <span className="absolute bottom-0 right-0 w-2.5 h-2.5 rounded-full bg-green-500 border-2 border-background" />
                  )}
                </div>
                <span className="text-sm">{user.displayName}</span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}
