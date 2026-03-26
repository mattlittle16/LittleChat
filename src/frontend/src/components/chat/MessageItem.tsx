import { memo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import EmojiPicker, { type EmojiClickData } from 'emoji-picker-react'
import { InlineMarkdownEditor } from './InlineMarkdownEditor'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMentions from '../../lib/remarkMentions'
import rehypeEmoji, { isEmojiOnly } from '../../lib/rehypeEmoji'
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter'
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism'
import { api } from '../../services/apiClient'
import { getConnection } from '../../services/signalrClient'
import { useRoomStore } from '../../stores/roomStore'
import { usePresenceStore } from '../../stores/presenceStore'
import { useCurrentUserStore } from '../../stores/currentUserStore'
import { useUserProfileStore } from '../../stores/userProfileStore'
import { useBookmarkStore } from '../../stores/bookmarkStore'
import { useHighlightStore } from '../../stores/highlightStore'
import { useLinkPreviewStore } from '../../stores/linkPreviewStore'
import { ReactionBar } from './ReactionBar'
import { AttachmentGrid } from './AttachmentGrid'
import { UserAvatar } from '../common/UserAvatar'
import { AvatarLightbox } from '../common/AvatarLightbox'
import { QuoteBlock } from './QuoteBlock'
import { PollMessage } from './PollMessage'
import { LinkPreviewCard } from './LinkPreviewCard'
import { cn } from '../../lib/utils'
import { Quote, Star, Bookmark, BookmarkCheck } from 'lucide-react'
import { addBookmark, removeBookmark, addHighlight, removeHighlight } from '../../services/enrichedMessagingApiService'
import type { Message, Room } from '../../types'
import type { OutboxMessage } from '../../types'

interface MentionNode {
  value: string
}

const STATUS_COLORS: Record<string, string> = {
  green: '#22c55e', yellow: '#eab308', red: '#ef4444', grey: '#6b7280',
  blue: '#3b82f6', orange: '#f97316', purple: '#a855f7', pink: '#ec4899',
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
  onSetPendingQuote?: (quoteData: { messageId: string; authorDisplayName: string; contentSnapshot: string }) => void
  onAddHighlight?: (messageId: string) => void
  onJumpTo?: (messageId: string) => void
}

function isOutbox(m: Message | OutboxMessage): m is OutboxMessage {
  return 'clientId' in m
}

function formatTime(isoOrTs: string | number): string {
  const date = typeof isoOrTs === 'number' ? new Date(isoOrTs) : new Date(isoOrTs)
  const isToday = date.toDateString() === new Date().toDateString()
  if (isToday) return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  const sameYear = date.getFullYear() === new Date().getFullYear()
  return date.toLocaleString([], {
    month: 'short', day: 'numeric',
    ...(sameYear ? {} : { year: 'numeric' }),
    hour: '2-digit', minute: '2-digit',
  })
}

async function openDmWithUser(userId: string) {
  const { loadRooms, setActiveRoom } = useRoomStore.getState()
  const room = await api.post<Room>('/api/rooms/dm', { targetUserId: userId })
  await loadRooms()
  setActiveRoom(room.id)
}

export const MessageItem = memo(function MessageItem({ message, isGrouped = false, isPending = false, isKeyboardSelected = false, deleteConfirmPending = false, shouldStartEditing = false, onSetPendingQuote, onAddHighlight, onJumpTo }: MessageItemProps) {
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
  const [bookmarkLoading, setBookmarkLoading] = useState(false)
  const [justHighlighted, setJustHighlighted] = useState(false)
  const emojiButtonRef = useRef<HTMLButtonElement>(null)

  const bookmarkFolders = useBookmarkStore(s => s.folders)
  const bookmarkUnfiled = useBookmarkStore(s => s.unfiled)
  const addBookmarkToStore = useBookmarkStore(s => s.addBookmark)
  const removeBookmarkFromStore = useBookmarkStore(s => s.removeBookmark)

  const messageId = isOutbox(message) ? null : (message as Message).id
  const livePreview = useLinkPreviewStore(s => messageId ? s.previews[messageId] : undefined)
  const existingBookmark = messageId
    ? (bookmarkUnfiled.find(b => b.messageId === messageId) ??
       bookmarkFolders.flatMap(f => f.bookmarks).find(b => b.messageId === messageId))
    : undefined
  const isBookmarked = !!existingBookmark

  const addHighlightToStore = useHighlightStore(s => s.addHighlight)
  const removeHighlightFromStore = useHighlightStore(s => s.removeHighlight)
  const existingHighlight = useHighlightStore(s =>
    !isOutbox(message)
      ? (s.highlights[(message as Message).roomId] ?? []).find(h => h.messageId === (message as Message).id) ?? null
      : null
  )
  const isHighlighted = existingHighlight !== null

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

  async function handleToggleBookmark() {
    if (bookmarkLoading || isOutbox(message)) return
    setBookmarkLoading(true)
    try {
      if (isBookmarked && existingBookmark) {
        await removeBookmark(existingBookmark.id)
        removeBookmarkFromStore(existingBookmark.id)
      } else {
        const bm = await addBookmark((message as Message).id, null)
        addBookmarkToStore(bm)
      }
    } catch { /* ignore */ } finally {
      setBookmarkLoading(false)
    }
  }

  async function handleToggleHighlight() {
    if (isOutbox(message) || !message.roomId) return
    if (onAddHighlight) {
      onAddHighlight((message as Message).id)
      return
    }
    if (isHighlighted && existingHighlight) {
      try {
        await removeHighlight(message.roomId, existingHighlight.id)
        removeHighlightFromStore(message.roomId, existingHighlight.id)
        setJustHighlighted(false)
      } catch { /* ignore */ }
    } else {
      try {
        const result = await addHighlight(message.roomId, (message as Message).id)
        addHighlightToStore(message.roomId, result)
        setJustHighlighted(true)
        setTimeout(() => setJustHighlighted(false), 2000)
      } catch { /* ignore */ }
    }
  }

  function handleSetPendingQuote() {
    if (isOutbox(message) || !onSetPendingQuote) return
    onSetPendingQuote({
      messageId: (message as Message).id,
      authorDisplayName: (message as Message).author.displayName,
      contentSnapshot: message.content,
    })
  }

  const showPill = !editing && (hovered || confirmDelete || pickerOpen)


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
      {!isOutbox(message) && onSetPendingQuote && (
        <button
          onClick={handleSetPendingQuote}
          className="rounded-full px-1.5 py-1 flex items-center justify-center text-muted-foreground hover:bg-muted/60 hover:text-foreground"
          title="Quote message"
          aria-label="Quote message"
        >
          <Quote size={14} />
        </button>
      )}
      {!isOutbox(message) && (
        <button
          onClick={handleToggleHighlight}
          className="rounded-full px-1.5 py-1 flex items-center justify-center text-muted-foreground hover:bg-muted/60 hover:text-foreground group/star"
          title={isHighlighted ? 'Remove highlight' : 'Highlight message'}
          aria-label={isHighlighted ? 'Remove highlight' : 'Highlight message'}
        >
          <Star
            size={14}
            className={
              isHighlighted
                ? 'fill-yellow-400 text-yellow-400 group-hover/star:fill-yellow-500 group-hover/star:text-yellow-500'
                : justHighlighted
                ? 'fill-yellow-400 text-yellow-400'
                : ''
            }
          />
        </button>
      )}
      {!isOutbox(message) && (
        <button
          onClick={handleToggleBookmark}
          disabled={bookmarkLoading}
          className={
            isBookmarked
              ? 'rounded-full px-1.5 py-1 flex items-center justify-center text-green-500 hover:bg-muted/60 disabled:opacity-50'
              : 'rounded-full px-1.5 py-1 flex items-center justify-center text-muted-foreground hover:bg-muted/60 hover:text-foreground disabled:opacity-50'
          }
          title={isBookmarked ? 'Remove bookmark' : 'Bookmark message'}
          aria-label={isBookmarked ? 'Remove bookmark' : 'Bookmark message'}
        >
          {isBookmarked ? <BookmarkCheck size={14} /> : <Bookmark size={14} />}
        </button>
      )}
      {isOwn && (
        <>
          <div className="w-px h-4 bg-border mx-0.5" />
          {editDeleteButtons}
        </>
      )}
    </>
  )

  const inlinePill = undefined

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
                className="text-sm font-semibold hover:underline"
                onClick={() => openDmWithUser(message.author.id)}
                title={`DM ${message.author.displayName}`}
              >
                {authorProfile?.displayName ?? message.author.displayName}
              </button>
              {(() => {
                const status = authorProfile?.status
                const dotColor = showAuthorOnline && status?.color
                  ? (STATUS_COLORS[status.color] ?? status.color)
                  : showAuthorOnline ? '#22c55e' : '#ef4444'
                const showEmoji = status?.emoji
                const statusText = status?.text ? (status.text.length > 100 ? status.text.slice(0, 100) + '…' : status.text) : null
                return (
                  <span className="flex items-center gap-1">
                    <span className="inline-block w-2 h-2 rounded-full flex-shrink-0" style={{ backgroundColor: dotColor }} />
                    {showEmoji && <span className="text-xs">{status!.emoji}</span>}
                    {statusText && <span className="text-xs text-muted-foreground">{statusText}</span>}
                  </span>
                )
              })()}
              <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
              {message.editedAt && (
                <span className="text-xs text-muted-foreground">(edited)</span>
              )}
            </div>
          </div>
        )}

        {!isOutbox(message) && (message as Message).quote && (
          <QuoteBlock
            quote={(message as Message).quote!}
            onJumpTo={onJumpTo}
          />
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
        ) : !isOutbox(message) && (message as Message).messageType === 'poll' && (message as Message).poll ? (
          <PollMessage poll={(message as Message).poll!} />
        ) : (
          <div className={cn('flex items-start gap-1.5', isHighlighted && (isGrouped ? '' : 'mt-0.5'))}>
            {isHighlighted && (
              <>
                <Star size={11} className="fill-yellow-400 text-yellow-400 mt-[3px] flex-shrink-0" aria-label="Highlighted" />
                <span className="text-muted-foreground/60 mt-[1px] select-none flex-shrink-0">|</span>
              </>
            )}
            <div className={cn('prose prose-sm dark:prose-invert max-w-none break-words min-w-0', isHighlighted ? 'grouped-prose' : isGrouped ? 'grouped-prose' : 'mt-0.5', isEmojiOnly(message.content) && 'emoji-only')}>
              <ReactMarkdown
                remarkPlugins={[remarkGfm, remarkMentions]}
                rehypePlugins={[rehypeEmoji]}
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
          </div>
        )}

        {!isOutbox(message) && (() => {
          const preview = livePreview ?? (message as Message).linkPreview
          return preview && !preview.isDismissed ? (
            <LinkPreviewCard
              messageId={(message as Message).id}
              preview={preview}
              isCurrentUserSender={isOwn}
            />
          ) : null
        })()}

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

      {/* Hover action pill */}
      <div
        className={cn(
          'absolute left-4 bottom-0 translate-y-1/2 flex items-center gap-0.5 border rounded-full shadow-sm px-1.5 py-0.5 z-20 bg-zinc-200 dark:bg-zinc-600',
          'transition-opacity duration-150',
          showPill ? 'opacity-100' : 'opacity-0 pointer-events-none',
        )}
      >
        {pillButtons}
      </div>

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
