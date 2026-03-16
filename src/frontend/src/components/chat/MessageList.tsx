import { useEffect, useLayoutEffect, useMemo, useRef } from 'react'
import { useMessageStore, getRoomMessages } from '../../stores/messageStore'
import { useOutboxStore } from '../../stores/outboxStore'
import { useRoomStore } from '../../stores/roomStore'
import { MessageItem } from './MessageItem'

interface MessageListProps {
  roomId: string
  selectedMessageId?: string | null
  deleteConfirmPending?: boolean
  editingMessageId?: string | null
}

export function MessageList({ roomId, selectedMessageId = null, deleteConfirmPending = false, editingMessageId = null }: MessageListProps) {
  const { messages, hasMoreByRoom, loadPage } = useMessageStore()
  const { messages: outbox } = useOutboxStore()
  const listRef = useRef<HTMLDivElement>(null)
  const contentRef = useRef<HTMLDivElement>(null)
  const isNearBottomRef = useRef(true)
  const isPaginatingRef = useRef(false)
  const scrollHeightBeforeRef = useRef(0)

  const roomMessages = useMemo(
    () => getRoomMessages(messages, roomId),
    [messages, roomId]
  )
  const roomOutbox = useMemo(
    () => outbox.filter(m => m.roomId === roomId),
    [outbox, roomId]
  )
  const hasMore = hasMoreByRoom.get(roomId) ?? false

  // Room switch: reset state and scroll to bottom
  useEffect(() => {
    isNearBottomRef.current = true
    isPaginatingRef.current = false
    loadPage(roomId)
  }, [roomId, loadPage])

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

  // Auto-scroll to bottom when new messages arrive — only when near bottom
  useEffect(() => {
    if (!isNearBottomRef.current) return
    const list = listRef.current
    if (!list) return
    // Double-RAF: first frame flushes React DOM, second lets browser complete layout.
    // Re-check isNearBottomRef inside the RAF — the user may have scrolled up in the gap.
    requestAnimationFrame(() => requestAnimationFrame(() => {
      if (!isNearBottomRef.current) return
      list.scrollTop = list.scrollHeight
      isNearBottomRef.current = true
    }))
    if (!document.hidden) {
      const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
      if (room && room.unreadCount > 0) {
        useRoomStore.getState().markRead(roomId)
      }
    }
  }, [roomMessages.length, roomOutbox.length, roomId])

  // Re-scroll when content grows (images/videos/media finish loading) if near bottom
  useEffect(() => {
    const content = contentRef.current
    const list = listRef.current
    if (!content || !list) return
    const observer = new ResizeObserver(() => {
      if (isNearBottomRef.current) {
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

  // Track near-bottom; clear unread on scroll to bottom; trigger pagination near top
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

      // Trigger pagination when near top — snapshot scrollHeight before load so
      // useLayoutEffect can restore position after React commits the new messages.
      if (el.scrollTop < 100 && hasMore && !isPaginatingRef.current) {
        const oldest = roomMessages[0]
        if (oldest) {
          isPaginatingRef.current = true
          scrollHeightBeforeRef.current = el.scrollHeight
          loadPage(roomId, { createdAt: oldest.createdAt, id: oldest.id })
        }
      }
    }

    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [roomId, hasMore, roomMessages, loadPage])

  return (
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
            />
          )
        })}

        {roomOutbox.map(msg => (
          <MessageItem key={msg.clientId} message={msg} />
        ))}
      </div>
    </div>
  )
}
