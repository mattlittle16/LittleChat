import { useEffect, useRef, useState } from 'react'
import { Sidebar } from './Sidebar'
import { MessageList } from '../chat/MessageList'
import { MessageInput } from '../chat/MessageInput'
import { TypingIndicator } from '../chat/TypingIndicator'
import { SearchModal } from '../search/SearchModal'
import { MentionToastContainer } from '../chat/MentionToast'
import { useRoomStore } from '../../stores/roomStore'
import { useSignalR } from '../../hooks/useSignalR'

export function ChatLayout() {
  const { activeRoomId, rooms } = useRoomStore()
  const { status } = useSignalR(activeRoomId)
  const [searchOpen, setSearchOpen] = useState(false)
  const [dmMenuOpen, setDmMenuOpen] = useState(false)
  const [dmConfirming, setDmConfirming] = useState(false)
  const dmMenuRef = useRef<HTMLDivElement>(null)

  const activeRoom = rooms.find(r => r.id === activeRoomId)
  const roomName = activeRoom
    ? activeRoom.isDm
      ? activeRoom.otherUserDisplayName ?? activeRoom.name
      : `# ${activeRoom.name}`
    : null

  const isDisconnected = status === 'disconnected' || status === 'reconnecting'

  // Close DM menu when clicking outside
  useEffect(() => {
    if (!dmMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (dmMenuRef.current && !dmMenuRef.current.contains(e.target as Node)) {
        setDmMenuOpen(false)
        setDmConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [dmMenuOpen])

  async function handleDeleteDm() {
    if (!activeRoomId) return
    setDmMenuOpen(false)
    setDmConfirming(false)
    await useRoomStore.getState().deleteRoom(activeRoomId)
  }

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
    <div className="flex h-screen overflow-hidden relative">
      {/* Subtle background shapes — main app */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
        <div style={{
          position: 'absolute',
          top: '-15%',
          right: '-5%',
          width: '40%',
          paddingBottom: '34%',
          borderRadius: '55% 45% 40% 60% / 50% 38% 62% 50%',
          background: 'radial-gradient(ellipse at 45% 45%, hsl(243 72% 60% / 0.10), transparent 68%)',
          filter: 'blur(52px)',
          transform: 'rotate(10deg)',
        }} />
        <div style={{
          position: 'absolute',
          bottom: '-8%',
          left: '20%',
          width: '38%',
          paddingBottom: '28%',
          borderRadius: '40% 60% 55% 45% / 58% 44% 56% 42%',
          background: 'radial-gradient(ellipse at 55% 55%, hsl(260 65% 65% / 0.08), transparent 68%)',
          filter: 'blur(56px)',
          transform: 'rotate(-8deg)',
        }} />
      </div>

      <Sidebar />

      <div className="flex flex-1 flex-col min-w-0 relative">
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

            {/* DM actions menu — only shown when viewing a DM */}
            {activeRoom?.isDm && (
              <div className="relative" ref={dmMenuRef}>
                <button
                  onClick={() => { setDmMenuOpen(o => !o); setDmConfirming(false) }}
                  title="DM options"
                  className="rounded px-1.5 py-1 text-sm text-muted-foreground hover:text-foreground hover:bg-muted/60"
                >
                  ⋯
                </button>
                {dmMenuOpen && (
                  <div className="absolute right-0 top-full mt-1 z-20 w-44 rounded border bg-background shadow-md text-sm">
                    {!dmConfirming ? (
                      <button
                        onClick={() => setDmConfirming(true)}
                        className="w-full px-3 py-2 text-left text-destructive hover:bg-muted/60"
                      >
                        Delete conversation
                      </button>
                    ) : (
                      <div className="px-3 py-2 flex flex-col gap-1">
                        <span className="text-xs text-muted-foreground">Are you sure?</span>
                        <div className="flex gap-1 mt-1">
                          <button
                            onClick={handleDeleteDm}
                            className="flex-1 rounded bg-destructive px-2 py-1 text-xs text-destructive-foreground hover:opacity-90"
                          >
                            Yes, delete
                          </button>
                          <button
                            onClick={() => setDmConfirming(false)}
                            className="flex-1 rounded bg-muted px-2 py-1 text-xs text-foreground hover:bg-muted/60"
                          >
                            No
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
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
            <TypingIndicator roomId={activeRoomId} />
            <MessageInput roomId={activeRoomId} disabled={isDisconnected} />
          </>
        ) : (
          <div className="flex flex-1 items-center justify-center text-muted-foreground text-sm">
            Select a room to start chatting.
          </div>
        )}
      </div>

      {searchOpen && <SearchModal onClose={() => setSearchOpen(false)} />}
      <MentionToastContainer />
    </div>
  )
}
