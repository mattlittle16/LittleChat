import { useState } from 'react'
import { ChevronDown, ChevronRight, Trash2 } from 'lucide-react'
import { useDroppable } from '@dnd-kit/core'
import type { BookmarkFolder } from '../../types'
import { useBookmarkStore } from '../../stores/bookmarkStore'
import { deleteBookmarkFolder, removeBookmark as apiRemoveBookmark } from '../../services/enrichedMessagingApiService'
import { DraggableBookmarkItem } from './BookmarksView'

interface Props {
  folder: BookmarkFolder
  onNavigate: (roomId: string, messageId: string) => void
  isActiveDrag: boolean
}

export function BookmarkFolderSection({ folder, onNavigate, isActiveDrag }: Props) {
  const [open, setOpen] = useState(true)
  const { removeFolder, removeBookmark } = useBookmarkStore()

  const { setNodeRef, isOver } = useDroppable({ id: `folder:${folder.id}` })

  const handleDeleteFolder = async () => {
    try {
      await deleteBookmarkFolder(folder.id)
      removeFolder(folder.id)
    } catch { /* ignore */ }
  }

  const handleRemoveBookmark = async (bookmarkId: string) => {
    try {
      await apiRemoveBookmark(bookmarkId)
      removeBookmark(bookmarkId)
    } catch { /* ignore */ }
  }

  return (
    <div className="mb-1">
      {/* Folder heading — also the drop target */}
      <div
        ref={setNodeRef}
        className="flex items-center gap-1 px-2 py-1 rounded cursor-pointer transition-colors"
        style={{
          background: isOver && isActiveDrag ? 'hsl(var(--accent) / 0.5)' : 'transparent',
          outline: isOver && isActiveDrag ? '1px dashed hsl(var(--border))' : 'none',
        }}
        onClick={() => setOpen(o => !o)}
      >
        {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        <span className="text-sm font-medium flex-1 truncate">{folder.name}</span>
        {!open && (folder.bookmarks?.length ?? 0) > 0 && (
          <span className="text-xs text-muted-foreground opacity-60 mr-1">{folder.bookmarks!.length}</span>
        )}
        <button
          onClick={e => { e.stopPropagation(); handleDeleteFolder() }}
          className="text-muted-foreground opacity-0 hover:opacity-100 hover:text-destructive focus:outline-none"
          style={{ opacity: undefined }} // Let group-hover handle it via CSS
          aria-label="Delete folder"
        >
          <Trash2 size={13} />
        </button>
      </div>

      {open && (
        <div className="ml-3 space-y-0.5">
          {(folder.bookmarks?.length ?? 0) === 0 && !isOver && (
            <p className="text-xs text-muted-foreground px-2 py-1">No bookmarks</p>
          )}
          {folder.bookmarks?.map(b => (
            <DraggableBookmarkItem
              key={b.id}
              bookmark={b}
              onNavigate={onNavigate}
              onRemove={() => handleRemoveBookmark(b.id)}
            />
          ))}
        </div>
      )}
    </div>
  )
}
