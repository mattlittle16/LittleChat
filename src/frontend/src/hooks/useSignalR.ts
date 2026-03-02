import { useEffect, useRef, useState } from 'react'
import { startConnection, getConnection } from '../services/signalrClient'
import { useMessageStore } from '../stores/messageStore'
import { useRoomStore } from '../stores/roomStore'
import { usePresenceStore } from '../stores/presenceStore'
import { useTypingStore } from '../stores/typingStore'
import type { Message } from '../types'

type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

const HEARTBEAT_INTERVAL_MS = 15_000

export function useSignalR(roomId: string | null) {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected')
  const addMessage = useMessageStore(s => s.addMessage)
  const updateMessage = useMessageStore(s => s.updateMessage)
  const removeMessage = useMessageStore(s => s.removeMessage)
  const updateReactions = useMessageStore(s => s.updateReactions)
  const { updateUnread, activeRoomId } = useRoomStore()
  const { setOnline, setOffline } = usePresenceStore()
  const { setTyping } = useTypingStore()
  const prevRoomRef = useRef<string | null>(null)
  const heartbeatRef = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    if (!roomId) return

    let cancelled = false
    setStatus('connecting')

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
        // Update unread badge if this isn't the currently visible room
        if (msg.roomId !== activeRoomId) {
          updateUnread(msg.roomId, 1)
        }
      })

      // T071: wire presence updates from server
      connection.on('PresenceUpdate', (userId: string, isOnline: boolean) => {
        if (isOnline) setOnline(userId)
        else setOffline(userId)
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
