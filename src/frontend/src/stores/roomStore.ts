import { create } from 'zustand'
import { api } from '../services/apiClient'
import type { Room } from '../types'

const ACTIVE_ROOM_KEY = 'littlechat_active_room'

interface RoomState {
  rooms: Room[]
  activeRoomId: string | null
  loadRooms: () => Promise<void>
  setActiveRoom: (id: string | null) => void
  markRead: (roomId: string) => void
  updateUnread: (roomId: string, count: number) => void
  setMention: (roomId: string) => void
}

export const useRoomStore = create<RoomState>((set) => ({
  rooms: [],
  activeRoomId: localStorage.getItem(ACTIVE_ROOM_KEY),

  loadRooms: async () => {
    const rooms = await api.get<Room[]>('/api/rooms')
    set({ rooms })
  },

  setActiveRoom: (id) => {
    if (id) {
      localStorage.setItem(ACTIVE_ROOM_KEY, id)
    } else {
      localStorage.removeItem(ACTIVE_ROOM_KEY)
    }
    set({ activeRoomId: id })
  },

  markRead: (roomId) => {
    set(s => ({
      rooms: s.rooms.map(r =>
        r.id === roomId ? { ...r, unreadCount: 0, hasMention: false } : r
      ),
    }))
  },

  updateUnread: (roomId, count) => {
    set(s => ({
      rooms: s.rooms.map(r =>
        r.id === roomId ? { ...r, unreadCount: r.unreadCount + count } : r
      ),
    }))
  },

  setMention: (roomId) => {
    set(s => ({
      rooms: s.rooms.map(r =>
        r.id === roomId ? { ...r, hasMention: true } : r
      ),
    }))
  },
}))
