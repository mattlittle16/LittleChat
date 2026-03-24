import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MessageInput } from '../components/chat/MessageInput'

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('../stores/outboxStore', () => ({
  useOutboxStore: () => ({ enqueue: vi.fn(), messages: [], retryAll: vi.fn() }),
}))
vi.mock('../services/signalrClient', () => ({
  getConnection: () => null,
}))
vi.mock('../services/apiClient', () => ({
  getAccessToken: vi.fn().mockReturnValue('tok'),
  api: { get: vi.fn() },
}))
vi.mock('emoji-picker-react', () => ({ default: () => null }))
vi.mock('../components/chat/GifPicker', () => ({ GifPicker: () => null }))
vi.mock('../components/files/FileUploadProgress', () => ({ FileUploadProgress: () => null }))
// InlineMarkdownEditor wraps Tiptap which needs a complex environment — use a textarea stub
vi.mock('../components/chat/InlineMarkdownEditor', () => ({
  InlineMarkdownEditor: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea aria-label="Message" value={value} onChange={e => onChange(e.target.value)} />
  ),
}))

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('MessageInput', () => {
  it('renders without crashing', () => {
    render(<MessageInput roomId="room-1" />)
  })

  it('renders the Attach files button', () => {
    render(<MessageInput roomId="room-1" />)
    expect(screen.getByLabelText('Attach files')).toBeInTheDocument()
  })

  it('renders the Insert emoji button', () => {
    render(<MessageInput roomId="room-1" />)
    expect(screen.getByLabelText('Insert emoji')).toBeInTheDocument()
  })

  it('disables attachment button when disabled prop is true', () => {
    render(<MessageInput roomId="room-1" disabled />)
    const attachButton = screen.getByLabelText('Attach files')
    expect(attachButton).toBeDisabled()
  })

  it('disables emoji button when disabled prop is true', () => {
    render(<MessageInput roomId="room-1" disabled />)
    const emojiButton = screen.getByLabelText('Insert emoji')
    expect(emojiButton).toBeDisabled()
  })
})
