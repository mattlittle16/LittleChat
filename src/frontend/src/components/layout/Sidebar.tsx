import { useEffect, useRef, useState } from 'react'
import { BellOff } from 'lucide-react'
import { useRoomStore } from '../../stores/roomStore'
import { usePresenceStore } from '../../stores/presenceStore'
import { useNotificationPreferencesStore } from '../../stores/notificationPreferencesStore'
import { api } from '../../services/apiClient'
import { ComposeDialog } from './ComposeDialog'
import { ThemeToggle } from '../ThemeToggle'
import logoSvg from '../../assets/logo.svg'
import type { ConversationOverrideLevel, Room } from '../../types'

const sidebarStyle = {
  background: 'hsl(var(--sidebar-bg))',
  color: 'hsl(var(--sidebar-fg))',
}

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
      <aside className="flex h-full w-60 flex-col" style={sidebarStyle}>
        {/* Logo */}
        <div className="flex items-center gap-2.5 px-4 py-3" style={{ borderBottom: '1px solid hsl(var(--sidebar-muted-fg) / 0.2)' }}>
          <img src={logoSvg} alt="" className="h-6 w-6 flex-shrink-0" />
          <span className="font-semibold text-sm tracking-tight" style={{ color: 'hsl(var(--sidebar-fg))' }}>
            MattLab Chat
          </span>
        </div>

        {/* Channels section */}
        <div className="flex items-center justify-between px-4 py-2 mt-1">
          <span
            className="text-xs font-semibold uppercase tracking-wider"
            style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          >
            Channels
          </span>
          <button
            onClick={() => setCreating(c => !c)}
            className="text-lg leading-none transition-colors"
            style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
            onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
            onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
            title="Create Channel"
          >
            +
          </button>
        </div>

        {creating && (
          <form onSubmit={handleCreateRoom} className="px-3 pb-2 flex flex-col gap-1.5">
            <input
              autoFocus
              value={newRoomName}
              onChange={e => setNewRoomName(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Escape') {
                  setCreating(false)
                  setNewRoomName('')
                }
              }}
              placeholder="Channel name"
              className="w-full rounded border px-2 py-1 text-sm focus:outline-none focus:ring-1"
              style={{
                background: 'hsl(var(--sidebar-active-bg))',
                borderColor: 'hsl(var(--sidebar-muted-fg) / 0.4)',
                color: 'hsl(var(--sidebar-fg))',
              }}
            />
            <div className="flex gap-2">
              <button
                type="submit"
                className="rounded px-2 py-1 text-xs font-medium"
                style={{
                  background: 'hsl(var(--primary))',
                  color: 'hsl(var(--primary-foreground))',
                }}
              >
                Create
              </button>
              <button
                type="button"
                onClick={() => { setCreating(false); setNewRoomName('') }}
                className="rounded px-2 py-1 text-xs font-medium"
                style={{
                  color: 'hsl(var(--sidebar-muted-fg))',
                }}
              >
                Cancel
              </button>
            </div>
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
            <p className="px-4 py-1 text-xs" style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>
              No channels yet
            </p>
          )}

          {/* DMs section */}
          <div className="flex items-center justify-between px-4 py-2 mt-3">
            <span
              className="text-xs font-semibold uppercase tracking-wider"
              style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
            >
              Direct Messages
            </span>
            <button
              onClick={() => setComposing(true)}
              className="text-lg leading-none transition-colors"
              style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
              onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
              onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
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
            <p className="px-4 py-1 text-xs" style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>
              No DMs yet
            </p>
          )}
        </nav>

        {/* Bottom user/theme area */}
        <div
          className="flex items-center justify-end px-3 py-2"
          style={{ borderTop: '1px solid hsl(var(--sidebar-muted-fg) / 0.2)' }}
        >
          <ThemeToggle />
        </div>
      </aside>

      {composing && <ComposeDialog onClose={() => setComposing(false)} />}
    </>
  )
}

function RoomItem({ room, isActive, onClick }: { room: Room; isActive: boolean; onClick: () => void }) {
  const [confirming, setConfirming] = useState(false)
  const [notifMenuOpen, setNotifMenuOpen] = useState(false)
  const confirmRef = useRef<HTMLDivElement>(null)
  const notifMenuRef = useRef<HTMLDivElement>(null)
  const setOverride = useNotificationPreferencesStore(s => s.setOverride)
  const overrides = useNotificationPreferencesStore(s => s.overrides)
  const currentOverride: ConversationOverrideLevel | undefined = overrides[room.id]

  // Close confirm popover when clicking outside
  useEffect(() => {
    if (!confirming) return
    function handleClickOutside(e: MouseEvent) {
      if (confirmRef.current && !confirmRef.current.contains(e.target as Node)) {
        setConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [confirming])

  // Close notif menu when clicking outside
  useEffect(() => {
    if (!notifMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (notifMenuRef.current && !notifMenuRef.current.contains(e.target as Node)) {
        setNotifMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [notifMenuOpen])

  async function handleConfirmDelete() {
    setConfirming(false)
    await useRoomStore.getState().deleteRoom(room.id)
  }

  return (
    <div className="relative group">
      <button
        onClick={onClick}
        className="w-full flex items-center gap-2 px-4 py-1.5 text-sm text-left transition-colors"
        style={{
          background: isActive ? 'hsl(var(--sidebar-active-bg))' : 'transparent',
          color: isActive ? 'hsl(var(--sidebar-fg))' : 'hsl(var(--sidebar-muted-fg))',
          fontWeight: isActive ? '500' : undefined,
        }}
        onMouseEnter={e => {
          if (!isActive) e.currentTarget.style.background = 'hsl(var(--sidebar-active-bg) / 0.5)'
        }}
        onMouseLeave={e => {
          if (!isActive) e.currentTarget.style.background = 'transparent'
        }}
      >
        <span style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>#</span>
        <span className="flex-1 truncate">{room.name}</span>
        {currentOverride && (
          <BellOff className="w-3 h-3 flex-shrink-0 opacity-60 group-hover:hidden" style={{ color: 'hsl(var(--sidebar-muted-fg))' }} />
        )}
        <UnreadBadge room={room} />
      </button>

      {/* Hover-reveal notification override button */}
      <div className="absolute right-7 top-1/2 -translate-y-1/2 hidden group-hover:flex" ref={notifMenuRef}>
        <button
          onClick={(e) => { e.stopPropagation(); setNotifMenuOpen(o => !o) }}
          className="flex items-center justify-center w-5 h-5 rounded transition-colors"
          style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          title="Notification settings"
        >
          <BellOff className="w-3 h-3" />
        </button>
        {notifMenuOpen && (
          <div
            className="absolute right-0 top-full mt-1 z-30 w-44 rounded border bg-background shadow-md text-xs"
            style={{ borderColor: 'hsl(var(--border))' }}
            onClick={e => e.stopPropagation()}
          >
            {(['follow_global', 'all_messages', 'mentions_only', 'muted'] as const).map(level => {
                const isActive = currentOverride === level || (level === 'follow_global' && !currentOverride)
                return (
                  <button
                    key={level}
                    onClick={() => { setOverride(room.id, level); setNotifMenuOpen(false) }}
                    className="w-full px-3 py-1.5 text-left hover:bg-muted/60 flex items-center gap-2"
                    style={{ color: 'hsl(var(--foreground))' }}
                  >
                    <span className="w-3 text-center text-green-500">{isActive ? '✓' : ''}</span>
                    {level === 'follow_global' ? 'Follow Global Setting' :
                     level === 'all_messages' ? 'All Messages' :
                     level === 'mentions_only' ? '@Mentions Only' : 'Muted'}
                  </button>
                )
              })}
          </div>
        )}
      </div>

      {/* Hover-reveal delete button */}
      <button
        onClick={(e) => { e.stopPropagation(); setConfirming(true) }}
        className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center
          justify-center w-5 h-5 rounded transition-colors"
        style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
        onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--destructive))')}
        onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
        title="Delete room"
        aria-label="Delete room"
      >
        &#x2715;
      </button>

      {/* Two-step confirm popover */}
      {confirming && (
        <div
          ref={confirmRef}
          className="absolute right-0 top-full z-20 mt-1 flex items-center gap-1 rounded border px-2 py-1 shadow-md text-xs"
          style={{
            background: 'hsl(var(--background))',
            borderColor: 'hsl(var(--border))',
            color: 'hsl(var(--foreground))',
          }}
        >
          <span style={{ color: 'hsl(var(--muted-foreground))' }}>Delete?</span>
          <button
            onClick={handleConfirmDelete}
            className="rounded px-1.5 py-0.5 hover:opacity-90"
            style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="rounded px-1.5 py-0.5"
            style={{ background: 'hsl(var(--muted))', color: 'hsl(var(--foreground))' }}
          >
            No
          </button>
        </div>
      )}
    </div>
  )
}

function DmItem({ room, isActive, onClick }: { room: Room; isActive: boolean; onClick: () => void }) {
  const name = room.otherUserDisplayName ?? room.name
  const avatar = room.otherUserAvatarUrl
  const online = usePresenceStore(s => room.otherUserId ? s.isOnline(room.otherUserId) : false)
  const [confirming, setConfirming] = useState(false)
  const [notifMenuOpen, setNotifMenuOpen] = useState(false)
  const confirmRef = useRef<HTMLDivElement>(null)
  const notifMenuRef = useRef<HTMLDivElement>(null)
  const setOverride = useNotificationPreferencesStore(s => s.setOverride)
  const overrides = useNotificationPreferencesStore(s => s.overrides)
  const currentOverride: ConversationOverrideLevel | undefined = overrides[room.id]

  // Close confirm popover when clicking outside
  useEffect(() => {
    if (!confirming) return
    function handleClickOutside(e: MouseEvent) {
      if (confirmRef.current && !confirmRef.current.contains(e.target as Node)) {
        setConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [confirming])

  // Close notif menu when clicking outside
  useEffect(() => {
    if (!notifMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (notifMenuRef.current && !notifMenuRef.current.contains(e.target as Node)) {
        setNotifMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [notifMenuOpen])

  async function handleConfirmDelete() {
    setConfirming(false)
    await useRoomStore.getState().deleteRoom(room.id)
  }

  return (
    <div className="relative group">
      <button
        onClick={onClick}
        className="w-full flex items-center gap-2 px-4 py-1.5 text-sm text-left transition-colors"
        style={{
          background: isActive ? 'hsl(var(--sidebar-active-bg))' : 'transparent',
          color: isActive ? 'hsl(var(--sidebar-fg))' : 'hsl(var(--sidebar-muted-fg))',
          fontWeight: isActive ? '500' : undefined,
        }}
        onMouseEnter={e => {
          if (!isActive) e.currentTarget.style.background = 'hsl(var(--sidebar-active-bg) / 0.5)'
        }}
        onMouseLeave={e => {
          if (!isActive) e.currentTarget.style.background = 'transparent'
        }}
      >
        <div className="relative flex-shrink-0">
          {avatar ? (
            <img src={avatar} alt={name} className="w-5 h-5 rounded-full object-cover" />
          ) : (
            <div
              className="w-5 h-5 rounded-full flex items-center justify-center text-xs font-semibold"
              style={{ background: 'hsl(var(--sidebar-active-bg))', color: 'hsl(var(--sidebar-fg))' }}
            >
              {name.charAt(0).toUpperCase()}
            </div>
          )}
          <span
            className={`absolute -bottom-0.5 -right-0.5 w-2 h-2 rounded-full ${online ? 'bg-green-400' : 'bg-red-400'}`}
            style={{ boxShadow: '0 0 0 2px hsl(var(--sidebar-bg))' }}
          />
        </div>
        <span className="flex-1 truncate">{name}</span>
        {currentOverride && (
          <BellOff className="w-3 h-3 flex-shrink-0 opacity-60 group-hover:hidden" style={{ color: 'hsl(var(--sidebar-muted-fg))' }} />
        )}
        <UnreadBadge room={room} />
      </button>

      {/* Hover-reveal notification override button */}
      <div className="absolute right-7 top-1/2 -translate-y-1/2 hidden group-hover:flex" ref={notifMenuRef}>
        <button
          onClick={(e) => { e.stopPropagation(); setNotifMenuOpen(o => !o) }}
          className="flex items-center justify-center w-5 h-5 rounded transition-colors"
          style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          title="Notification settings"
        >
          <BellOff className="w-3 h-3" />
        </button>
        {notifMenuOpen && (
          <div
            className="absolute right-0 top-full mt-1 z-30 w-44 rounded border bg-background shadow-md text-xs"
            style={{ borderColor: 'hsl(var(--border))' }}
            onClick={e => e.stopPropagation()}
          >
            {(['follow_global', 'all_messages', 'mentions_only', 'muted'] as const).map(level => {
                const isActive = currentOverride === level || (level === 'follow_global' && !currentOverride)
                return (
                  <button
                    key={level}
                    onClick={() => { setOverride(room.id, level); setNotifMenuOpen(false) }}
                    className="w-full px-3 py-1.5 text-left hover:bg-muted/60 flex items-center gap-2"
                    style={{ color: 'hsl(var(--foreground))' }}
                  >
                    <span className="w-3 text-center text-green-500">{isActive ? '✓' : ''}</span>
                    {level === 'follow_global' ? 'Follow Global Setting' :
                     level === 'all_messages' ? 'All Messages' :
                     level === 'mentions_only' ? '@Mentions Only' : 'Muted'}
                  </button>
                )
              })}
          </div>
        )}
      </div>

      {/* Hover-reveal delete button */}
      <button
        onClick={(e) => { e.stopPropagation(); setConfirming(true) }}
        className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center
          justify-center w-5 h-5 rounded transition-colors"
        style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
        onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--destructive))')}
        onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
        title="Delete conversation"
        aria-label="Delete conversation"
      >
        &#x2715;
      </button>

      {/* Two-step confirm popover */}
      {confirming && (
        <div
          ref={confirmRef}
          className="absolute right-0 top-full z-20 mt-1 flex items-center gap-1 rounded border px-2 py-1 shadow-md text-xs"
          style={{
            background: 'hsl(var(--background))',
            borderColor: 'hsl(var(--border))',
            color: 'hsl(var(--foreground))',
          }}
        >
          <span style={{ color: 'hsl(var(--muted-foreground))' }}>Delete?</span>
          <button
            onClick={handleConfirmDelete}
            className="rounded px-1.5 py-0.5 hover:opacity-90"
            style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="rounded px-1.5 py-0.5"
            style={{ background: 'hsl(var(--muted))', color: 'hsl(var(--foreground))' }}
          >
            No
          </button>
        </div>
      )}
    </div>
  )
}

function UnreadBadge({ room }: { room: Room }) {
  if (room.hasMention && room.unreadCount === 0) {
    return (
      <span
        className="ml-auto rounded-full px-1.5 py-0.5 text-xs font-semibold group-hover:hidden"
        style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
      >
        @
      </span>
    )
  }
  if (room.unreadCount === 0) return null
  return (
    <span
      className="ml-auto rounded-full px-1.5 py-0.5 text-xs font-semibold group-hover:hidden"
      style={
        room.hasMention
          ? { background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }
          : { background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }
      }
    >
      {room.unreadCount > 99 ? '99+' : room.unreadCount}
    </span>
  )
}
