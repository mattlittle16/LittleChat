import { create } from 'zustand'

interface TypingState {
  // roomId → { userId → displayName }
  typingByRoom: Record<string, Record<string, string>>
  setTyping: (roomId: string, userId: string, displayName: string) => void
  clearTyping: (roomId: string, userId: string) => void
  clearRoom: (roomId: string) => void
}

// Timers live outside Zustand to avoid serialization issues
const timers = new Map<string, ReturnType<typeof setTimeout>>()

export const useTypingStore = create<TypingState>((set, get) => ({
  typingByRoom: {},

  setTyping: (roomId, userId, displayName) => {
    const key = `${roomId}:${userId}`

    // Reset the 2-second auto-clear timer
    const existing = timers.get(key)
    if (existing) clearTimeout(existing)
    timers.set(key, setTimeout(() => {
      get().clearTyping(roomId, userId)
      timers.delete(key)
    }, 2_000))

    set(s => ({
      typingByRoom: {
        ...s.typingByRoom,
        [roomId]: { ...s.typingByRoom[roomId], [userId]: displayName },
      },
    }))
  },

  clearTyping: (roomId, userId) => {
    set(s => {
      const room = { ...s.typingByRoom[roomId] }
      delete room[userId]
      const typingByRoom = { ...s.typingByRoom }
      if (Object.keys(room).length === 0) delete typingByRoom[roomId]
      else typingByRoom[roomId] = room
      return { typingByRoom }
    })
  },

  clearRoom: (roomId) => {
    // Cancel all pending timers for this room to prevent state updates after unmount
    for (const [key, timer] of timers) {
      if (key.startsWith(`${roomId}:`)) {
        clearTimeout(timer)
        timers.delete(key)
      }
    }
    set(s => {
      const typingByRoom = { ...s.typingByRoom }
      delete typingByRoom[roomId]
      return { typingByRoom }
    })
  },
}))
