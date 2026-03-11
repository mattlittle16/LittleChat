import { useEffect, useMemo, useRef } from 'react'
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
  const sentinelRef = useRef<HTMLDivElement>(null)
  const isNearBottomRef = useRef(true)

  const roomMessages = useMemo(
    () => getRoomMessages(messages, roomId),
    [messages, roomId]
  )
  const roomOutbox = outbox.filter(m => m.roomId === roomId)
  const hasMore = hasMoreByRoom.get(roomId) ?? false

  // Initial load
  useEffect(() => {
    loadPage(roomId)
  }, [roomId, loadPage])

  // Auto-scroll to bottom when new messages arrive (only when near bottom).
  // Also clears the unread badge — but only when the tab is visible, so the tab
  // title count accumulates correctly when the user is in another browser tab.
  useEffect(() => {
    if (isNearBottomRef.current && listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight
      if (!document.hidden) {
        const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
        if (room && room.unreadCount > 0) {
          useRoomStore.getState().markRead(roomId)
        }
      }
    }
  }, [roomMessages.length, roomOutbox.length, roomId])

  // When the tab becomes visible again, clear unread if the user is at the bottom
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

  // Track whether user is near the bottom; clear unread badge when they scroll down
  useEffect(() => {
    const el = listRef.current
    if (!el) return

    function onScroll() {
      if (!el) return
      const wasNearBottom = isNearBottomRef.current
      isNearBottomRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 100

      // Transition to near-bottom: clear unread badge if there are any
      if (!wasNearBottom && isNearBottomRef.current) {
        const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
        if (room && room.unreadCount > 0) {
          useRoomStore.getState().markRead(roomId)
        }
      }
    }

    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [roomId])

  // IntersectionObserver for infinite scroll — load older messages when sentinel visible
  useEffect(() => {
    const sentinel = sentinelRef.current
    const list = listRef.current
    if (!sentinel || !list) return

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting || !hasMore) return
        const oldest = roomMessages[0]
        if (!oldest) return

        // Preserve scroll position after prepend
        const heightBefore = list.scrollHeight

        loadPage(roomId, { createdAt: oldest.createdAt, id: oldest.id }).then(() => {
          queueMicrotask(() => {
            list.scrollTop += list.scrollHeight - heightBefore
          })
        })
      },
      { root: list, rootMargin: '100px' }
    )

    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [roomId, hasMore, roomMessages, loadPage])

  return (
    <div ref={listRef} className="flex-1 overflow-y-auto py-2">
      {/* Sentinel for infinite scroll (prepend older messages) */}
      <div ref={sentinelRef} className="h-px" />

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

      {/* Outbox (pending/sending/failed messages from this room) */}
      {roomOutbox.map(msg => (
        <MessageItem key={msg.clientId} message={msg} />
      ))}
    </div>
  )
}
