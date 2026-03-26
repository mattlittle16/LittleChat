import { useEffect, useMemo, useRef, useState } from 'react'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { Bookmark, Plus, X, GripVertical } from 'lucide-react'
import { useBookmarkStore } from '../../stores/bookmarkStore'
import {
  getBookmarks,
  createBookmarkFolder,
  removeBookmark as apiRemoveBookmark,
  moveBookmark as apiMoveBookmark,
} from '../../services/enrichedMessagingApiService'
import { BookmarkFolderSection } from './BookmarkFolderSection'
import type { Bookmark as BookmarkType } from '../../types'

interface Props {
  onNavigate: (roomId: string, messageId: string) => void
}

export function BookmarksView({ onNavigate }: Props) {
  const { folders, unfiled, setBookmarks, addFolder, removeBookmark, moveBookmark } = useBookmarkStore()

  const [activeDragId, setActiveDragId] = useState<string | null>(null)
  const [showFolderInput, setShowFolderInput] = useState(false)
  const [newFolderName, setNewFolderName] = useState('')
  const [creatingFolder, setCreatingFolder] = useState(false)
  const folderInputRef = useRef<HTMLInputElement>(null)

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }))

  useEffect(() => {
    getBookmarks().then(data => setBookmarks(data.folders, data.unfiled)).catch(() => {})
  }, [setBookmarks])

  useEffect(() => {
    if (showFolderInput) folderInputRef.current?.focus()
  }, [showFolderInput])

  const allBookmarks = useMemo(
    () => [...unfiled, ...folders.flatMap(f => f.bookmarks ?? [])],
    [unfiled, folders],
  )
  const activeDragBookmark = activeDragId ? allBookmarks.find(b => b.id === activeDragId) ?? null : null

  async function handleCreateFolder() {
    const name = newFolderName.trim()
    if (!name) return
    setCreatingFolder(true)
    try {
      const folder = await createBookmarkFolder(name)
      addFolder({ ...folder, bookmarks: [] })
      setNewFolderName('')
      setShowFolderInput(false)
    } catch { /* ignore */ } finally {
      setCreatingFolder(false)
    }
  }

  function handleDragStart(event: DragStartEvent) {
    setActiveDragId(String(event.active.id))
  }

  async function handleDragEnd(event: DragEndEvent) {
    setActiveDragId(null)
    const { active, over } = event
    if (!over) return

    const bookmarkId = String(active.id)
    const overId = String(over.id)

    let targetFolderId: string | null = null
    if (overId === 'unfiled') {
      targetFolderId = null
    } else if (overId.startsWith('folder:')) {
      targetFolderId = overId.slice(7)
    } else {
      return
    }

    // Check if already in target
    const currentFolderId = unfiled.find(b => b.id === bookmarkId)
      ? null
      : folders.flatMap(f => f.bookmarks ?? []).find(b => b.id === bookmarkId)?.folderId ?? null

    if (targetFolderId === currentFolderId) return

    try {
      await apiMoveBookmark(bookmarkId, targetFolderId)
      moveBookmark(bookmarkId, targetFolderId)
    } catch { /* ignore */ }
  }

  const handleRemoveUnfiled = async (bookmarkId: string) => {
    try {
      await apiRemoveBookmark(bookmarkId)
      removeBookmark(bookmarkId)
    } catch { /* ignore */ }
  }

  const isEmpty = folders.length === 0 && unfiled.length === 0

  return (
    <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
      <div className="flex flex-col h-full">
        {/* Header */}
        <div className="flex items-center justify-between px-3 py-2 border-b border-border flex-shrink-0">
          <h2 className="font-semibold text-sm flex items-center gap-2">
            <Bookmark size={16} /> Bookmarks
          </h2>
          <button
            onClick={() => setShowFolderInput(v => !v)}
            className="text-muted-foreground hover:text-foreground"
            title="New folder"
          >
            {showFolderInput ? <X size={16} /> : <Plus size={16} />}
          </button>
        </div>

        {/* Inline folder creation */}
        {showFolderInput && (
          <div className="flex gap-2 px-3 py-2 border-b border-border flex-shrink-0">
            <input
              ref={folderInputRef}
              value={newFolderName}
              onChange={e => setNewFolderName(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter') handleCreateFolder()
                if (e.key === 'Escape') { setShowFolderInput(false); setNewFolderName('') }
              }}
              placeholder="Folder name…"
              maxLength={50}
              disabled={creatingFolder}
              className="flex-1 rounded border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring disabled:opacity-50"
            />
            <button
              onClick={handleCreateFolder}
              disabled={creatingFolder || !newFolderName.trim()}
              className="rounded bg-primary px-3 py-1.5 text-xs font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
            >
              Add
            </button>
          </div>
        )}

        <div className="flex-1 overflow-y-auto p-2">
          {/* Folders */}
          {folders.map(f => (
            <BookmarkFolderSection
              key={f.id}
              folder={f}
              onNavigate={onNavigate}
              isActiveDrag={activeDragId !== null}
            />
          ))}

          {/* Unfiled */}
          {unfiled.length > 0 && (
            <UnfiledSection
              bookmarks={unfiled}
              isActiveDrag={activeDragId !== null}
              onNavigate={onNavigate}
              onRemove={handleRemoveUnfiled}
            />
          )}

          {isEmpty && (
            <div className="flex flex-col items-center justify-center h-full text-muted-foreground p-8 text-center">
              <Bookmark size={32} className="mb-3 opacity-30" />
              <p className="text-sm">No bookmarks yet. Bookmark messages to save them here.</p>
            </div>
          )}
        </div>
      </div>

      <DragOverlay>
        {activeDragBookmark && (
          <div className="rounded border border-border bg-card shadow-lg px-3 py-2 text-sm opacity-90 max-w-56 truncate flex items-center gap-2">
            <GripVertical size={12} className="text-muted-foreground flex-shrink-0" />
            <span className="truncate">{activeDragBookmark.contentPreview || activeDragBookmark.roomName}</span>
          </div>
        )}
      </DragOverlay>
    </DndContext>
  )
}

function UnfiledSection({
  bookmarks,
  isActiveDrag,
  onNavigate,
  onRemove,
}: {
  bookmarks: BookmarkType[]
  isActiveDrag: boolean
  onNavigate: (roomId: string, messageId: string) => void
  onRemove: (id: string) => void
}) {
  const { removeBookmark } = useBookmarkStore()
  // We use the store's removeBookmark here (called from parent via onRemove which also calls API)
  void removeBookmark

  return (
    <DroppableUnfiledSection isActiveDrag={isActiveDrag}>
      {bookmarks.map(b => (
        <DraggableBookmarkItem
          key={b.id}
          bookmark={b}
          onNavigate={onNavigate}
          onRemove={() => onRemove(b.id)}
        />
      ))}
    </DroppableUnfiledSection>
  )
}

import { useDroppable } from '@dnd-kit/core'

function DroppableUnfiledSection({ isActiveDrag, children }: { isActiveDrag: boolean; children: React.ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: 'unfiled' })

  return (
    <div className="mt-2">
      <div
        ref={setNodeRef}
        className="rounded px-2 py-0.5 mb-1 transition-colors"
        style={{
          background: isOver && isActiveDrag ? 'hsl(var(--accent) / 0.5)' : 'transparent',
          outline: isOver && isActiveDrag ? '1px dashed hsl(var(--border))' : 'none',
        }}
      >
        <p className="text-xs font-medium text-muted-foreground">Unfiled</p>
      </div>
      <div className="space-y-0.5">{children}</div>
    </div>
  )
}

import { useDraggable } from '@dnd-kit/core'
import { Trash2 } from 'lucide-react'

export function DraggableBookmarkItem({
  bookmark: b,
  onNavigate,
  onRemove,
}: {
  bookmark: BookmarkType
  onNavigate: (roomId: string, messageId: string) => void
  onRemove: () => void
}) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: b.id })
  const style = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` }
    : undefined

  const isDisabled = !!b.placeholderReason

  return (
    <div
      ref={setNodeRef}
      style={{ ...style, opacity: isDragging ? 0.3 : 1 }}
      className="flex items-start gap-1.5 px-2 py-1.5 rounded hover:bg-muted/30 group"
    >
      <button
        {...attributes}
        {...listeners}
        className="mt-0.5 flex-shrink-0 cursor-grab text-muted-foreground opacity-0 group-hover:opacity-60 hover:opacity-100 focus:outline-none"
        tabIndex={-1}
        aria-label="Drag to reorder"
      >
        <GripVertical size={13} />
      </button>
      <div className="flex-1 min-w-0">
        <p className="text-xs text-muted-foreground">{b.roomName}</p>
        {isDisabled ? (
          <p className="text-sm italic text-muted-foreground">
            {b.placeholderReason === 'message_deleted' ? 'Original message deleted' : 'Conversation deleted'}
          </p>
        ) : (
          <button
            className="text-sm text-left hover:underline text-foreground truncate w-full"
            onClick={() => onNavigate(b.roomId, b.messageId)}
          >
            {b.contentPreview}
          </button>
        )}
        <p className="text-xs text-muted-foreground">{b.authorDisplayName}</p>
      </div>
      <button
        onClick={onRemove}
        className="mt-0.5 flex-shrink-0 text-muted-foreground opacity-0 group-hover:opacity-100 hover:text-destructive"
        aria-label="Remove bookmark"
      >
        <Trash2 size={13} />
      </button>
    </div>
  )
}
