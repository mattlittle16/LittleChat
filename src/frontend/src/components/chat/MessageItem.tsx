import { useState } from 'react'
import { InlineMarkdownEditor } from './InlineMarkdownEditor'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter'
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism'
import { api } from '../../services/apiClient'
import { getConnection } from '../../services/signalrClient'
import { useRoomStore } from '../../stores/roomStore'
import { usePresenceStore } from '../../stores/presenceStore'
import { useCurrentUserStore } from '../../stores/currentUserStore'
import { ReactionBar } from './ReactionBar'
import { AttachmentGrid } from './AttachmentGrid'
import { cn } from '../../lib/utils'
import type { Message, Room } from '../../types'
import type { OutboxMessage } from '../../types'

interface MessageItemProps {
  message: Message | OutboxMessage
  isPending?: boolean
  isKeyboardSelected?: boolean
  deleteConfirmPending?: boolean
  shouldStartEditing?: boolean
}

function isOutbox(m: Message | OutboxMessage): m is OutboxMessage {
  return 'clientId' in m
}

function formatTime(isoOrTs: string | number): string {
  const date = typeof isoOrTs === 'number' ? new Date(isoOrTs) : new Date(isoOrTs)
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

async function openDmWithUser(userId: string) {
  const { loadRooms, setActiveRoom } = useRoomStore.getState()
  const room = await api.post<Room>('/api/rooms/dm', { targetUserId: userId })
  await loadRooms()
  setActiveRoom(room.id)
}

export function MessageItem({ message, isPending = false, isKeyboardSelected = false, deleteConfirmPending = false, shouldStartEditing = false }: MessageItemProps) {
  const authorId = isOutbox(message) ? null : message.author.id
  const isAuthorOnline = usePresenceStore(s => authorId ? s.isOnline(authorId) : false)
  const currentUserId = useCurrentUserStore(s => s.id)
  const isOwn = !isOutbox(message) && message.author.id === currentUserId

  const [editing, setEditing] = useState(false)
  const [editContent, setEditContent] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [hovered, setHovered] = useState(false)

  // Keyboard shortcut: parent sets shouldStartEditing to trigger inline edit mode.
  // React "derived state during render" pattern — setState inside render is processed
  // in the same cycle without an extra effect flush.
  const [prevShouldStartEditing, setPrevShouldStartEditing] = useState(shouldStartEditing)
  if (prevShouldStartEditing !== shouldStartEditing) {
    setPrevShouldStartEditing(shouldStartEditing)
    if (shouldStartEditing && !editing && !isOutbox(message)) {
      setEditContent(message.content)
      setEditing(true)
    }
  }

  if (isOutbox(message)) {
    return (
      <div className={`px-4 py-1 ${message.status === 'failed' ? 'opacity-60' : 'opacity-50'}`}>
        <div className="flex items-baseline gap-2">
          <span className="text-sm font-semibold">You</span>
          <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
          <span className="text-xs text-muted-foreground">
            {message.status === 'sending' ? '· Sending…' : message.status === 'failed' ? '· Failed' : '· Pending'}
          </span>
        </div>
        <p className="text-sm mt-0.5 whitespace-pre-wrap break-words">{message.content}</p>
      </div>
    )
  }

  function startEdit() {
    setEditContent(message.content)
    setEditing(true)
  }

  function cancelEdit() {
    setEditing(false)
    setEditContent('')
  }

  function submitEdit() {
    const trimmed = editContent.trim()
    if (!trimmed || trimmed === message.content) { cancelEdit(); return }
    const connection = getConnection()
    if (connection?.state === 'Connected') {
      connection.invoke('EditMessage', {
        messageId: (message as Message).id,
        roomId: message.roomId,
        content: trimmed,
      }).catch(() => {})
    }
    cancelEdit()
  }

  function handleDelete() {
    const connection = getConnection()
    if (connection?.state === 'Connected') {
      connection.invoke('DeleteMessage', {
        messageId: (message as Message).id,
        roomId: message.roomId,
      }).catch(() => {})
    }
    setConfirmDelete(false)
  }

  return (
    <div
      className={cn('relative px-4 py-1 hover:bg-muted/40', isPending && 'opacity-60', isKeyboardSelected && 'ring-2 ring-primary/40 bg-primary/5 rounded')}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div className="min-w-0">
        <div className="flex items-baseline gap-2">
          <button
            className="flex items-center gap-1.5 text-sm font-semibold hover:underline"
            onClick={() => openDmWithUser(message.author.id)}
            title={`DM ${message.author.displayName}`}
          >
            <span className={`inline-block w-2 h-2 rounded-full flex-shrink-0 ${isAuthorOnline ? 'bg-green-500' : 'bg-red-500'}`} />
            {message.author.displayName}
          </button>
          <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
          {/* T095: edited label */}
          {message.editedAt && (
            <span className="text-xs text-muted-foreground">(edited)</span>
          )}
        </div>

        {/* Inline edit textarea */}
        {editing ? (
          <div className="mt-1">
            <div onKeyDown={e => { if (e.key === 'Escape') cancelEdit() }}>
              <InlineMarkdownEditor
                value={editContent}
                onChange={setEditContent}
                onSubmit={submitEdit}
                minHeight="2.25rem"
                maxHeight="12rem"
                autoFocus
              />
            </div>
            <div className="flex gap-2 mt-1">
              <button
                onClick={submitEdit}
                className="rounded bg-primary px-3 py-1 text-xs text-primary-foreground hover:opacity-90"
              >
                Save
              </button>
              <button
                onClick={cancelEdit}
                className="rounded border px-3 py-1 text-xs hover:bg-muted/60"
              >
                Cancel
              </button>
              <span className="text-xs text-muted-foreground self-center">Enter to save · Esc to cancel</span>
            </div>
          </div>
        ) : (
          <div className="prose prose-sm dark:prose-invert max-w-none mt-0.5">
            <ReactMarkdown
              remarkPlugins={[remarkGfm]}
              components={{
                a({ href, children, ...props }) {
                  const safeHref = href ?? ''
                  const isSafe = safeHref.startsWith('https://') || safeHref.startsWith('http://')
                  if (!isSafe) return <span>{children}</span>
                  return <a href={safeHref} target="_blank" rel="noopener noreferrer" {...props}>{children}</a>
                },
                code({ className, children, ...props }) {
                  const match = /language-(\w+)/.exec(className ?? '')
                  const isBlock = !props.ref && match
                  if (isBlock) {
                    return (
                      <SyntaxHighlighter style={oneDark} language={match[1]} PreTag="div">
                        {String(children).replace(/\n$/, '')}
                      </SyntaxHighlighter>
                    )
                  }
                  return (
                    <code
                      className="rounded px-[0.35em] py-[0.1em] text-[0.85em] font-mono"
                      style={{
                        background: 'hsl(var(--muted))',
                        border: '1px solid hsl(var(--border))',
                        color: 'hsl(var(--foreground))',
                      }}
                      {...props}
                    >
                      {children}
                    </code>
                  )
                },
              }}
            >
              {message.content}
            </ReactMarkdown>
          </div>
        )}

        {isKeyboardSelected && (
          <span className="text-xs text-muted-foreground mt-0.5 block">
            {deleteConfirmPending ? 'Press Del again to confirm · Esc to cancel' : 'E to edit · Del to delete · Esc to cancel'}
          </span>
        )}

        <AttachmentGrid attachments={message.attachments} />

        <ReactionBar messageId={message.id} roomId={message.roomId} reactions={message.reactions} />
      </div>

      {/* Hover context menu — own messages only (T094) */}
      {isOwn && !editing && (hovered || confirmDelete) && (
        <div className="absolute right-4 top-1 flex items-center gap-1 border rounded shadow-sm px-1 py-0.5"
          style={{ background: 'hsl(var(--background))' }}>
          <button
            onClick={startEdit}
            className="rounded px-2 py-0.5 text-xs hover:bg-muted/60"
          >
            Edit
          </button>
          {confirmDelete ? (
            <>
              <span className="text-xs text-muted-foreground">Delete?</span>
              <button
                onClick={handleDelete}
                className="rounded px-2 py-0.5 text-xs text-destructive hover:bg-destructive/10"
              >
                Yes
              </button>
              <button
                onClick={() => setConfirmDelete(false)}
                className="rounded px-2 py-0.5 text-xs hover:bg-muted/60"
              >
                No
              </button>
            </>
          ) : (
            <button
              onClick={() => setConfirmDelete(true)}
              className="rounded px-2 py-0.5 text-xs text-destructive hover:bg-destructive/10"
            >
              Delete
            </button>
          )}
        </div>
      )}
    </div>
  )
}
