import { useRef, useState } from 'react'
import { useOutboxStore } from '../../stores/outboxStore'
import { getConnection } from '../../services/signalrClient'

const TYPING_DEBOUNCE_MS = 500

const MAX_LENGTH = 4_000

interface MessageInputProps {
  roomId: string
  disabled?: boolean
}

export function MessageInput({ roomId, disabled = false }: MessageInputProps) {
  const [content, setContent] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const typingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const { enqueue, messages: outboxMessages, retryAll } = useOutboxStore()
  const hasFailed = outboxMessages.some(m => m.status === 'failed')
  const isConnected = getConnection()?.state === 'Connected'
  const isDisabled = disabled || !isConnected

  function notifyTyping() {
    const connection = getConnection()
    if (connection?.state !== 'Connected') return

    // Debounce: at most one StartTyping per 500ms
    if (typingTimerRef.current) return
    connection.invoke('StartTyping', { roomId }).catch(() => {})
    typingTimerRef.current = setTimeout(() => {
      typingTimerRef.current = null
    }, TYPING_DEBOUNCE_MS)
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      submit()
    }
  }

  async function submit() {
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > MAX_LENGTH) return
    setContent('')
    await enqueue(roomId, trimmed)
    textareaRef.current?.focus()
  }

  const remaining = MAX_LENGTH - content.length
  const overLimit = remaining < 0

  return (
    <div className="border-t p-3 flex flex-col gap-2">
      {hasFailed && (
        <div className="flex items-center gap-2 text-sm text-destructive">
          <span>Some messages failed to send.</span>
          <button
            onClick={retryAll}
            className="underline hover:no-underline"
          >
            Retry all
          </button>
        </div>
      )}
      <div className="flex items-end gap-2">
        <textarea
          ref={textareaRef}
          value={content}
          onChange={e => { setContent(e.target.value); notifyTyping() }}
          onKeyDown={handleKeyDown}
          disabled={isDisabled}
          rows={1}
          placeholder={isDisabled ? 'Reconnecting…' : 'Message'}
          className="flex-1 resize-none rounded-md border bg-background px-3 py-2 text-sm
                     placeholder:text-muted-foreground focus:outline-none focus:ring-2
                     focus:ring-ring disabled:opacity-50"
          style={{ minHeight: '2.5rem', maxHeight: '10rem' }}
        />
        <button
          onClick={submit}
          disabled={isDisabled || !content.trim() || overLimit}
          className="rounded-md bg-primary px-4 py-2 text-sm text-primary-foreground
                     hover:opacity-90 disabled:opacity-50"
        >
          Send
        </button>
      </div>
      {content.length > MAX_LENGTH * 0.9 && (
        <p className={`text-xs text-right ${overLimit ? 'text-destructive' : 'text-muted-foreground'}`}>
          {remaining} characters remaining
        </p>
      )}
    </div>
  )
}
