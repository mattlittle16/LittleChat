import { create } from 'zustand'
import { api } from '../services/apiClient'
import type { SidebarGroup } from '../types'

interface SidebarGroupState {
  groups: SidebarGroup[]
  fetchGroups: () => Promise<void>
  createGroup: (name: string) => Promise<SidebarGroup>
  renameGroup: (groupId: string, name: string) => Promise<void>
  deleteGroup: (groupId: string) => Promise<void>
  assignRoom: (groupId: string, roomId: string) => Promise<void>
  unassignRoom: (roomId: string) => Promise<void>
  setCollapsed: (groupId: string, isCollapsed: boolean) => Promise<void>
  reorderRooms: (groupId: string | null, roomIds: string[]) => Promise<void>
}

export const useSidebarGroupStore = create<SidebarGroupState>((set) => ({
  groups: [],

  fetchGroups: async () => {
    const groups = await api.get<SidebarGroup[]>('/api/sidebar-groups')
    set({ groups })
  },

  createGroup: async (name) => {
    const group = await api.post<SidebarGroup>('/api/sidebar-groups', { name })
    set(s => ({ groups: [...s.groups, group] }))
    return group
  },

  renameGroup: async (groupId, name) => {
    await api.patch(`/api/sidebar-groups/${groupId}`, { name })
    set(s => ({
      groups: s.groups.map(g => g.id === groupId ? { ...g, name } : g),
    }))
  },

  deleteGroup: async (groupId) => {
    await api.delete(`/api/sidebar-groups/${groupId}`)
    set(s => ({
      groups: s.groups
        .filter(g => g.id !== groupId)
        .map(g => ({ ...g })),
    }))
  },

  assignRoom: async (groupId, roomId) => {
    await api.put(`/api/sidebar-groups/${groupId}/rooms/${roomId}`, null)
    set(s => ({
      groups: s.groups.map(g => {
        if (g.id === groupId) return { ...g, roomIds: [...g.roomIds.filter(id => id !== roomId), roomId] }
        return { ...g, roomIds: g.roomIds.filter(id => id !== roomId) }
      }),
    }))
  },

  unassignRoom: async (roomId) => {
    await api.delete(`/api/sidebar-groups/rooms/${roomId}`)
    set(s => ({
      groups: s.groups.map(g => ({ ...g, roomIds: g.roomIds.filter(id => id !== roomId) })),
    }))
  },

  setCollapsed: async (groupId, isCollapsed) => {
    await api.patch(`/api/sidebar-groups/${groupId}/collapsed`, { isCollapsed })
    set(s => ({
      groups: s.groups.map(g => g.id === groupId ? { ...g, isCollapsed } : g),
    }))
  },

  reorderRooms: async (groupId, roomIds) => {
    // Optimistic update
    const prevGroups = useSidebarGroupStore.getState().groups
    set(s => ({
      groups: s.groups.map(g => {
        if (groupId === null) return g  // uncategorized handled by filtering ungrouped
        return g.id === groupId ? { ...g, roomIds } : g
      }),
    }))
    // For uncategorized (groupId === null), there's no group to update in the groups array
    // The ungrouped list is derived elsewhere; we still call the API to persist.
    try {
      await api.patch('/api/sidebar-groups/rooms/reorder', { groupId, roomIds })
    } catch {
      // Roll back optimistic update on failure
      set({ groups: prevGroups })
      throw new Error('Failed to save topic order. Changes have been reverted.')
    }
  },
}))
