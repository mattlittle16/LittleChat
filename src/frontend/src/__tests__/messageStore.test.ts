import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useMessageStore, getRoomMessages } from '../stores/messageStore'
import type { Message } from '../types'

vi.mock('../services/apiClient', () => ({
  api: {
    get: vi.fn(),
  },
}))

import { api } from '../services/apiClient'
const mockApi = api as { get: ReturnType<typeof vi.fn> }

function makeMessage(overrides: Partial<Message> = {}): Message {
  return {
    id: 'msg-1',
    roomId: 'room-1',
    author: { id: 'user-1', displayName: 'Alice', avatarUrl: null },
    content: 'Hello',
    attachments: [],
    reactions: [],
    createdAt: '2024-01-01T00:00:00Z',
    editedAt: null,
    ...overrides,
  }
}

beforeEach(() => {
  useMessageStore.setState({
    messages: new Map(),
    hasMoreByRoom: new Map(),
    hasNewerByRoom: new Map(),
    pendingAroundByRoom: new Map(),
    scrollToMessageId: null,
  })
  vi.clearAllMocks()
})

describe('addMessage', () => {
  it('inserts a new message into the map', () => {
    const msg = makeMessage()
    useMessageStore.getState().addMessage(msg)
    expect(useMessageStore.getState().messages.get('msg-1')).toEqual(msg)
  })

  it('replaces an existing message with the same id', () => {
    const original = makeMessage({ content: 'Original' })
    const updated = makeMessage({ content: 'Updated' })
    useMessageStore.getState().addMessage(original)
    useMessageStore.getState().addMessage(updated)
    expect(useMessageStore.getState().messages.get('msg-1')?.content).toBe('Updated')
    expect(useMessageStore.getState().messages.size).toBe(1)
  })
})

describe('removeMessage', () => {
  it('deletes a message by id', () => {
    useMessageStore.getState().addMessage(makeMessage())
    useMessageStore.getState().removeMessage('msg-1')
    expect(useMessageStore.getState().messages.has('msg-1')).toBe(false)
  })

  it('is a no-op for unknown ids', () => {
    useMessageStore.getState().removeMessage('does-not-exist')
    expect(useMessageStore.getState().messages.size).toBe(0)
  })
})

describe('updateReactions', () => {
  it('replaces emoji entry with new count and users', () => {
    const msg = makeMessage({ reactions: [{ emoji: '👍', count: 1, users: ['Alice'] }] })
    useMessageStore.getState().addMessage(msg)
    useMessageStore.getState().updateReactions('msg-1', '👍', 2, ['Alice', 'Bob'])
    const reactions = useMessageStore.getState().messages.get('msg-1')!.reactions
    expect(reactions).toHaveLength(1)
    expect(reactions[0]).toEqual({ emoji: '👍', count: 2, users: ['Alice', 'Bob'] })
  })

  it('removes emoji entry when count is 0', () => {
    const msg = makeMessage({ reactions: [{ emoji: '👍', count: 1, users: ['Alice'] }] })
    useMessageStore.getState().addMessage(msg)
    useMessageStore.getState().updateReactions('msg-1', '👍', 0, [])
    expect(useMessageStore.getState().messages.get('msg-1')!.reactions).toHaveLength(0)
  })

  it('adds a new emoji without affecting existing ones', () => {
    const msg = makeMessage({ reactions: [{ emoji: '👍', count: 1, users: ['Alice'] }] })
    useMessageStore.getState().addMessage(msg)
    useMessageStore.getState().updateReactions('msg-1', '❤️', 1, ['Bob'])
    const reactions = useMessageStore.getState().messages.get('msg-1')!.reactions
    expect(reactions).toHaveLength(2)
  })

  it('is a no-op for unknown message ids', () => {
    useMessageStore.getState().updateReactions('unknown', '👍', 1, ['Alice'])
    expect(useMessageStore.getState().messages.size).toBe(0)
  })
})

describe('clearRoom', () => {
  it('removes all messages for the specified room', () => {
    useMessageStore.getState().addMessage(makeMessage({ id: 'a', roomId: 'room-1' }))
    useMessageStore.getState().addMessage(makeMessage({ id: 'b', roomId: 'room-1' }))
    useMessageStore.getState().addMessage(makeMessage({ id: 'c', roomId: 'room-2' }))
    useMessageStore.getState().clearRoom('room-1')
    const { messages } = useMessageStore.getState()
    expect(messages.has('a')).toBe(false)
    expect(messages.has('b')).toBe(false)
    expect(messages.has('c')).toBe(true)
  })

  it('clears pagination maps for the room', () => {
    useMessageStore.setState({
      messages: new Map(),
      hasMoreByRoom: new Map([['room-1', true], ['room-2', true]]),
      hasNewerByRoom: new Map([['room-1', true]]),
      pendingAroundByRoom: new Map(),
      scrollToMessageId: null,
    })
    useMessageStore.getState().clearRoom('room-1')
    const { hasMoreByRoom, hasNewerByRoom } = useMessageStore.getState()
    expect(hasMoreByRoom.has('room-1')).toBe(false)
    expect(hasMoreByRoom.has('room-2')).toBe(true)
    expect(hasNewerByRoom.has('room-1')).toBe(false)
  })
})

describe('loadPage', () => {
  it('merges fetched messages into the store and sets hasMore', async () => {
    const msgs = [makeMessage({ id: 'a' }), makeMessage({ id: 'b' })]
    mockApi.get.mockResolvedValueOnce({ messages: msgs, hasMore: true, hasNewer: false })

    await useMessageStore.getState().loadPage('room-1')

    const { messages, hasMoreByRoom } = useMessageStore.getState()
    expect(messages.has('a')).toBe(true)
    expect(messages.has('b')).toBe(true)
    expect(hasMoreByRoom.get('room-1')).toBe(true)
  })
})

describe('loadAroundMessage', () => {
  it('replaces room messages with the context window and sets both pagination flags', async () => {
    // Pre-existing message in room-1
    useMessageStore.getState().addMessage(makeMessage({ id: 'old', roomId: 'room-1' }))
    const contextMsgs = [makeMessage({ id: 'ctx-1' }), makeMessage({ id: 'ctx-2' })]
    mockApi.get.mockResolvedValueOnce({ messages: contextMsgs, hasMore: true, hasNewer: true })

    await useMessageStore.getState().loadAroundMessage('room-1', 'ctx-1')

    const { messages, hasMoreByRoom, hasNewerByRoom } = useMessageStore.getState()
    expect(messages.has('old')).toBe(false)
    expect(messages.has('ctx-1')).toBe(true)
    expect(messages.has('ctx-2')).toBe(true)
    expect(hasMoreByRoom.get('room-1')).toBe(true)
    expect(hasNewerByRoom.get('room-1')).toBe(true)
  })
})

describe('getRoomMessages', () => {
  it('returns messages sorted by createdAt ascending', () => {
    const msgs = new Map<string, Message>([
      ['b', makeMessage({ id: 'b', roomId: 'r', createdAt: '2024-01-03T00:00:00Z' })],
      ['a', makeMessage({ id: 'a', roomId: 'r', createdAt: '2024-01-01T00:00:00Z' })],
      ['c', makeMessage({ id: 'c', roomId: 'r', createdAt: '2024-01-02T00:00:00Z' })],
    ])
    const result = getRoomMessages(msgs, 'r')
    expect(result.map(m => m.id)).toEqual(['a', 'c', 'b'])
  })

  it('uses id as a tiebreaker when createdAt is equal', () => {
    const same = '2024-01-01T00:00:00Z'
    const msgs = new Map<string, Message>([
      ['z', makeMessage({ id: 'z', roomId: 'r', createdAt: same })],
      ['a', makeMessage({ id: 'a', roomId: 'r', createdAt: same })],
    ])
    const result = getRoomMessages(msgs, 'r')
    expect(result[0].id).toBe('a')
    expect(result[1].id).toBe('z')
  })

  it('excludes messages from other rooms', () => {
    const msgs = new Map<string, Message>([
      ['x', makeMessage({ id: 'x', roomId: 'room-A' })],
      ['y', makeMessage({ id: 'y', roomId: 'room-B' })],
    ])
    expect(getRoomMessages(msgs, 'room-A').map(m => m.id)).toEqual(['x'])
  })
})
