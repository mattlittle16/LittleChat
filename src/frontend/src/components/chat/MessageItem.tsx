import { useRef, useState } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter'
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism'
import { api } from '../../services/apiClient'
import { getCurrentUserId } from '../../services/authService'
import { getConnection } from '../../services/signalrClient'
import { useRoomStore } from '../../stores/roomStore'
import { usePresenceStore } from '../../stores/presenceStore'
import { ReactionBar } from './ReactionBar'
import type { Message, Room } from '../../types'
import type { OutboxMessage } from '../../types'

interface MessageItemProps {
  message: Message | OutboxMessage
  isPending?: boolean
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

export function MessageItem({ message, isPending = false }: MessageItemProps) {
  const authorId = isOutbox(message) ? null : message.author.id
  const isAuthorOnline = usePresenceStore(s => authorId ? s.isOnline(authorId) : false)
  const currentUserId = getCurrentUserId()
  const isOwn = !isOutbox(message) && message.author.id === currentUserId

  const [editing, setEditing] = useState(false)
  const [editContent, setEditContent] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const editRef = useRef<HTMLTextAreaElement>(null)

  if (isOutbox(message)) {
    return (
      <div className={`flex gap-3 px-4 py-1 ${message.status === 'failed' ? 'opacity-60' : 'opacity-50'}`}>
        <div className="w-8 h-8 rounded-full bg-muted flex-shrink-0" />
        <div className="flex-1 min-w-0">
          <div className="flex items-baseline gap-2">
            <span className="text-sm font-semibold">You</span>
            <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
            <span className="text-xs text-muted-foreground">
              {message.status === 'sending' ? '· Sending…' : message.status === 'failed' ? '· Failed' : '· Pending'}
            </span>
          </div>
          <p className="text-sm mt-0.5 whitespace-pre-wrap break-words">{message.content}</p>
        </div>
      </div>
    )
  }

  function startEdit() {
    setEditContent(message.content)
    setEditing(true)
    setTimeout(() => editRef.current?.focus(), 0)
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

  function handleEditKey(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); submitEdit() }
    if (e.key === 'Escape') cancelEdit()
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
    <div className={`group relative flex gap-3 px-4 py-1 hover:bg-muted/40 ${isPending ? 'opacity-60' : ''}`}>
      <button
        className="relative flex-shrink-0 hover:opacity-80 transition-opacity"
        onClick={() => openDmWithUser(message.author.id)}
        title={`DM ${message.author.displayName}`}
      >
        {message.author.avatarUrl ? (
          <img
            src={message.author.avatarUrl}
            alt={message.author.displayName}
            className="w-8 h-8 rounded-full object-cover"
          />
        ) : (
          <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center text-sm font-semibold">
            {message.author.displayName.charAt(0).toUpperCase()}
          </div>
        )}
        {isAuthorOnline && (
          <span className="absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-green-500 ring-1 ring-background" />
        )}
      </button>

      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          <button
            className="text-sm font-semibold hover:underline"
            onClick={() => openDmWithUser(message.author.id)}
          >
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
            <textarea
              ref={editRef}
              value={editContent}
              onChange={e => setEditContent(e.target.value)}
              onKeyDown={handleEditKey}
              rows={2}
              className="w-full resize-none rounded-md border bg-background px-3 py-2 text-sm
                         focus:outline-none focus:ring-2 focus:ring-ring"
            />
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
                  return <code className={className} {...props}>{children}</code>
                },
              }}
            >
              {message.content}
            </ReactMarkdown>
          </div>
        )}

        {message.attachment && (() => {
          const isImage = /\.(png|jpe?g|gif|webp|svg|bmp|avif)$/i.test(message.attachment.fileName)
          return isImage ? (
            <a href={message.attachment.url} target="_blank" rel="noreferrer" className="mt-1 block">
              <img
                src={message.attachment.url}
                alt={message.attachment.fileName}
                className="max-w-xs max-h-64 rounded-md border object-contain"
              />
            </a>
          ) : (
            <a
              href={message.attachment.url}
              download={message.attachment.fileName}
              className="mt-1 inline-flex items-center gap-1 text-xs text-primary hover:underline"
            >
              📎 {message.attachment.fileName}
              <span className="text-muted-foreground">
                ({(message.attachment.fileSize / 1024).toFixed(1)} KB)
              </span>
            </a>
          )
        })()}

        <ReactionBar messageId={message.id} roomId={message.roomId} reactions={message.reactions} />
      </div>

      {/* Hover context menu — own messages only (T094) */}
      {isOwn && !editing && (
        <div className="absolute right-4 top-1 hidden group-hover:flex items-center gap-1 bg-background border rounded shadow-sm px-1 py-0.5">
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
