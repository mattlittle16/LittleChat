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
  deleteRoom: (roomId: string) => Promise<void>
  removeRoom: (roomId: string) => void
}

export const useRoomStore = create<RoomState>((set, get) => ({
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
    // Persist last-read position to server (fire-and-forget)
    api.post(`/api/rooms/${roomId}/read`, null).catch(() => {})
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

  deleteRoom: async (roomId) => {
    await api.delete(`/api/rooms/${roomId}`)
    get().removeRoom(roomId)
  },

  removeRoom: (roomId) => {
    set(s => {
      const remaining = s.rooms.filter(r => r.id !== roomId)
      const newActiveId = s.activeRoomId === roomId
        ? (remaining[0]?.id ?? null)
        : s.activeRoomId
      if (newActiveId !== s.activeRoomId) {
        if (newActiveId) localStorage.setItem(ACTIVE_ROOM_KEY, newActiveId)
        else localStorage.removeItem(ACTIVE_ROOM_KEY)
      }
      return { rooms: remaining, activeRoomId: newActiveId }
    })
  },
}))
