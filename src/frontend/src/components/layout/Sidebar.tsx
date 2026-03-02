import { useEffect, useState } from 'react'
import { useRoomStore } from '../../stores/roomStore'
import { api } from '../../services/apiClient'
import type { Room } from '../../types'

export function Sidebar() {
  const { rooms, activeRoomId, loadRooms, setActiveRoom } = useRoomStore()
  const [creating, setCreating] = useState(false)
  const [newRoomName, setNewRoomName] = useState('')

  useEffect(() => {
    loadRooms()
  }, [loadRooms])

  // Restore last-viewed room on mount
  useEffect(() => {
    const savedId = localStorage.getItem('littlechat_active_room')
    if (savedId && rooms.some(r => r.id === savedId)) {
      setActiveRoom(savedId)
    } else if (rooms.length > 0 && !activeRoomId) {
      setActiveRoom(rooms[0].id)
    }
  }, [rooms, activeRoomId, setActiveRoom])

  async function handleCreateRoom(e: React.FormEvent) {
    e.preventDefault()
    const name = newRoomName.trim()
    if (!name) return

    try {
      const room = await api.post<Room>('/api/rooms', { name })
      await loadRooms()
      setActiveRoom(room.id)
    } finally {
      setCreating(false)
      setNewRoomName('')
    }
  }

  return (
    <aside className="flex h-full w-60 flex-col border-r bg-muted/30">
      <div className="flex items-center justify-between px-4 py-3 border-b">
        <span className="font-semibold text-sm">Rooms</span>
        <button
          onClick={() => setCreating(c => !c)}
          className="text-muted-foreground hover:text-foreground text-lg leading-none"
          title="Create Room"
        >
          +
        </button>
      </div>

      {creating && (
        <form onSubmit={handleCreateRoom} className="px-3 py-2 border-b flex gap-2">
          <input
            autoFocus
            value={newRoomName}
            onChange={e => setNewRoomName(e.target.value)}
            placeholder="Room name"
            className="flex-1 rounded border px-2 py-1 text-sm bg-background focus:outline-none focus:ring-1 focus:ring-ring"
          />
          <button
            type="submit"
            className="rounded bg-primary px-2 py-1 text-xs text-primary-foreground"
          >
            Create
          </button>
        </form>
      )}

      <nav className="flex-1 overflow-y-auto py-1">
        {rooms.map(room => (
          <RoomItem
            key={room.id}
            room={room}
            isActive={room.id === activeRoomId}
            onClick={() => setActiveRoom(room.id)}
          />
        ))}
        {rooms.length === 0 && (
          <p className="px-4 py-2 text-xs text-muted-foreground">No rooms yet</p>
        )}
      </nav>
    </aside>
  )
}

function RoomItem({ room, isActive, onClick }: {
  room: Room
  isActive: boolean
  onClick: () => void
}) {
  return (
    <button
      onClick={onClick}
      className={`w-full flex items-center gap-2 px-4 py-1.5 text-sm text-left hover:bg-muted/60
        ${isActive ? 'bg-muted font-medium' : ''}`}
    >
      <span className="text-muted-foreground">#</span>
      <span className="flex-1 truncate">{room.name}</span>
      {room.unreadCount > 0 && (
        <span className={`ml-auto rounded-full px-1.5 py-0.5 text-xs font-semibold
          ${room.hasMention ? 'bg-destructive text-destructive-foreground' : 'bg-primary text-primary-foreground'}`}>
          {room.unreadCount > 99 ? '99+' : room.unreadCount}
        </span>
      )}
    </button>
  )
}
