import { useEffect, useLayoutEffect, useMemo, useRef } from 'react'
import { useMessageStore, getRoomMessages } from '../../stores/messageStore'
import { useOutboxStore } from '../../stores/outboxStore'
import { useRoomStore } from '../../stores/roomStore'
import { MessageItem } from './MessageItem'

const SCROLL_TO_MAX_ATTEMPTS = 5

interface MessageListProps {
  roomId: string
  selectedMessageId?: string | null
  deleteConfirmPending?: boolean
  editingMessageId?: string | null
  onSetPendingQuote?: (quoteData: { messageId: string; authorDisplayName: string; contentSnapshot: string }) => void
}

export function MessageList({ roomId, selectedMessageId = null, deleteConfirmPending = false, editingMessageId = null, onSetPendingQuote }: MessageListProps) {
  const { messages, hasMoreByRoom, hasNewerByRoom, loadPage, loadAroundMessage, loadNewerPage } = useMessageStore()
  const { messages: outbox } = useOutboxStore()
  const listRef = useRef<HTMLDivElement>(null)
  const contentRef = useRef<HTMLDivElement>(null)
  const isNearBottomRef = useRef(true)
  const isPaginatingRef = useRef(false)
  const scrollHeightBeforeRef = useRef(0)
  const scrollToAttemptsRef = useRef(0)

  const scrollToMessageId = useMessageStore((s) => s.scrollToMessageId)
  const setScrollToMessageId = useMessageStore((s) => s.setScrollToMessageId)

  const roomMessages = useMemo(
    () => getRoomMessages(messages, roomId),
    [messages, roomId]
  )
  const roomOutbox = useMemo(
    () => outbox.filter(m => m.roomId === roomId),
    [outbox, roomId]
  )
  const hasMore = hasMoreByRoom.get(roomId) ?? false
  const hasNewer = hasNewerByRoom.get(roomId) ?? false

  // Room switch: reset state. If a notification click pre-registered a pending aroundId,
  // load that context window instead of the default latest page — this prevents the race
  // condition where loadPage and loadAroundMessage fire simultaneously.
  useEffect(() => {
    isPaginatingRef.current = false
    useMessageStore.setState(s => {
      const nextHasNewer = new Map(s.hasNewerByRoom)
      nextHasNewer.delete(roomId)
      return { hasNewerByRoom: nextHasNewer }
    })

    const pending = useMessageStore.getState().pendingAroundByRoom.get(roomId)
    if (pending) {
      // Context mode: start with isNearBottom=false so the ResizeObserver and
      // scroll handler don't fire loadNewerPage before scroll-to-message runs.
      isNearBottomRef.current = false
      useMessageStore.setState(s => {
        const next = new Map(s.pendingAroundByRoom)
        next.delete(roomId)
        return { pendingAroundByRoom: next }
      })
      loadAroundMessage(roomId, pending).then(() => {
        setScrollToMessageId(pending)
      })
    } else {
      isNearBottomRef.current = true
      loadPage(roomId)
    }
  }, [roomId, loadPage, loadAroundMessage, setScrollToMessageId])

  // Pagination scroll restoration: runs after React commits new messages to the DOM,
  // before the browser paints — so the position correction is invisible to the user.
  useLayoutEffect(() => {
    if (!isPaginatingRef.current) return
    const el = listRef.current
    if (!el || scrollHeightBeforeRef.current === 0) return
    const delta = el.scrollHeight - scrollHeightBeforeRef.current
    if (delta > 0) el.scrollTop += delta
    scrollHeightBeforeRef.current = 0
    isPaginatingRef.current = false
  }, [roomMessages.length])

  // Auto-scroll to bottom when new messages arrive — only when near bottom and not in context mode
  useEffect(() => {
    if (!isNearBottomRef.current) return
    if (hasNewer) return  // In context mode — don't auto-scroll to absolute bottom
    const list = listRef.current
    if (!list) return
    // Double-RAF: first frame flushes React DOM, second lets browser complete layout.
    // Re-check isNearBottomRef inside the RAF — the user may have scrolled up in the gap.
    requestAnimationFrame(() => requestAnimationFrame(() => {
      if (!isNearBottomRef.current) return
      if (useMessageStore.getState().hasNewerByRoom.get(roomId)) return
      list.scrollTop = list.scrollHeight
      isNearBottomRef.current = true
    }))
    if (!document.hidden) {
      const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
      if (room && room.unreadCount > 0) {
        useRoomStore.getState().markRead(roomId)
      }
    }
  }, [roomMessages.length, roomOutbox.length, roomId, hasNewer])

  // Re-scroll when content grows (images/videos/media finish loading) if near bottom
  useEffect(() => {
    const content = contentRef.current
    const list = listRef.current
    if (!content || !list) return
    const observer = new ResizeObserver(() => {
      if (isNearBottomRef.current && !useMessageStore.getState().hasNewerByRoom.get(roomId)) {
        list.scrollTop = list.scrollHeight
      }
    })
    observer.observe(content)
    return () => observer.disconnect()
  }, [roomId])

  // When the tab becomes visible again, clear unread if at bottom
  useEffect(() => {
    function onVisibilityChange() {
      if (!document.hidden && isNearBottomRef.current) {
        const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
        if (room && room.unreadCount > 0) {
          useRoomStore.getState().markRead(roomId)
        }
      }
    }
    document.addEventListener('visibilitychange', onVisibilityChange)
    return () => document.removeEventListener('visibilitychange', onVisibilityChange)
  }, [roomId])

  // Track near-bottom; clear unread on scroll to bottom; trigger pagination near top/bottom
  useEffect(() => {
    const el = listRef.current
    if (!el) return

    function onScroll() {
      if (!el) return
      const wasNearBottom = isNearBottomRef.current
      isNearBottomRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80

      if (!wasNearBottom && isNearBottomRef.current) {
        const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
        if (room && room.unreadCount > 0) {
          useRoomStore.getState().markRead(roomId)
        }
      }

      // Trigger older pagination when near top
      if (el.scrollTop < 100 && hasMore && !isPaginatingRef.current) {
        const oldest = roomMessages[0]
        if (oldest) {
          isPaginatingRef.current = true
          scrollHeightBeforeRef.current = el.scrollHeight
          loadPage(roomId, { createdAt: oldest.createdAt, id: oldest.id })
        }
      }

      // Trigger newer pagination when near bottom in context mode
      if (isNearBottomRef.current && hasNewer && !isPaginatingRef.current) {
        const newest = roomMessages[roomMessages.length - 1]
        if (newest) {
          isPaginatingRef.current = true
          loadNewerPage(roomId, { createdAt: newest.createdAt, id: newest.id }).then(() => {
            isPaginatingRef.current = false
          })
        }
      }
    }

    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [roomId, hasMore, hasNewer, roomMessages, loadPage, loadNewerPage])

  // Scroll to a specific message after notification click, paginating if needed
  useEffect(() => {
    if (!scrollToMessageId) {
      scrollToAttemptsRef.current = 0
      return
    }

    // Wait for the initial page to load before attempting scroll.
    // If we check immediately after room navigation, messages may not be in
    // the store yet, causing the effect to give up before the DOM is ready.
    if (roomMessages.length === 0) return

    const el = document.querySelector(`[data-message-id="${scrollToMessageId}"]`)
    if (el) {
      el.scrollIntoView({ behavior: 'instant', block: 'center' })
      setScrollToMessageId(null)
      scrollToAttemptsRef.current = 0
      return
    }

    // Message not yet in DOM — load an older page and retry
    if (hasMore && scrollToAttemptsRef.current < SCROLL_TO_MAX_ATTEMPTS) {
      scrollToAttemptsRef.current++
      const oldest = roomMessages[0]
      if (oldest) {
        isPaginatingRef.current = true
        scrollHeightBeforeRef.current = listRef.current?.scrollHeight ?? 0
        loadPage(roomId, { createdAt: oldest.createdAt, id: oldest.id })
      }
    } else {
      // Message not found after exhausting history — give up gracefully
      setScrollToMessageId(null)
      scrollToAttemptsRef.current = 0
    }
  }, [scrollToMessageId, roomMessages, hasMore, roomId, loadPage, setScrollToMessageId])

  function handleJumpToNewest() {
    const store = useMessageStore.getState()
    store.clearRoom(roomId)
    isNearBottomRef.current = true
    store.loadPage(roomId).then(() => {
      const list = listRef.current
      if (list) list.scrollTop = list.scrollHeight
    })
  }

  return (
    <div className="flex-1 flex flex-col overflow-hidden relative">
      <div ref={listRef} className="flex-1 overflow-y-auto py-2">
        <div ref={contentRef} className="max-w-5xl ml-8">
          {hasMore && (
            <p className="text-center text-xs text-muted-foreground py-2">Loading older messages…</p>
          )}

          {roomMessages.map((msg, i) => {
            const prev = i > 0 ? roomMessages[i - 1] : null
            const isGrouped = prev != null
              && msg.author.id === prev.author.id
              && new Date(msg.createdAt).toDateString() === new Date(prev.createdAt).toDateString()
              && new Date(msg.createdAt).getTime() - new Date(prev.createdAt).getTime() < 30_000
            return (
              <MessageItem
                key={msg.id}
                message={msg}
                isGrouped={isGrouped}
                isKeyboardSelected={msg.id === selectedMessageId}
                deleteConfirmPending={deleteConfirmPending}
                shouldStartEditing={msg.id === editingMessageId}
                onSetPendingQuote={onSetPendingQuote}
              />
            )
          })}

          {roomOutbox.map(msg => (
            <MessageItem key={msg.clientId} message={msg} />
          ))}

          {hasNewer && (
            <p className="text-center text-xs text-muted-foreground py-2">Loading newer messages…</p>
          )}
        </div>
      </div>

      {hasNewer && (
        <div className="absolute bottom-6 left-1/2 -translate-x-1/2 z-10 pointer-events-none">
          <button
            onClick={handleJumpToNewest}
            className="pointer-events-auto flex items-center gap-1.5 px-4 py-1.5 rounded-full bg-primary text-primary-foreground text-xs font-medium shadow-lg hover:bg-primary/90 transition-colors"
          >
            Jump to Newest ↓
          </button>
        </div>
      )}
    </div>
  )
}
