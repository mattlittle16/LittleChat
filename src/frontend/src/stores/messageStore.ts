import { create } from 'zustand'
import { api } from '../services/apiClient'
import type { Message } from '../types'

interface MessagePage {
  messages: Message[]
  hasMore: boolean
  hasNewer: boolean
}

interface MessageState {
  messages: Map<string, Message>
  hasMoreByRoom: Map<string, boolean>
  hasNewerByRoom: Map<string, boolean>
  /** roomId → messageId: tells the room-switch effect to load around a specific message instead of the latest page */
  pendingAroundByRoom: Map<string, string>
  scrollToMessageId: string | null
  addMessage: (msg: Message) => void
  updateMessage: (msg: Message) => void
  removeMessage: (id: string) => void
  updateReactions: (messageId: string, emoji: string, count: number, users: string[]) => void
  loadPage: (roomId: string, before?: { createdAt: string; id: string }) => Promise<void>
  loadAroundMessage: (roomId: string, messageId: string) => Promise<void>
  loadNewerPage: (roomId: string, after: { createdAt: string; id: string }) => Promise<void>
  clearRoom: (roomId: string) => void
  setScrollToMessageId: (id: string | null) => void
  setPendingAround: (roomId: string, messageId: string) => void
}

export const useMessageStore = create<MessageState>((set) => ({
  messages: new Map(),
  hasMoreByRoom: new Map(),
  hasNewerByRoom: new Map(),
  pendingAroundByRoom: new Map(),
  scrollToMessageId: null,

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

  updateReactions: (messageId, emoji, count, users) => {
    set(s => {
      const msg = s.messages.get(messageId)
      if (!msg) return {}
      const reactions = msg.reactions.filter(r => r.emoji !== emoji)
      if (count > 0) reactions.push({ emoji, count, users })
      const next = new Map(s.messages)
      next.set(messageId, { ...msg, reactions })
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

  loadAroundMessage: async (roomId, messageId) => {
    const params = new URLSearchParams()
    params.set('aroundId', messageId)
    params.set('limit', '50')

    const data = await api.get<MessagePage>(
      `/api/rooms/${roomId}/messages?${params.toString()}`
    )

    set(s => {
      // Replace all messages for this room with the context window
      const next = new Map(s.messages)
      for (const [id, msg] of next) {
        if (msg.roomId === roomId) next.delete(id)
      }
      for (const msg of data.messages) {
        next.set(msg.id, msg)
      }
      const nextHasMore = new Map(s.hasMoreByRoom)
      nextHasMore.set(roomId, data.hasMore)
      const nextHasNewer = new Map(s.hasNewerByRoom)
      nextHasNewer.set(roomId, data.hasNewer)
      return { messages: next, hasMoreByRoom: nextHasMore, hasNewerByRoom: nextHasNewer }
    })
  },

  loadNewerPage: async (roomId, after) => {
    const params = new URLSearchParams()
    params.set('after', after.createdAt)
    params.set('afterId', after.id)
    params.set('limit', '50')

    const data = await api.get<MessagePage>(
      `/api/rooms/${roomId}/messages?${params.toString()}`
    )

    set(s => {
      const next = new Map(s.messages)
      for (const msg of data.messages) {
        next.set(msg.id, msg)
      }
      const nextHasNewer = new Map(s.hasNewerByRoom)
      nextHasNewer.set(roomId, data.hasNewer)
      return { messages: next, hasNewerByRoom: nextHasNewer }
    })
  },

  setScrollToMessageId: (id) => set({ scrollToMessageId: id }),

  setPendingAround: (roomId, messageId) => set(s => {
    const next = new Map(s.pendingAroundByRoom)
    next.set(roomId, messageId)
    return { pendingAroundByRoom: next }
  }),

  clearRoom: (roomId) => {
    set(s => {
      const next = new Map(s.messages)
      for (const [id, msg] of next) {
        if (msg.roomId === roomId) next.delete(id)
      }
      const nextHasMore = new Map(s.hasMoreByRoom)
      nextHasMore.delete(roomId)
      const nextHasNewer = new Map(s.hasNewerByRoom)
      nextHasNewer.delete(roomId)
      return { messages: next, hasMoreByRoom: nextHasMore, hasNewerByRoom: nextHasNewer }
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
