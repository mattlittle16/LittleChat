import { memo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import EmojiPicker, { type EmojiClickData } from 'emoji-picker-react'
import { InlineMarkdownEditor } from './InlineMarkdownEditor'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMentions from '../../lib/remarkMentions'
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
import { AvatarLightbox } from '../common/AvatarLightbox'
import { cn } from '../../lib/utils'
import type { Message, Room } from '../../types'
import type { OutboxMessage } from '../../types'

interface MentionNode {
  value: string
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const remarkRehypeOptions: any = {
  handlers: {
    mention(_state: object, node: MentionNode) {
      return {
        type: 'element',
        tagName: 'mention',
        properties: { value: node.value },
        children: [{ type: 'text', value: node.value }],
      }
    },
  },
}

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

export const MessageItem = memo(function MessageItem({ message, isGrouped = false, isPending = false, isKeyboardSelected = false, deleteConfirmPending = false, shouldStartEditing = false }: MessageItemProps) {
  const authorId = isOutbox(message) ? null : message.author.id
  const isSystemAuthor = !authorId || authorId === '00000000-0000-0000-0000-000000000000'
  const isAuthorOnline = usePresenceStore(s => (!isSystemAuthor && authorId) ? s.isOnline(authorId) : false)
  const showAuthorOnline = isSystemAuthor || isAuthorOnline
  const currentUserId = useCurrentUserStore(s => s.id)
  const isOwn = !isOutbox(message) && message.author.id === currentUserId
  const authorProfile = useUserProfileStore(s => authorId ? s.profiles[authorId] : undefined)

  const [editing, setEditing] = useState(false)
  const [editContent, setEditContent] = useState('')
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [hovered, setHovered] = useState(false)
  const [pickerOpen, setPickerOpen] = useState(false)
  const [pickerPosition, setPickerPosition] = useState<{ top: number; left: number } | null>(null)
  const [lightbox, setLightbox] = useState<{ src: string; authed: boolean } | null>(null)
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
      }).catch(err => console.error('[MessageItem] EditMessage failed', err))
    }
    cancelEdit()
  }

  function handleDelete() {
    const connection = getConnection()
    if (connection?.state === 'Connected') {
      connection.invoke('DeleteMessage', {
        messageId: (message as Message).id,
        roomId: message.roomId,
      }).catch(err => console.error('[MessageItem] DeleteMessage failed', err))
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
    }).catch(err => console.error('[MessageItem] AddReaction failed', err))
  }

  const showPill = !editing && (hovered || confirmDelete || pickerOpen)
  const hasReactions = !isOutbox(message) && (message.reactions?.length ?? 0) > 0

  // Edit/delete buttons shared between both pill variants
  const editDeleteButtons = !confirmDelete ? (
    <>
      <button onClick={startEdit} className="rounded-full px-2.5 py-1 text-sm text-muted-foreground hover:bg-muted/60 hover:text-foreground">
        Edit
      </button>
      <button onClick={() => setConfirmDelete(true)} className="rounded-full px-2.5 py-1 text-sm text-destructive hover:bg-muted/60">
        Delete
      </button>
    </>
  ) : (
    <>
      <span className="text-xs text-muted-foreground px-1">Delete?</span>
      <button onClick={handleDelete} className="rounded-full px-2.5 py-1 text-sm text-destructive hover:bg-muted/60">Yes</button>
      <button onClick={() => setConfirmDelete(false)} className="rounded-full px-2.5 py-1 text-sm hover:bg-muted/60">No</button>
    </>
  )

  // Full pill for no-reaction messages: emoji button + edit/delete (own only)
  const pillButtons = (
    <>
      <button
        ref={emojiButtonRef}
        onClick={handleOpenEmojiPicker}
        className="rounded-full px-1.5 py-1 flex items-center justify-center text-sm text-muted-foreground hover:bg-muted/60 hover:text-foreground"
        title="Add reaction"
        aria-label="Add reaction"
      >
        🙂
      </button>
      {isOwn && (
        <>
          <div className="w-px h-4 bg-border mx-0.5" />
          {editDeleteButtons}
        </>
      )}
    </>
  )

  // Inline pill for messages that already have reactions:
  // - not own: no pill (+ handles adding emojis)
  // - own: edit/delete only (+ handles adding emojis)
  const inlinePill = hasReactions && showPill && isOwn ? editDeleteButtons : undefined

  return (
    <div
      data-message-id={isOutbox(message) ? undefined : message.id}
      className={cn('group relative px-4 hover:bg-muted/90 dark:hover:bg-white/[0.06] hover:z-10', isGrouped ? 'pt-0.5 pb-3' : 'pt-2 pb-3', isPending && 'opacity-60', isKeyboardSelected && 'ring-2 ring-primary/40 bg-primary/5 rounded')}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div className="relative min-w-0 max-w-screen-xl">
        {!isGrouped && (
          <div className="flex items-center gap-2">
            <button
              className="flex-shrink-0 rounded-full focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              onClick={() => {
                const profileUrl = authorProfile?.profileImageUrl ?? message.author.profileImageUrl ?? null
                const avatarUrl = message.author.avatarUrl ?? null
                if (profileUrl) setLightbox({ src: profileUrl, authed: true })
                else if (avatarUrl) setLightbox({ src: avatarUrl, authed: false })
              }}
            >
              <UserAvatar
                userId={message.author.id}
                displayName={authorProfile?.displayName ?? message.author.displayName}
                profileImageUrl={authorProfile?.profileImageUrl ?? message.author.profileImageUrl ?? null}
                avatarUrl={message.author.avatarUrl}
                size={32}
              />
            </button>
            <div className="flex items-baseline gap-2">
              <button
                className="flex items-center gap-1.5 text-sm font-semibold hover:underline"
                onClick={() => openDmWithUser(message.author.id)}
                title={`DM ${message.author.displayName}`}
              >
                <span className={`inline-block w-2 h-2 rounded-full flex-shrink-0 ${showAuthorOnline ? 'bg-green-500' : 'bg-red-500'}`} />
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
              remarkPlugins={[remarkGfm, remarkMentions]}
              remarkRehypeOptions={remarkRehypeOptions}
              components={{
                mention({ children, ...props }: { children?: unknown; [key: string]: unknown }) {
                  const value: string = (props.value as string) ?? String(children)
                  const isTopic = value.toLowerCase() === '@topic'
                  return (
                    <span className={isTopic ? 'mention-topic' : 'mention'}>
                      {value}
                    </span>
                  )
                },
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

        <ReactionBar
          messageId={message.id}
          roomId={message.roomId}
          reactions={message.reactions ?? []}
          onOpenEmojiPicker={openPickerAt}
          inlinePill={inlinePill}
        />
      </div>

      {/* Hover action pill — only shown when message has no reactions; otherwise rendered inline in ReactionBar */}
      {!hasReactions && (
        <div
          className={cn(
            'absolute left-4 bottom-0 translate-y-1/2 flex items-center gap-0.5 border rounded-full shadow-sm px-1.5 py-0.5 z-20 bg-zinc-200 dark:bg-zinc-600',
            'transition-opacity duration-150',
            showPill ? 'opacity-100' : 'opacity-0 pointer-events-none',
          )}
        >
          {pillButtons}
        </div>
      )}

      {lightbox && (
        <AvatarLightbox
          src={lightbox.src}
          alt="Profile photo"
          authed={lightbox.authed}
          onClose={() => setLightbox(null)}
        />
      )}

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
})
