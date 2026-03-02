import { useEffect, useState } from 'react'
import { Sidebar } from './Sidebar'
import { MessageList } from '../chat/MessageList'
import { MessageInput } from '../chat/MessageInput'
import { SearchModal } from '../search/SearchModal'
import { useRoomStore } from '../../stores/roomStore'
import { useSignalR } from '../../hooks/useSignalR'

export function ChatLayout() {
  const { activeRoomId, rooms } = useRoomStore()
  const { status } = useSignalR(activeRoomId)
  const [searchOpen, setSearchOpen] = useState(false)

  const activeRoom = rooms.find(r => r.id === activeRoomId)
  const roomName = activeRoom
    ? activeRoom.isDm
      ? activeRoom.otherUserDisplayName ?? activeRoom.name
      : `# ${activeRoom.name}`
    : null

  const isDisconnected = status === 'disconnected' || status === 'reconnecting'

  // Ctrl+K / Cmd+K shortcut to open search
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault()
        setSearchOpen(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar />

      <div className="flex flex-1 flex-col min-w-0">
        {/* Toolbar */}
        <header className="flex h-12 items-center justify-between border-b px-4 flex-shrink-0">
          <span className="font-semibold text-sm truncate">
            {roomName ?? 'LittleChat'}
          </span>

          <div className="flex items-center gap-2">
            {isDisconnected && (
              <span className="text-xs text-muted-foreground animate-pulse">
                {status === 'reconnecting' ? 'Reconnecting…' : 'Disconnected'}
              </span>
            )}
            <button
              onClick={() => setSearchOpen(true)}
              title="Search (Ctrl+K)"
              className="flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-xs text-muted-foreground hover:text-foreground hover:bg-muted/60 transition-colors"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M21 21l-4.35-4.35M17 11A6 6 0 1 1 5 11a6 6 0 0 1 12 0z" />
              </svg>
              Search
              <kbd className="ml-1 text-[10px] opacity-60">⌘K</kbd>
            </button>
          </div>
        </header>

        {/* Main chat area */}
        {activeRoomId ? (
          <>
            <MessageList roomId={activeRoomId} />
            <MessageInput roomId={activeRoomId} disabled={isDisconnected} />
          </>
        ) : (
          <div className="flex flex-1 items-center justify-center text-muted-foreground text-sm">
            Select a room to start chatting.
          </div>
        )}
      </div>

      {searchOpen && <SearchModal onClose={() => setSearchOpen(false)} />}
    </div>
  )
}
