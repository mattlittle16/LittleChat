import { useEffect, useRef } from 'react'
import { useNotificationStore } from '../../stores/notificationStore'
import { useRoomStore } from '../../stores/roomStore'
import { useMessageStore } from '../../stores/messageStore'
import type { Notification } from '../../types'

function notifLabel(type: string) {
  if (type === 'mention') return '@mention'
  if (type === 'topic_alert') return '@topic'
  if (type === 'unread_dm') return 'DM'
  if (type === 'reaction') return 'reaction'
  return type
}

function timeAgo(iso: string): string {
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000)
  if (diff < 60) return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
  return `${Math.floor(diff / 86400)}d ago`
}

interface Props {
  onClose: () => void
}

export function NotificationCenter({ onClose }: Props) {
  const { notifications, unreadCount, loadNotifications, markRead, markAllRead, markRoomRead } =
    useNotificationStore()
  const setActiveRoom = useRoomStore((s) => s.setActiveRoom)
  const activeRoomId = useRoomStore((s) => s.activeRoomId)
  const setScrollToMessageId = useMessageStore((s) => s.setScrollToMessageId)
  const setPendingAround = useMessageStore((s) => s.setPendingAround)
  const loadAroundMessage = useMessageStore((s) => s.loadAroundMessage)
  const panelRef = useRef<HTMLDivElement>(null)

  // Load on mount
  useEffect(() => {
    loadNotifications()
  }, [loadNotifications])

  // Close when clicking outside
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        onClose()
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [onClose])

  function handleNotifClick(n: Notification) {
    if (n.type === 'reaction' && n.messageId) {
      const messageId = n.messageId
      markRead([n.id])
      if (activeRoomId === n.roomId) {
        // Same room: no room switch effect fires, load directly
        loadAroundMessage(n.roomId, messageId).then(() => {
          setScrollToMessageId(messageId)
        })
      } else {
        // Different room: store pending before switching so the room switch
        // effect uses loadAroundMessage instead of loadPage (avoids race)
        setPendingAround(n.roomId, messageId)
        setActiveRoom(n.roomId)
      }
      onClose()
      return
    }
    setActiveRoom(n.roomId)
    markRoomRead(n.roomId)
    onClose()
  }

  function handleMarkAllRead() {
    markAllRead()
  }

  // Mark individual notification as read when visible and unread
  function handleMarkRead(n: Notification) {
    if (!n.isRead) {
      markRead([n.id])
    }
  }

  return (
    <div
      ref={panelRef}
      className="absolute right-0 top-full mt-1 z-30 w-80 max-h-[480px] flex flex-col rounded-lg border bg-background shadow-xl"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b">
        <span className="font-semibold text-sm">
          Notifications
          {unreadCount > 0 && (
            <span className="ml-2 text-xs bg-destructive text-destructive-foreground rounded-full px-1.5 py-0.5">
              {unreadCount}
            </span>
          )}
        </span>
        {unreadCount > 0 && (
          <button
            onClick={handleMarkAllRead}
            className="text-xs text-muted-foreground hover:text-foreground"
          >
            Mark all read
          </button>
        )}
      </div>

      {/* Notification list */}
      <div className="overflow-y-auto flex-1">
        {notifications.length === 0 ? (
          <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
            No notifications
          </div>
        ) : (
          notifications.map((n) => (
            <button
              key={n.id}
              className={[
                'w-full text-left px-4 py-3 hover:bg-muted/60 border-b last:border-0 transition-colors',
                n.isRead ? 'opacity-60' : '',
              ].join(' ')}
              onClick={() => {
                handleNotifClick(n)
                handleMarkRead(n)
              }}
            >
              <div className="flex items-start gap-2">
                {!n.isRead && (
                  <span className="mt-1.5 h-2 w-2 flex-shrink-0 rounded-full bg-blue-500" />
                )}
                {n.isRead && <span className="mt-1.5 h-2 w-2 flex-shrink-0" />}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1.5 mb-0.5">
                    <span className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      {notifLabel(n.type)}
                    </span>
                    <span className="text-[10px] text-muted-foreground">·</span>
                    <span className="text-[10px] text-muted-foreground">{n.roomName}</span>
                    <span className="ml-auto text-[10px] text-muted-foreground flex-shrink-0">
                      {timeAgo(n.createdAt)}
                    </span>
                  </div>
                  <p className="text-xs font-medium truncate">{n.fromDisplayName}</p>
                  {n.type === 'reaction' ? (
                    <p className="text-xs text-muted-foreground line-clamp-2">reacted: {n.contentPreview}</p>
                  ) : (
                    <p className="text-xs text-muted-foreground line-clamp-2">{n.contentPreview}</p>
                  )}
                </div>
              </div>
            </button>
          ))
        )}
      </div>
    </div>
  )
}
