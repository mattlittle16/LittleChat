import { create } from 'zustand'
import { api } from '../services/apiClient'
import type { Message } from '../types'

interface MessagePage {
  messages: Message[]
  hasMore: boolean
}

interface MessageState {
  messages: Map<string, Message>
  hasMoreByRoom: Map<string, boolean>
  addMessage: (msg: Message) => void
  updateMessage: (msg: Message) => void
  removeMessage: (id: string) => void
  loadPage: (roomId: string, before?: { createdAt: string; id: string }) => Promise<void>
  clearRoom: (roomId: string) => void
}

export const useMessageStore = create<MessageState>((set) => ({
  messages: new Map(),
  hasMoreByRoom: new Map(),

  addMessage: (msg) => {
    set(s => {
      const next = new Map(s.messages)
      next.set(msg.id, msg)
      return { messages: next }
    })
  },

  updateMessage: (msg) => {
    set(s => {
      const next = new Map(s.messages)
      next.set(msg.id, msg)
      return { messages: next }
    })
  },

  removeMessage: (id) => {
    set(s => {
      const next = new Map(s.messages)
      next.delete(id)
      return { messages: next }
    })
  },

  loadPage: async (roomId, before?) => {
    const params = new URLSearchParams()
    if (before) {
      params.set('before', before.createdAt)
      params.set('beforeId', before.id)
    }
    params.set('limit', '50')

    const data = await api.get<MessagePage>(
      `/api/rooms/${roomId}/messages?${params.toString()}`
    )

    set(s => {
      const next = new Map(s.messages)
      for (const msg of data.messages) {
        next.set(msg.id, msg)
      }
      const nextHasMore = new Map(s.hasMoreByRoom)
      nextHasMore.set(roomId, data.hasMore)
      return { messages: next, hasMoreByRoom: nextHasMore }
    })
  },

  clearRoom: (roomId) => {
    set(s => {
      const next = new Map(s.messages)
      for (const [id, msg] of next) {
        if (msg.roomId === roomId) next.delete(id)
      }
      const nextHasMore = new Map(s.hasMoreByRoom)
      nextHasMore.delete(roomId)
      return { messages: next, hasMoreByRoom: nextHasMore }
    })
  },
}))

/** Sorted messages for a given room, derived from the Map */
export function getRoomMessages(messages: Map<string, Message>, roomId: string): Message[] {
  return Array.from(messages.values())
    .filter(m => m.roomId === roomId)
    .sort((a, b) => {
      const diff = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
      return diff !== 0 ? diff : a.id.localeCompare(b.id)
    })
}
