import { create } from 'zustand'
import { addToOutbox, deleteFromOutbox, getAllPending, updateStatus } from '../services/outboxDb'
import { getConnection } from '../services/signalrClient'
import type { OutboxMessage } from '../types'

interface OutboxState {
  messages: OutboxMessage[]
  enqueue: (roomId: string, content: string, quotedMessageId?: string) => Promise<void>
  drainOutbox: () => Promise<void>
  retryAll: () => Promise<void>
  loadFromDb: () => Promise<void>
}

export const useOutboxStore = create<OutboxState>((set, get) => ({
  messages: [],

  loadFromDb: async () => {
    const pending = await getAllPending()
    set({ messages: pending })
  },

  enqueue: async (roomId, content, quotedMessageId) => {
    const msg: OutboxMessage = {
      clientId: crypto.randomUUID(),
      roomId,
      content,
      createdAt: Date.now(),
      status: 'pending',
      ...(quotedMessageId ? { quotedMessageId } : {}),
    }
    await addToOutbox(msg)
    set(s => ({ messages: [...s.messages, msg] }))
    // Attempt immediate send
    await get().drainOutbox()
  },

  drainOutbox: async () => {
    const { messages } = get()
    const pending = messages.filter(m => m.status === 'pending' || m.status === 'failed')
    if (pending.length === 0) return

    const connection = getConnection()
    if (!connection || connection.state !== 'Connected') return

    for (const msg of pending) {
      await updateStatus(msg.clientId, 'sending')
      set(s => ({
        messages: s.messages.map(m =>
          m.clientId === msg.clientId ? { ...m, status: 'sending' } : m
        ),
      }))

      try {
        await connection.invoke('SendMessage', {
          messageId: msg.clientId,
          roomId: msg.roomId,
          content: msg.content,
          ...(msg.quotedMessageId ? { quotedMessageId: msg.quotedMessageId } : {}),
        })
        await deleteFromOutbox(msg.clientId)
        set(s => ({ messages: s.messages.filter(m => m.clientId !== msg.clientId) }))
      } catch (err) {
        console.error('[outbox] SendMessage failed', err)
        await updateStatus(msg.clientId, 'failed')
        set(s => ({
          messages: s.messages.map(m =>
            m.clientId === msg.clientId ? { ...m, status: 'failed' } : m
          ),
        }))
      }
    }
  },

  retryAll: async () => {
    const { messages } = get()
    for (const msg of messages.filter(m => m.status === 'failed')) {
      await updateStatus(msg.clientId, 'pending')
    }
    set(s => ({
      messages: s.messages.map(m =>
        m.status === 'failed' ? { ...m, status: 'pending' } : m
      ),
    }))
    await get().drainOutbox()
  },
}))
