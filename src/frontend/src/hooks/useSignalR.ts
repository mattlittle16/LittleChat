import { useEffect, useRef, useState } from 'react'
import { startConnection, getConnection } from '../services/signalrClient'
import { useMessageStore } from '../stores/messageStore'
import { useRoomStore } from '../stores/roomStore'
import { usePresenceStore } from '../stores/presenceStore'
import { useTypingStore } from '../stores/typingStore'
import { useNotificationPreferencesStore } from '../stores/notificationPreferencesStore'
import { showMentionToast } from '../components/chat/MentionToast'
import { playChime, showBrowserNotification } from '../services/notificationService'
import type { Message } from '../types'

type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

const HEARTBEAT_INTERVAL_MS = 15_000

export function useSignalR(roomId: string | null) {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected')
  const addMessage = useMessageStore(s => s.addMessage)
  const updateMessage = useMessageStore(s => s.updateMessage)
  const removeMessage = useMessageStore(s => s.removeMessage)
  const updateReactions = useMessageStore(s => s.updateReactions)
  const { updateUnread, activeRoomId, setMention } = useRoomStore()
  const { setOnline, setOffline, setInitialPresence } = usePresenceStore()
  const { setTyping } = useTypingStore()
  const prevRoomRef = useRef<string | null>(null)
  const heartbeatRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    if (!roomId) return

    let cancelled = false
    // Defer status update to avoid synchronous setState inside an effect body
    queueMicrotask(() => { if (!cancelled) setStatus('connecting') })

    startConnection(
      roomId,
      () => setStatus('reconnecting'),
      () => setStatus('connected'),
      () => {
        setStatus('disconnected')
        if (heartbeatRef.current) {
          clearInterval(heartbeatRef.current)
          heartbeatRef.current = null
        }
      },
    ).then(connection => {
      if (cancelled) return
      setStatus('connected')

      connection.on('ReceiveMessage', (msg: Message) => {
        addMessage(msg)
        // Update unread badge if this isn't the currently visible room,
        // or if it is but the tab is hidden (user is in another tab)
        if (msg.roomId !== activeRoomId || document.hidden) {
          updateUnread(msg.roomId, 1)
        }
        // Safety net: if the room isn't in our list yet, reload rooms
        if (!useRoomStore.getState().rooms.find(r => r.id === msg.roomId)) {
          useRoomStore.getState().loadRooms()
        }
        // Update browser tab title with total unread count
        queueMicrotask(() => {
          const rooms = useRoomStore.getState().rooms
          const total = rooms.reduce((sum, r) => sum + r.unreadCount, 0)
          document.title = total > 0 ? `(${total}) LittleChat` : 'LittleChat'
        })
        // Play chime and browser notification if not actively viewing this conversation,
        // or if the user has focus elsewhere (different window/app, tab still visible)
        if (msg.roomId !== activeRoomId || document.visibilityState !== 'visible' || !document.hasFocus()) {
          const room = useRoomStore.getState().rooms.find(r => r.id === msg.roomId)
          const isDm = room?.isDm ?? false
          const level = useNotificationPreferencesStore.getState().effectiveLevelForRoom(msg.roomId, isDm)
          if (level === 'all_messages') {
            playChime()
            if (document.visibilityState !== 'visible') {
              const authorName = msg.author?.displayName ?? 'Someone'
              showBrowserNotification(authorName, msg.content, msg.roomId)
            }
          }
        }
      })

      // Recipient is notified when someone opens a brand-new DM with them
      connection.on('DmCreated', async (roomId: string) => {
        await useRoomStore.getState().loadRooms()
        connection.invoke('JoinRoom', roomId).catch(() => {})
        updateUnread(roomId, 1)
      })

      // Both participants are notified when a DM is deleted
      connection.on('DmDeleted', (roomId: string) => {
        useRoomStore.getState().removeRoom(roomId)
      })

      // All members are notified when a group room is deleted
      connection.on('RoomDeleted', (roomId: string) => {
        useRoomStore.getState().removeRoom(roomId)
      })

      // T071: wire presence updates from server
      connection.on('PresenceUpdate', (userId: string, isOnline: boolean) => {
        if (isOnline) setOnline(userId)
        else setOffline(userId)
      })

      // Snapshot of all currently online users sent on connect — populates the
      // local presence store so reloading one browser doesn't show everyone as offline.
      connection.on('PresenceSnapshot', (userIds: string[]) => {
        setInitialPresence(userIds)
      })

      // T093: wire edit and delete events
      connection.on('MessageEdited', (messageId: string, _roomId: string, content: string, editedAt: string) => {
        const existing = useMessageStore.getState().messages.get(messageId)
        if (existing) updateMessage({ ...existing, content, editedAt })
      })

      connection.on('MessageDeleted', (messageId: string) => {
        removeMessage(messageId)
      })

      // T084: wire reaction and typing events
      connection.on('ReactionUpdated', (
        messageId: string, _roomId: string, emoji: string,
        count: number, _added: boolean, users: string[]
      ) => {
        updateReactions(messageId, emoji, count, users)
      })

      connection.on('UserTyping', (roomId: string, _userId: string, displayName: string) => {
        setTyping(roomId, _userId, displayName)
      })

      connection.on('MentionNotification', (
        _messageId: string,
        roomId: string,
        roomName: string,
        _fromUserId: string,
        fromDisplayName: string,
        contentPreview: string
      ) => {
        setMention(roomId.toString())
        showMentionToast({ roomId: roomId.toString(), roomName, fromDisplayName, contentPreview })
        const room = useRoomStore.getState().rooms.find(r => r.id === roomId.toString())
        const isDm = room?.isDm ?? false
        const level = useNotificationPreferencesStore.getState().effectiveLevelForRoom(roomId.toString(), isDm)
        if (level !== 'muted') {
          playChime()
          showBrowserNotification(fromDisplayName, contentPreview, roomId.toString())
        }
      })

      // T072: send heartbeat every 15s to keep presence key alive (30s TTL)
      heartbeatRef.current = setInterval(() => {
        if (connection.state === 'Connected') {
          connection.invoke('Heartbeat').catch(() => {
            // Not fatal — TTL will expire naturally if disconnected
          })
        }
      }, HEARTBEAT_INTERVAL_MS)
    }).catch(() => {
      if (!cancelled) setStatus('disconnected')
    })

    return () => {
      cancelled = true
      if (heartbeatRef.current) {
        clearInterval(heartbeatRef.current)
        heartbeatRef.current = null
      }
    }
  }, [roomId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Join new room group when active room changes (without reconnecting)
  useEffect(() => {
    if (!roomId || roomId === prevRoomRef.current) return
    prevRoomRef.current = roomId

    const connection = getConnection()
    if (connection?.state === 'Connected') {
      connection.invoke('JoinRoom', roomId).catch(() => {
        // Not fatal — OnConnectedAsync handles the initial join
      })
    }
  }, [roomId])

  return { status }
}
