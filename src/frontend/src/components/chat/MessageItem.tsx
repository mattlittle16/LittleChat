import { useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import EmojiPicker, { type EmojiClickData } from 'emoji-picker-react'
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
import { useUserProfileStore } from '../../stores/userProfileStore'
import { ReactionBar } from './ReactionBar'
import { AttachmentGrid } from './AttachmentGrid'
import { UserAvatar } from '../common/UserAvatar'
import { cn } from '../../lib/utils'
import type { Message, Room } from '../../types'
import type { OutboxMessage } from '../../types'

const PICKER_HEIGHT = 350
const PICKER_WIDTH = 300
const PICKER_MARGIN = 8

interface MessageItemProps {
  message: Message | OutboxMessage
  isGrouped?: boolean
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

export function MessageItem({ message, isGrouped = false, isPending = false, isKeyboardSelected = false, deleteConfirmPending = false, shouldStartEditing = false }: MessageItemProps) {
  const authorId = isOutbox(message) ? null : message.author.id
  const isAuthorOnline = usePresenceStore(s => authorId ? s.isOnline(authorId) : false)
  const currentUserId = useCurrentUserStore(s => s.id)
  const isOwn = !isOutbox(message) && message.author.id === currentUserId
  const authorProfile = useUserProfileStore(s => authorId ? s.profiles[authorId] : undefined)

  const [editing, setEditing] = useState(false)
  const [editContent, setEditContent] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [hovered, setHovered] = useState(false)
  const [pickerOpen, setPickerOpen] = useState(false)
  const [pickerPosition, setPickerPosition] = useState<{ top: number; left: number } | null>(null)
  const emojiButtonRef = useRef<HTMLButtonElement>(null)

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

  // System messages (e.g. ownership transfer announcements) — render centered, no avatar, no actions
  if (!isOutbox(message) && message.isSystem) {
    return (
      <div className="px-4 py-1.5 flex items-center justify-center">
        <span className="text-xs italic text-muted-foreground text-center">{message.content}</span>
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

  function openPickerAt(rect: DOMRect) {
    const openBelow = rect.top < PICKER_HEIGHT + PICKER_MARGIN
    const top = openBelow
      ? rect.bottom + PICKER_MARGIN
      : rect.top - PICKER_HEIGHT - PICKER_MARGIN
    const left = Math.min(rect.left, window.innerWidth - PICKER_WIDTH - 8)
    setPickerPosition({ top, left })
    setPickerOpen(true)
  }

  function handleOpenEmojiPicker() {
    const rect = emojiButtonRef.current?.getBoundingClientRect()
    if (rect) openPickerAt(rect)
  }

  function handleEmojiClick(data: EmojiClickData) {
    setPickerOpen(false)
    setPickerPosition(null)
    const connection = getConnection()
    if (connection?.state !== 'Connected') return
    connection.invoke('AddReaction', {
      messageId: (message as Message).id,
      roomId: message.roomId,
      emoji: data.emoji,
    }).catch(() => {})
  }

  const showPill = !editing && (hovered || confirmDelete || pickerOpen)

  return (
    <div
      className={cn('group relative px-4 hover:bg-muted/90 dark:hover:bg-white/[0.06] hover:z-10', isGrouped ? 'pt-0 pb-0' : 'py-1', isPending && 'opacity-60', isKeyboardSelected && 'ring-2 ring-primary/40 bg-primary/5 rounded')}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div className="relative min-w-0 max-w-screen-xl">
        {!isGrouped && (
          <div className="flex items-center gap-2">
            <UserAvatar
              userId={message.author.id}
              displayName={authorProfile?.displayName ?? message.author.displayName}
              profileImageUrl={authorProfile?.profileImageUrl ?? message.author.profileImageUrl ?? null}
              avatarUrl={message.author.avatarUrl}
              size={32}
            />
            <div className="flex items-baseline gap-2">
              <button
                className="flex items-center gap-1.5 text-sm font-semibold hover:underline"
                onClick={() => openDmWithUser(message.author.id)}
                title={`DM ${message.author.displayName}`}
              >
                <span className={`inline-block w-2 h-2 rounded-full flex-shrink-0 ${isAuthorOnline ? 'bg-green-500' : 'bg-red-500'}`} />
                {authorProfile?.displayName ?? message.author.displayName}
              </button>
              <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
              {message.editedAt && (
                <span className="text-xs text-muted-foreground">(edited)</span>
              )}
            </div>
          </div>
        )}

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
              <button onClick={submitEdit} className="rounded bg-primary px-3 py-1 text-xs text-primary-foreground hover:opacity-90">Save</button>
              <button onClick={cancelEdit} className="rounded border px-3 py-1 text-xs hover:bg-muted/60">Cancel</button>
              <span className="text-xs text-muted-foreground self-center">Enter to save · Esc to cancel</span>
            </div>
          </div>
        ) : (
          <div className={cn('prose prose-sm dark:prose-invert max-w-none break-words', isGrouped ? 'grouped-prose' : 'mt-0.5')}>
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

        <ReactionBar messageId={message.id} roomId={message.roomId} reactions={message.reactions ?? []} onOpenEmojiPicker={openPickerAt} />

        {/* Unified hover action pill — emoji + edit/delete (own only) */}
        {showPill && (
        <div
          className="absolute right-0 top-0 -translate-y-1/2 flex items-center gap-0.5 border rounded-full shadow-sm px-1.5 py-0.5 z-20 bg-zinc-200 dark:bg-zinc-600"
        >
          {/* Emoji reaction button — always shown */}
          <button
            ref={emojiButtonRef}
            onClick={handleOpenEmojiPicker}
            className="rounded-full w-6 h-6 flex items-center justify-center text-sm text-muted-foreground hover:bg-muted/60 hover:text-foreground"
            title="Add reaction"
          >
            🙂
          </button>

          {/* Edit / delete — own messages only */}
          {isOwn && !confirmDelete && (
            <>
              <div className="w-px h-3 bg-border mx-0.5" />
              <button onClick={startEdit} className="rounded-full px-2 py-0.5 text-xs text-muted-foreground hover:bg-muted/60 hover:text-foreground">
                Edit
              </button>
              <button onClick={() => setConfirmDelete(true)} className="rounded-full px-2 py-0.5 text-xs text-destructive hover:bg-destructive/10">
                Delete
              </button>
            </>
          )}
          {isOwn && confirmDelete && (
            <>
              <div className="w-px h-3 bg-border mx-0.5" />
              <span className="text-xs text-muted-foreground px-1">Delete?</span>
              <button onClick={handleDelete} className="rounded-full px-2 py-0.5 text-xs text-destructive hover:bg-destructive/10">Yes</button>
              <button onClick={() => setConfirmDelete(false)} className="rounded-full px-2 py-0.5 text-xs hover:bg-muted/60">No</button>
            </>
          )}
        </div>
        )}
      </div>

      {/* Emoji picker portal */}
      {pickerOpen && pickerPosition && createPortal(
        <>
          <div style={{ position: 'fixed', inset: 0, zIndex: 9998 }} onClick={() => { setPickerOpen(false); setPickerPosition(null) }} />
          <div style={{ position: 'fixed', top: pickerPosition.top, left: pickerPosition.left, zIndex: 9999 }}>
            <EmojiPicker onEmojiClick={handleEmojiClick} lazyLoadEmojis height={PICKER_HEIGHT} width={PICKER_WIDTH} />
          </div>
        </>,
        document.body
      )}
    </div>
  )
}
