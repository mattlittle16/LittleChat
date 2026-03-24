import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MessageItem } from '../components/chat/MessageItem'
import type { Message } from '../types'

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('../stores/presenceStore', () => ({
  usePresenceStore: () => false,
}))
vi.mock('../stores/currentUserStore', () => ({
  useCurrentUserStore: () => 'user-owner',
}))
vi.mock('../stores/userProfileStore', () => ({
  useUserProfileStore: () => undefined,
}))
vi.mock('../services/signalrClient', () => ({
  getConnection: () => null,
}))
vi.mock('../stores/roomStore', () => ({
  useRoomStore: {
    getState: () => ({ loadRooms: vi.fn(), setActiveRoom: vi.fn() }),
  },
}))
vi.mock('../services/apiClient', () => ({
  api: { post: vi.fn() },
}))
// emoji-picker-react is heavy; we don't need it for smoke tests
vi.mock('emoji-picker-react', () => ({
  default: () => null,
}))

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeMessage(overrides: Partial<Message> = {}): Message {
  return {
    id: 'msg-1',
    roomId: 'room-1',
    content: 'Hello **world**',
    author: {
      id: 'user-2',
      displayName: 'Bob',
      avatarUrl: null,
      profileImageUrl: null,
    },
    createdAt: '2024-01-15T10:00:00Z',
    editedAt: null,
    reactions: [],
    attachments: [],
    isSystem: false,
    ...overrides,
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('MessageItem', () => {
  it('renders without crashing', () => {
    render(<MessageItem message={makeMessage()} />)
  })

  it('renders the author display name', () => {
    render(<MessageItem message={makeMessage()} />)
    expect(screen.getByText('Bob')).toBeInTheDocument()
  })

  it('renders markdown content', () => {
    render(<MessageItem message={makeMessage({ content: 'Hello **world**' })} />)
    // react-markdown renders <strong>world</strong>
    expect(screen.getByText('world')).toBeInTheDocument()
  })

  it('renders the (edited) indicator when editedAt is set', () => {
    render(<MessageItem message={makeMessage({ editedAt: '2024-01-15T11:00:00Z' })} />)
    expect(screen.getByText('(edited)')).toBeInTheDocument()
  })

  it('renders system messages centered without author', () => {
    const { container } = render(<MessageItem message={makeMessage({ isSystem: true, content: 'Room was created' })} />)
    expect(container.querySelector('.italic')).toBeInTheDocument()
    expect(screen.queryByText('Bob')).not.toBeInTheDocument()
  })

  it('renders a pending outbox message', () => {
    const outboxMsg = {
      clientId: 'c1',
      roomId: 'room-1',
      content: 'Sending…',
      status: 'sending' as const,
      createdAt: Date.now(),
    }
    render(<MessageItem message={outboxMsg} />)
    expect(screen.getByText('Sending…')).toBeInTheDocument()
    expect(screen.getByText('· Sending…')).toBeInTheDocument()
  })

  it('does not render Edit button for messages by another user', () => {
    // currentUser is 'user-owner', message author is 'user-2'
    render(<MessageItem message={makeMessage()} />)
    expect(screen.queryByText('Edit')).not.toBeInTheDocument()
  })

  it('does not render Delete button for messages by another user', () => {
    render(<MessageItem message={makeMessage()} />)
    expect(screen.queryByText('Delete')).not.toBeInTheDocument()
  })
})
