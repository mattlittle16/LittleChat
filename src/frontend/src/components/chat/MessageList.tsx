import { useEffect, useMemo, useRef } from 'react'
import { useVirtualizer } from '@tanstack/react-virtual'
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
  const isNearBottomRef = useRef(true)
  const isPaginatingRef = useRef(false)
  const prevCountRef = useRef(0)

  const roomMessages = useMemo(
    () => getRoomMessages(messages, roomId),
    [messages, roomId]
  )
  const roomOutbox = useMemo(
    () => outbox.filter(m => m.roomId === roomId),
    [outbox, roomId]
  )
  const hasMore = hasMoreByRoom.get(roomId) ?? false

  const virtualizer = useVirtualizer({
    count: roomMessages.length,
    getScrollElement: () => listRef.current,
    estimateSize: () => 60,
    measureElement: (el) => el?.getBoundingClientRect().height ?? 60,
    overscan: 5,
  })

  // Room switch: reset state and scroll immediately to bottom
  useEffect(() => {
    isNearBottomRef.current = true
    isPaginatingRef.current = false
    const el = listRef.current
    if (el) el.scrollTop = el.scrollHeight
    loadPage(roomId)
  }, [roomId, loadPage])

  // Pagination: restore scroll position after older messages are prepended (T021)
  // Uses scrollToIndex so the virtualizer's own coordinate system handles the jump,
  // rather than a height-delta that's unreliable before items are measured.
  useEffect(() => {
    if (!isPaginatingRef.current) return
    isPaginatingRef.current = false
    const numNew = roomMessages.length - prevCountRef.current
    if (numNew > 0) {
      requestAnimationFrame(() => {
        virtualizer.scrollToIndex(numNew, { align: 'start', behavior: 'auto' })
      })
    }
  }, [roomMessages.length, virtualizer])

  // Auto-scroll to bottom when new messages arrive — only when near bottom and not paginating (T020)
  useEffect(() => {
    if (isPaginatingRef.current) return
    if (!isNearBottomRef.current) return

    if (roomMessages.length > 0) {
      requestAnimationFrame(() => requestAnimationFrame(() => {
        const el = listRef.current
        if (el) el.scrollTop = el.scrollHeight
        isNearBottomRef.current = true
      }))
    }
    if (!document.hidden) {
      const room = useRoomStore.getState().rooms.find(r => r.id === roomId)
      if (room && room.unreadCount > 0) {
        useRoomStore.getState().markRead(roomId)
      }
    }
  }, [roomMessages.length, roomOutbox.length, roomId])

  // Re-scroll when virtualizer total size changes (items measured, media loaded) — mirrors old ResizeObserver
  const totalSize = virtualizer.getTotalSize()
  useEffect(() => {
    if (!isNearBottomRef.current) return
    const el = listRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [totalSize])

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

  // Track near-bottom on scroll; clear unread badge on scroll to bottom; trigger pagination near top
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

      // Trigger pagination when near top (T021)
      if (el.scrollTop < 100 && hasMore && !isPaginatingRef.current) {
        const oldest = roomMessages[0]
        if (oldest) {
          isPaginatingRef.current = true
          prevCountRef.current = roomMessages.length
          loadPage(roomId, { createdAt: oldest.createdAt, id: oldest.id })
        }
      }
    }

    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [roomId, hasMore, roomMessages, loadPage])

  const virtualItems = virtualizer.getVirtualItems()

  return (
    <div ref={listRef} className="flex-1 overflow-y-auto">
      {hasMore && (
        <p className="text-center text-xs text-muted-foreground py-2">Loading older messages…</p>
      )}

      {/* Virtualized message rows — only visible rows exist in DOM (T019) */}
      <div
        style={{ height: virtualizer.getTotalSize(), position: 'relative' }}
        className="max-w-5xl ml-8"
      >
        {virtualItems.map(vItem => {
          const msg = roomMessages[vItem.index]
          const prev = vItem.index > 0 ? roomMessages[vItem.index - 1] : null
          const isGrouped = prev != null
            && msg.author.id === prev.author.id
            && new Date(msg.createdAt).toDateString() === new Date(prev.createdAt).toDateString()
            && new Date(msg.createdAt).getTime() - new Date(prev.createdAt).getTime() < 30_000
          return (
            <div
              key={vItem.key}
              ref={virtualizer.measureElement}
              data-index={vItem.index}
              className="hover:z-10"
              style={{
                position: 'absolute',
                top: 0,
                left: 0,
                width: '100%',
                transform: `translateY(${vItem.start}px)`,
              }}
            >
              <MessageItem
                message={msg}
                isGrouped={isGrouped}
                isKeyboardSelected={msg.id === selectedMessageId}
                deleteConfirmPending={deleteConfirmPending}
                shouldStartEditing={msg.id === editingMessageId}
              />
            </div>
          )
        })}
      </div>

      {/* Outbox messages — always at bottom, not virtualized (typically 1–3 pending items) */}
      <div className="max-w-5xl ml-8 pb-2">
        {roomOutbox.map(msg => (
          <MessageItem key={msg.clientId} message={msg} />
        ))}
      </div>
    </div>
  )
}
