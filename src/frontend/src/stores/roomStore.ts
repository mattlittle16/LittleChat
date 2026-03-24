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
  addRoom: (room: Room) => void
  // 012-topics-overhaul
  createTopic: (name: string, isPrivate?: boolean, invitedUserIds?: string[]) => Promise<Room>
  inviteToTopic: (roomId: string, targetUserId: string) => Promise<void>
  removeMember: (roomId: string, targetUserId: string) => Promise<void>
  leaveTopic: (roomId: string, newOwnerUserId?: string, newOwnerDisplayName?: string) => Promise<void>
  transferOwnership: (roomId: string, newOwnerUserId: string, newOwnerDisplayName: string) => Promise<void>
  discoverTopics: (searchTerm?: string) => Promise<{ id: string; name: string; memberCount: number; createdAt: string }[]>
  joinTopic: (roomId: string) => Promise<void>
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
    api.post(`/api/rooms/${roomId}/read`, null).catch(err => console.error('[roomStore] markRead failed', err))
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

  addRoom: (room) => {
    set(s => {
      if (s.rooms.some(r => r.id === room.id)) return s
      return { rooms: [...s.rooms, room] }
    })
  },

  createTopic: async (name, isPrivate = false, invitedUserIds = []) => {
    const room = await api.post<Room>('/api/rooms', { name, isPrivate, invitedUserIds })
    get().addRoom(room)
    return room
  },

  inviteToTopic: async (roomId, targetUserId) => {
    await api.post(`/api/rooms/${roomId}/invite`, { targetUserId })
  },

  removeMember: async (roomId, targetUserId) => {
    await api.delete(`/api/rooms/${roomId}/members/${targetUserId}`)
  },

  leaveTopic: async (roomId, newOwnerUserId, newOwnerDisplayName) => {
    await api.post(`/api/rooms/${roomId}/leave`, {
      newOwnerUserId: newOwnerUserId ?? null,
      newOwnerDisplayName: newOwnerDisplayName ?? null,
    })
    get().removeRoom(roomId)
  },

  transferOwnership: async (roomId, newOwnerUserId, newOwnerDisplayName) => {
    await api.post(`/api/rooms/${roomId}/transfer-ownership`, { newOwnerUserId, newOwnerDisplayName })
    // Update local state: current user is no longer owner
    set(s => ({
      rooms: s.rooms.map(r =>
        r.id === roomId ? { ...r, ownerId: newOwnerUserId } : r
      ),
    }))
  },

  discoverTopics: async (searchTerm = '') => {
    const q = searchTerm.trim() ? `?q=${encodeURIComponent(searchTerm)}` : ''
    return api.get(`/api/rooms/discover${q}`)
  },

  joinTopic: async (roomId) => {
    await api.post(`/api/rooms/${roomId}/join`, null)
    // Reload rooms so the newly joined topic appears in the sidebar
    await get().loadRooms()
  },
}))
