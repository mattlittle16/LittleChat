import { create } from 'zustand'
import type { Notification } from '../types'
import {
  fetchNotifications,
  markAllNotificationsRead,
  markNotificationsRead,
  markRoomNotificationsRead,
} from '../services/notificationService'

interface NotificationState {
  notifications: Notification[]
  unreadCount: number
  isOpen: boolean

  // Actions
  loadNotifications: () => Promise<void>
  setNotifications: (notifications: Notification[]) => void
  addNotification: (notification: Notification) => void
  markRead: (ids: string[]) => Promise<void>
  markRoomRead: (roomId: string) => Promise<void>
  markAllRead: () => Promise<void>
  setOpen: (open: boolean) => void
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  notifications: [],
  unreadCount: 0,
  isOpen: false,

  loadNotifications: async () => {
    try {
      const notifications = await fetchNotifications()
      const unreadCount = notifications.filter((n) => !n.isRead).length
      set({ notifications, unreadCount })
    } catch {
      // Non-fatal — notification center will show empty state
    }
  },

  setNotifications: (notifications) => {
    const unreadCount = notifications.filter((n) => !n.isRead).length
    set({ notifications, unreadCount })
  },

  addNotification: (notification) => {
    // Deduplicate by id
    const existing = get().notifications
    if (existing.some((n) => n.id === notification.id)) return
    const notifications = [notification, ...existing]
    const unreadCount = notifications.filter((n) => !n.isRead).length
    set({ notifications, unreadCount })
  },

  markRead: async (ids) => {
    // Optimistic update
    const notifications = get().notifications.map((n) =>
      ids.includes(n.id) ? { ...n, isRead: true } : n
    )
    const unreadCount = notifications.filter((n) => !n.isRead).length
    set({ notifications, unreadCount })
    try {
      await markNotificationsRead(ids)
    } catch {
      // Optimistic update stands — next page load will reconcile
    }
  },

  markRoomRead: async (roomId) => {
    // Optimistic update
    const notifications = get().notifications.map((n) =>
      n.roomId === roomId ? { ...n, isRead: true } : n
    )
    const unreadCount = notifications.filter((n) => !n.isRead).length
    set({ notifications, unreadCount })
    try {
      await markRoomNotificationsRead(roomId)
    } catch {
      // Optimistic update stands
    }
  },

  markAllRead: async () => {
    // Optimistic update
    const notifications = get().notifications.map((n) => ({ ...n, isRead: true }))
    set({ notifications, unreadCount: 0 })
    try {
      await markAllNotificationsRead()
    } catch {
      // Optimistic update stands
    }
  },

  setOpen: (open) => set({ isOpen: open }),
}))
