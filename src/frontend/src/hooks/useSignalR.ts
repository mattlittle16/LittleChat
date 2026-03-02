import { useEffect, useRef, useState } from 'react'
import { startConnection, getConnection } from '../services/signalrClient'
import { useMessageStore } from '../stores/messageStore'
import { useRoomStore } from '../stores/roomStore'
import type { Message } from '../types'

type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export function useSignalR(roomId: string | null) {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected')
  const addMessage = useMessageStore(s => s.addMessage)
  const { updateUnread, activeRoomId } = useRoomStore()
  const prevRoomRef = useRef<string | null>(null)

  useEffect(() => {
    if (!roomId) return

    let cancelled = false
    setStatus('connecting')

    startConnection(
      roomId,
      () => setStatus('reconnecting'),
      () => setStatus('connected'),
      () => setStatus('disconnected'),
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
    }).catch(() => {
      if (!cancelled) setStatus('disconnected')
    })

    return () => {
      cancelled = true
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
