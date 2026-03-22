import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useRoomStore } from '../stores/roomStore'
import type { Room } from '../types'

vi.mock('../services/apiClient', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn().mockResolvedValue(undefined),
    delete: vi.fn().mockResolvedValue(undefined),
  },
}))

function makeRoom(overrides: Partial<Room> = {}): Room {
  return {
    id: 'room-1',
    name: 'General',
    isDm: false,
    unreadCount: 0,
    hasMention: false,
    lastMessagePreview: null,
    createdAt: '2024-01-01T00:00:00Z',
    isPrivate: false,
    ownerId: 'user-1',
    isProtected: false,
    memberCount: 1,
    otherUserId: null,
    otherUserDisplayName: null,
    otherUserAvatarUrl: null,
    ...overrides,
  }
}

beforeEach(() => {
  localStorage.clear()
  useRoomStore.setState({ rooms: [], activeRoomId: null })
})

describe('removeRoom', () => {
  it('switches active room to first remaining when the active room is removed', () => {
    const r1 = makeRoom({ id: 'r1' })
    const r2 = makeRoom({ id: 'r2' })
    useRoomStore.setState({ rooms: [r1, r2], activeRoomId: 'r1' })
    useRoomStore.getState().removeRoom('r1')
    expect(useRoomStore.getState().activeRoomId).toBe('r2')
    expect(useRoomStore.getState().rooms).toHaveLength(1)
  })

  it('sets activeRoomId to null when the last room is removed', () => {
    useRoomStore.setState({ rooms: [makeRoom({ id: 'r1' })], activeRoomId: 'r1' })
    useRoomStore.getState().removeRoom('r1')
    expect(useRoomStore.getState().activeRoomId).toBeNull()
    expect(useRoomStore.getState().rooms).toHaveLength(0)
  })

  it('keeps the current active room when a non-active room is removed', () => {
    const r1 = makeRoom({ id: 'r1' })
    const r2 = makeRoom({ id: 'r2' })
    useRoomStore.setState({ rooms: [r1, r2], activeRoomId: 'r1' })
    useRoomStore.getState().removeRoom('r2')
    expect(useRoomStore.getState().activeRoomId).toBe('r1')
    expect(useRoomStore.getState().rooms).toHaveLength(1)
  })

  it('persists the new active room id to localStorage', () => {
    useRoomStore.setState({
      rooms: [makeRoom({ id: 'r1' }), makeRoom({ id: 'r2' })],
      activeRoomId: 'r1',
    })
    useRoomStore.getState().removeRoom('r1')
    expect(localStorage.getItem('littlechat_active_room')).toBe('r2')
  })

  it('removes the localStorage key when no rooms remain', () => {
    localStorage.setItem('littlechat_active_room', 'r1')
    useRoomStore.setState({ rooms: [makeRoom({ id: 'r1' })], activeRoomId: 'r1' })
    useRoomStore.getState().removeRoom('r1')
    expect(localStorage.getItem('littlechat_active_room')).toBeNull()
  })
})

describe('addRoom', () => {
  it('appends a room to the list', () => {
    useRoomStore.getState().addRoom(makeRoom({ id: 'r1' }))
    expect(useRoomStore.getState().rooms).toHaveLength(1)
  })

  it('does not add a duplicate room', () => {
    useRoomStore.getState().addRoom(makeRoom({ id: 'r1' }))
    useRoomStore.getState().addRoom(makeRoom({ id: 'r1' }))
    expect(useRoomStore.getState().rooms).toHaveLength(1)
  })
})

describe('setActiveRoom', () => {
  it('sets activeRoomId and persists to localStorage', () => {
    useRoomStore.getState().setActiveRoom('room-abc')
    expect(useRoomStore.getState().activeRoomId).toBe('room-abc')
    expect(localStorage.getItem('littlechat_active_room')).toBe('room-abc')
  })

  it('clears localStorage when called with null', () => {
    localStorage.setItem('littlechat_active_room', 'room-abc')
    useRoomStore.getState().setActiveRoom(null)
    expect(useRoomStore.getState().activeRoomId).toBeNull()
    expect(localStorage.getItem('littlechat_active_room')).toBeNull()
  })
})

describe('markRead', () => {
  it('zeroes unreadCount and clears hasMention for the target room', () => {
    useRoomStore.setState({
      rooms: [makeRoom({ id: 'r1', unreadCount: 5, hasMention: true })],
      activeRoomId: 'r1',
    })
    useRoomStore.getState().markRead('r1')
    const room = useRoomStore.getState().rooms[0]
    expect(room.unreadCount).toBe(0)
    expect(room.hasMention).toBe(false)
  })

  it('does not affect other rooms', () => {
    useRoomStore.setState({
      rooms: [
        makeRoom({ id: 'r1', unreadCount: 3 }),
        makeRoom({ id: 'r2', unreadCount: 7 }),
      ],
      activeRoomId: 'r1',
    })
    useRoomStore.getState().markRead('r1')
    expect(useRoomStore.getState().rooms.find(r => r.id === 'r2')!.unreadCount).toBe(7)
  })
})

describe('updateUnread', () => {
  it('increments the unread count by the given amount', () => {
    useRoomStore.setState({ rooms: [makeRoom({ id: 'r1', unreadCount: 2 })], activeRoomId: null })
    useRoomStore.getState().updateUnread('r1', 3)
    expect(useRoomStore.getState().rooms[0].unreadCount).toBe(5)
  })
})

describe('setMention', () => {
  it('sets hasMention to true for the room', () => {
    useRoomStore.setState({ rooms: [makeRoom({ id: 'r1', hasMention: false })], activeRoomId: null })
    useRoomStore.getState().setMention('r1')
    expect(useRoomStore.getState().rooms[0].hasMention).toBe(true)
  })
})
