import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useNotificationStore } from '../stores/notificationStore'
import type { Notification } from '../types'

vi.mock('../services/notificationService', () => ({
  fetchNotifications: vi.fn(),
  markNotificationsRead: vi.fn().mockResolvedValue(undefined),
  markRoomNotificationsRead: vi.fn().mockResolvedValue(undefined),
  markAllNotificationsRead: vi.fn().mockResolvedValue(undefined),
}))

import {
  fetchNotifications,
  markNotificationsRead,
  markRoomNotificationsRead,
  markAllNotificationsRead,
} from '../services/notificationService'

const mockFetch = fetchNotifications as ReturnType<typeof vi.fn>
const mockMarkRead = markNotificationsRead as ReturnType<typeof vi.fn>
const mockMarkRoomRead = markRoomNotificationsRead as ReturnType<typeof vi.fn>
const mockMarkAll = markAllNotificationsRead as ReturnType<typeof vi.fn>

function makeNotification(overrides: Partial<Notification> = {}): Notification {
  return {
    id: 'n-1',
    recipientUserId: 'user-1',
    type: 'mention',
    messageId: 'msg-1',
    roomId: 'room-1',
    roomName: 'General',
    fromUserId: 'user-2',
    fromDisplayName: 'Bob',
    contentPreview: 'Hello @alice',
    isRead: false,
    createdAt: '2024-01-01T00:00:00Z',
    expiresAt: '2024-12-31T00:00:00Z',
    ...overrides,
  }
}

beforeEach(() => {
  useNotificationStore.setState({ notifications: [], unreadCount: 0, isOpen: false })
  vi.clearAllMocks()
})

describe('setNotifications', () => {
  it('replaces the notification list and recomputes unreadCount', () => {
    const notifs = [
      makeNotification({ id: 'n-1', isRead: false }),
      makeNotification({ id: 'n-2', isRead: true }),
      makeNotification({ id: 'n-3', isRead: false }),
    ]
    useNotificationStore.getState().setNotifications(notifs)
    expect(useNotificationStore.getState().notifications).toHaveLength(3)
    expect(useNotificationStore.getState().unreadCount).toBe(2)
  })
})

describe('addNotification', () => {
  it('prepends the notification and updates unreadCount', () => {
    useNotificationStore.getState().setNotifications([makeNotification({ id: 'n-1' })])
    useNotificationStore.getState().addNotification(makeNotification({ id: 'n-2', isRead: false }))
    const { notifications, unreadCount } = useNotificationStore.getState()
    expect(notifications[0].id).toBe('n-2')
    expect(unreadCount).toBe(2)
  })

  it('deduplicates — ignores notification with existing id', () => {
    useNotificationStore.getState().setNotifications([makeNotification({ id: 'n-1' })])
    useNotificationStore.getState().addNotification(makeNotification({ id: 'n-1' }))
    expect(useNotificationStore.getState().notifications).toHaveLength(1)
  })
})

describe('markRead', () => {
  it('sets isRead on matched notifications and recomputes unreadCount', async () => {
    useNotificationStore.getState().setNotifications([
      makeNotification({ id: 'n-1', isRead: false }),
      makeNotification({ id: 'n-2', isRead: false }),
    ])
    await useNotificationStore.getState().markRead(['n-1'])
    const { notifications, unreadCount } = useNotificationStore.getState()
    expect(notifications.find(n => n.id === 'n-1')!.isRead).toBe(true)
    expect(notifications.find(n => n.id === 'n-2')!.isRead).toBe(false)
    expect(unreadCount).toBe(1)
    expect(mockMarkRead).toHaveBeenCalledWith(['n-1'])
  })

  it('keeps the optimistic update even when the API call fails', async () => {
    mockMarkRead.mockRejectedValueOnce(new Error('network error'))
    useNotificationStore.getState().setNotifications([makeNotification({ id: 'n-1', isRead: false })])
    await useNotificationStore.getState().markRead(['n-1'])
    expect(useNotificationStore.getState().notifications[0].isRead).toBe(true)
  })
})

describe('markRoomRead', () => {
  it('marks all notifications for a room as read', async () => {
    useNotificationStore.getState().setNotifications([
      makeNotification({ id: 'n-1', roomId: 'r1', isRead: false }),
      makeNotification({ id: 'n-2', roomId: 'r1', isRead: false }),
      makeNotification({ id: 'n-3', roomId: 'r2', isRead: false }),
    ])
    await useNotificationStore.getState().markRoomRead('r1')
    const { notifications, unreadCount } = useNotificationStore.getState()
    expect(notifications.find(n => n.id === 'n-1')!.isRead).toBe(true)
    expect(notifications.find(n => n.id === 'n-2')!.isRead).toBe(true)
    expect(notifications.find(n => n.id === 'n-3')!.isRead).toBe(false)
    expect(unreadCount).toBe(1)
    expect(mockMarkRoomRead).toHaveBeenCalledWith('r1')
  })
})

describe('markAllRead', () => {
  it('sets all notifications as read and zeroes unreadCount', async () => {
    useNotificationStore.getState().setNotifications([
      makeNotification({ id: 'n-1', isRead: false }),
      makeNotification({ id: 'n-2', isRead: false }),
    ])
    await useNotificationStore.getState().markAllRead()
    const { notifications, unreadCount } = useNotificationStore.getState()
    expect(notifications.every(n => n.isRead)).toBe(true)
    expect(unreadCount).toBe(0)
    expect(mockMarkAll).toHaveBeenCalled()
  })
})

describe('loadNotifications', () => {
  it('fetches notifications and populates the store', async () => {
    const notifs = [makeNotification({ id: 'n-1', isRead: false })]
    mockFetch.mockResolvedValueOnce(notifs)
    await useNotificationStore.getState().loadNotifications()
    expect(useNotificationStore.getState().notifications).toHaveLength(1)
    expect(useNotificationStore.getState().unreadCount).toBe(1)
  })

  it('leaves store empty when fetch fails', async () => {
    mockFetch.mockRejectedValueOnce(new Error('network error'))
    await useNotificationStore.getState().loadNotifications()
    expect(useNotificationStore.getState().notifications).toHaveLength(0)
  })
})
