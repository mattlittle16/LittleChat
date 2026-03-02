import { useEffect, useMemo, useRef } from 'react'
import { useMessageStore, getRoomMessages } from '../../stores/messageStore'
import { useOutboxStore } from '../../stores/outboxStore'
import { MessageItem } from './MessageItem'

interface MessageListProps {
  roomId: string
}

export function MessageList({ roomId }: MessageListProps) {
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

  // Auto-scroll to bottom when new messages arrive (only when near bottom)
  useEffect(() => {
    if (isNearBottomRef.current && listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight
    }
  }, [roomMessages.length, roomOutbox.length])

  // Track whether user is near the bottom
  useEffect(() => {
    const el = listRef.current
    if (!el) return

    function onScroll() {
      if (!el) return
      isNearBottomRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 100
    }

    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [])

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

      {roomMessages.map(msg => (
        <MessageItem key={msg.id} message={msg} />
      ))}

      {/* Outbox (pending/sending/failed messages from this room) */}
      {roomOutbox.map(msg => (
        <MessageItem key={msg.clientId} message={msg} />
      ))}
    </div>
  )
}
