import { useEffect, useState } from 'react'
import { useRoomStore } from '../../stores/roomStore'
import { api } from '../../services/apiClient'
import { ComposeDialog } from './ComposeDialog'
import type { Room } from '../../types'

export function Sidebar() {
  const { rooms, activeRoomId, loadRooms, setActiveRoom } = useRoomStore()
  const [creating, setCreating] = useState(false)
  const [newRoomName, setNewRoomName] = useState('')
  const [composing, setComposing] = useState(false)

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

  const channelRooms = rooms.filter(r => !r.isDm)
  const dmRooms = rooms.filter(r => r.isDm)

  return (
    <>
      <aside className="flex h-full w-60 flex-col border-r bg-muted/30">
        {/* Channels section */}
        <div className="flex items-center justify-between px-4 py-2 mt-1">
          <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Channels</span>
          <button
            onClick={() => setCreating(c => !c)}
            className="text-muted-foreground hover:text-foreground text-lg leading-none"
            title="Create Channel"
          >
            +
          </button>
        </div>

        {creating && (
          <form onSubmit={handleCreateRoom} className="px-3 pb-2 flex gap-2">
            <input
              autoFocus
              value={newRoomName}
              onChange={e => setNewRoomName(e.target.value)}
              placeholder="Channel name"
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

        <nav className="flex-1 overflow-y-auto">
          {channelRooms.map(room => (
            <RoomItem
              key={room.id}
              room={room}
              isActive={room.id === activeRoomId}
              onClick={() => setActiveRoom(room.id)}
            />
          ))}
          {channelRooms.length === 0 && (
            <p className="px-4 py-1 text-xs text-muted-foreground">No channels yet</p>
          )}

          {/* DMs section */}
          <div className="flex items-center justify-between px-4 py-2 mt-3">
            <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Direct Messages</span>
            <button
              onClick={() => setComposing(true)}
              className="text-muted-foreground hover:text-foreground text-lg leading-none"
              title="New DM"
            >
              +
            </button>
          </div>

          {dmRooms.map(room => (
            <DmItem
              key={room.id}
              room={room}
              isActive={room.id === activeRoomId}
              onClick={() => setActiveRoom(room.id)}
            />
          ))}
          {dmRooms.length === 0 && (
            <p className="px-4 py-1 text-xs text-muted-foreground">No DMs yet</p>
          )}
        </nav>
      </aside>

      {composing && <ComposeDialog onClose={() => setComposing(false)} />}
    </>
  )
}

function RoomItem({ room, isActive, onClick }: { room: Room; isActive: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={`w-full flex items-center gap-2 px-4 py-1.5 text-sm text-left hover:bg-muted/60
        ${isActive ? 'bg-muted font-medium' : ''}`}
    >
      <span className="text-muted-foreground">#</span>
      <span className="flex-1 truncate">{room.name}</span>
      <UnreadBadge room={room} />
    </button>
  )
}

function DmItem({ room, isActive, onClick }: { room: Room; isActive: boolean; onClick: () => void }) {
  const name = room.otherUserDisplayName ?? room.name
  const avatar = room.otherUserAvatarUrl

  return (
    <button
      onClick={onClick}
      className={`w-full flex items-center gap-2 px-4 py-1.5 text-sm text-left hover:bg-muted/60
        ${isActive ? 'bg-muted font-medium' : ''}`}
    >
      {avatar ? (
        <img src={avatar} alt={name} className="w-5 h-5 rounded-full flex-shrink-0 object-cover" />
      ) : (
        <div className="w-5 h-5 rounded-full bg-primary/20 flex-shrink-0 flex items-center justify-center text-xs font-semibold">
          {name.charAt(0).toUpperCase()}
        </div>
      )}
      <span className="flex-1 truncate">{name}</span>
      <UnreadBadge room={room} />
    </button>
  )
}

function UnreadBadge({ room }: { room: Room }) {
  if (room.unreadCount === 0) return null
  return (
    <span className={`ml-auto rounded-full px-1.5 py-0.5 text-xs font-semibold
      ${room.hasMention ? 'bg-destructive text-destructive-foreground' : 'bg-primary text-primary-foreground'}`}>
      {room.unreadCount > 99 ? '99+' : room.unreadCount}
    </span>
  )
}
