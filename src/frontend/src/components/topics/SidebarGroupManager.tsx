import { useState } from 'react'
import { X, Trash2 } from 'lucide-react'
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  useDroppable,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core'
import { SortableContext, useSortable, verticalListSortingStrategy, arrayMove } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useSidebarGroupStore } from '../../stores/sidebarGroupStore'
import { useRoomStore } from '../../stores/roomStore'
import type { Room } from '../../types'

interface Props {
  onClose: () => void
}

const UNGROUPED_ID = '__ungrouped__'

export function SidebarGroupManager({ onClose }: Props) {
  const groups = useSidebarGroupStore(s => s.groups)
  const createGroup = useSidebarGroupStore(s => s.createGroup)
  const renameGroup = useSidebarGroupStore(s => s.renameGroup)
  const deleteGroup = useSidebarGroupStore(s => s.deleteGroup)
  const assignRoom = useSidebarGroupStore(s => s.assignRoom)
  const unassignRoom = useSidebarGroupStore(s => s.unassignRoom)
  const reorderRooms = useSidebarGroupStore(s => s.reorderRooms)
  const allRooms = useRoomStore(s => s.rooms)
  const rooms = allRooms.filter(r => !r.isDm)

  const [newGroupName, setNewGroupName] = useState('')
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')
  const [activeDragId, setActiveDragId] = useState<string | null>(null)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } })
  )

  async function handleCreate() {
    const name = newGroupName.trim()
    if (!name) return
    setCreating(true)
    setError(null)
    try {
      await createGroup(name)
      setNewGroupName('')
    } catch {
      setError('Failed to create group.')
    } finally {
      setCreating(false)
    }
  }

  async function handleRename(groupId: string) {
    const name = editingName.trim()
    if (!name) { setEditingId(null); return }
    try {
      await renameGroup(groupId, name)
    } catch {
      setError('Failed to rename group.')
    } finally {
      setEditingId(null)
    }
  }

  async function handleDelete(groupId: string) {
    try {
      await deleteGroup(groupId)
    } catch {
      setError('Failed to delete group.')
    }
  }

  const assignedRoomIds = new Set(groups.flatMap(g => g.roomIds))
  const ungroupedRooms = rooms.filter(r => !assignedRoomIds.has(r.id))
  const ungroupedIds = ungroupedRooms.map(r => r.id)

  function findBucketForRoom(roomId: string): { type: 'group'; groupId: string } | { type: 'ungrouped' } | null {
    for (const g of groups) {
      if (g.roomIds.includes(roomId)) return { type: 'group', groupId: g.id }
    }
    if (ungroupedIds.includes(roomId)) return { type: 'ungrouped' }
    return null
  }

  function handleDragStart(event: DragStartEvent) {
    setActiveDragId(String(event.active.id))
  }

  async function handleDragEnd(event: DragEndEvent) {
    setActiveDragId(null)
    const { active, over } = event
    if (!over) return

    const activeRoomId = String(active.id)
    const overId = String(over.id)

    const sourceBucket = findBucketForRoom(activeRoomId)
    if (!sourceBucket) return

    // Dropped on a section container
    if (overId === UNGROUPED_ID) {
      if (sourceBucket.type !== 'ungrouped') {
        await unassignRoom(activeRoomId)
      }
      return
    }

    const targetGroup = groups.find(g => g.id === overId)
    if (targetGroup) {
      if (sourceBucket.type === 'group' && sourceBucket.groupId === targetGroup.id) return
      await assignRoom(targetGroup.id, activeRoomId)
      return
    }

    // Dropped on a room item — figure out target bucket
    const targetBucket = findBucketForRoom(overId)
    if (!targetBucket) return

    const movingBetweenBuckets =
      sourceBucket.type !== targetBucket.type ||
      (sourceBucket.type === 'group' && targetBucket.type === 'group' && sourceBucket.groupId !== targetBucket.groupId)

    if (movingBetweenBuckets) {
      if (targetBucket.type === 'group') {
        await assignRoom(targetBucket.groupId, activeRoomId)
      } else {
        await unassignRoom(activeRoomId)
      }
      return
    }

    if (activeRoomId === overId) return

    // Same-bucket reorder
    if (sourceBucket.type === 'group') {
      const group = groups.find(g => g.id === sourceBucket.groupId)
      if (!group) return
      const oldIndex = group.roomIds.indexOf(activeRoomId)
      const newIndex = group.roomIds.indexOf(overId)
      if (oldIndex === -1 || newIndex === -1) return
      await reorderRooms(sourceBucket.groupId, arrayMove([...group.roomIds], oldIndex, newIndex))
    } else {
      const oldIndex = ungroupedIds.indexOf(activeRoomId)
      const newIndex = ungroupedIds.indexOf(overId)
      if (oldIndex === -1 || newIndex === -1) return
      await reorderRooms(null, arrayMove([...ungroupedIds], oldIndex, newIndex))
    }
  }

  const activeDragRoom = activeDragId ? rooms.find(r => r.id === activeDragId) : null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div
        className="w-full max-w-lg rounded-lg border bg-background shadow-xl p-6 relative flex flex-col"
        style={{ borderColor: 'hsl(var(--border))', maxHeight: '85vh' }}
      >
        <button
          onClick={onClose}
          className="absolute right-4 top-4 text-muted-foreground hover:text-foreground"
          aria-label="Close"
        >
          <X className="w-4 h-4" />
        </button>

        <h2 className="text-base font-semibold mb-1">Manage Groups</h2>
        <p className="text-xs text-muted-foreground mb-4">
          Create groups to organize your topics. Drag topics between groups to assign them.
        </p>

        {error && (
          <p className="text-xs mb-3" style={{ color: 'hsl(var(--destructive))' }}>{error}</p>
        )}

        {/* Create new group */}
        <div className="flex gap-2 mb-4">
          <input
            value={newGroupName}
            onChange={e => setNewGroupName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleCreate()}
            placeholder="New group name…"
            maxLength={50}
            className="flex-1 rounded border px-3 py-1.5 text-sm focus:outline-none focus:ring-1"
            style={{ borderColor: 'hsl(var(--border))' }}
          />
          <button
            onClick={handleCreate}
            disabled={creating || !newGroupName.trim()}
            className="rounded px-3 py-1.5 text-sm font-medium disabled:opacity-50"
            style={{ background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }}
          >
            Add
          </button>
        </div>

        <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
          <div className="flex-1 overflow-y-auto flex flex-col gap-3 min-h-0">
            {/* Groups */}
            {groups.map(group => {
              const groupRooms = group.roomIds
                .map(id => rooms.find(r => r.id === id))
                .filter((r): r is Room => r !== undefined)
              return (
                <GroupSection
                  key={group.id}
                  id={group.id}
                  name={group.name}
                  rooms={groupRooms}
                  roomIds={group.roomIds}
                  editingId={editingId}
                  editingName={editingName}
                  onEditStart={(id, name) => { setEditingId(id); setEditingName(name) }}
                  onEditChange={setEditingName}
                  onRename={handleRename}
                  onEditCancel={() => setEditingId(null)}
                  onDelete={handleDelete}
                />
              )
            })}

            {groups.length === 0 && (
              <p className="text-sm text-muted-foreground text-center py-2">
                No groups yet. Create one above.
              </p>
            )}

            {/* Ungrouped */}
            {rooms.length > 0 && (
              <UngroupedSection rooms={ungroupedRooms} roomIds={ungroupedIds} />
            )}
          </div>

          <DragOverlay>
            {activeDragRoom ? (
              <div
                className="flex items-center gap-2 px-3 py-1.5 text-sm rounded border shadow-lg opacity-90"
                style={{
                  background: 'hsl(var(--background))',
                  borderColor: 'hsl(var(--border))',
                }}
              >
                <span style={{ color: 'hsl(var(--muted-foreground))' }}>
                  {activeDragRoom.isPrivate ? '🔒' : '#'}
                </span>
                <span className="truncate">{activeDragRoom.name}</span>
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>

        <div className="flex justify-end mt-4">
          <button
            type="button"
            onClick={onClose}
            className="rounded px-3 py-1.5 text-sm font-medium"
            style={{ background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }}
          >
            Done
          </button>
        </div>
      </div>
    </div>
  )
}

function GroupSection({
  id,
  name,
  rooms,
  roomIds,
  editingId,
  editingName,
  onEditStart,
  onEditChange,
  onRename,
  onEditCancel,
  onDelete,
}: {
  id: string
  name: string
  rooms: Room[]
  roomIds: string[]
  editingId: string | null
  editingName: string
  onEditStart: (id: string, name: string) => void
  onEditChange: (name: string) => void
  onRename: (id: string) => void
  onEditCancel: () => void
  onDelete: (id: string) => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id })

  return (
    <div
      ref={setNodeRef}
      className="rounded border p-3 flex flex-col gap-2 transition-colors"
      style={{
        borderColor: isOver ? 'hsl(var(--primary))' : 'hsl(var(--border))',
        background: isOver ? 'hsl(var(--primary) / 0.05)' : undefined,
      }}
    >
      <div className="flex items-center gap-2">
        {editingId === id ? (
          <input
            autoFocus
            value={editingName}
            onChange={e => onEditChange(e.target.value)}
            onBlur={() => onRename(id)}
            onKeyDown={e => { if (e.key === 'Enter') onRename(id); if (e.key === 'Escape') onEditCancel() }}
            maxLength={50}
            className="flex-1 rounded border px-2 py-0.5 text-sm focus:outline-none focus:ring-1"
            style={{ borderColor: 'hsl(var(--border))' }}
          />
        ) : (
          <span
            className="flex-1 text-sm font-medium cursor-pointer hover:underline"
            onClick={() => onEditStart(id, name)}
            title="Click to rename"
          >
            {name}
          </span>
        )}
        <button
          onClick={() => onDelete(id)}
          className="text-muted-foreground hover:text-destructive transition-colors"
          title="Delete group"
        >
          <Trash2 className="w-3.5 h-3.5" />
        </button>
      </div>

      <SortableContext items={roomIds} strategy={verticalListSortingStrategy}>
        {rooms.map(room => (
          <SortableManagerRoomItem key={room.id} room={room} />
        ))}
      </SortableContext>

      {rooms.length === 0 && (
        <p className="text-xs text-muted-foreground text-center py-1 border border-dashed rounded"
          style={{ borderColor: 'hsl(var(--border))' }}>
          Drop topics here
        </p>
      )}
    </div>
  )
}

function UngroupedSection({ rooms, roomIds }: { rooms: Room[]; roomIds: string[] }) {
  const { setNodeRef, isOver } = useDroppable({ id: UNGROUPED_ID })

  return (
    <div
      ref={setNodeRef}
      className="rounded border p-3 flex flex-col gap-2 transition-colors"
      style={{
        borderColor: isOver ? 'hsl(var(--primary))' : 'hsl(var(--border))',
        background: isOver ? 'hsl(var(--primary) / 0.05)' : undefined,
      }}
    >
      <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Ungrouped
      </span>
      <SortableContext items={roomIds} strategy={verticalListSortingStrategy}>
        {rooms.map(room => (
          <SortableManagerRoomItem key={room.id} room={room} />
        ))}
      </SortableContext>
      {rooms.length === 0 && (
        <p className="text-xs text-muted-foreground text-center py-1">
          All topics are assigned to groups.
        </p>
      )}
    </div>
  )
}

function SortableManagerRoomItem({ room }: { room: Room }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: room.id })

  return (
    <div
      ref={setNodeRef}
      style={{
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.3 : 1,
      }}
      className="flex items-center gap-2 py-1 px-2 rounded cursor-grab active:cursor-grabbing
        hover:bg-muted/40 transition-colors"
      {...attributes}
      {...listeners}
    >
      <span className="text-muted-foreground text-xs">
        {room.isPrivate ? '🔒' : '#'}
      </span>
      <span className="flex-1 text-sm truncate">{room.name}</span>
    </div>
  )
}
