import { useState } from 'react'
import { X, Trash2, GripVertical } from 'lucide-react'
import { useSidebarGroupStore } from '../../stores/sidebarGroupStore'
import { useRoomStore } from '../../stores/roomStore'

interface Props {
  onClose: () => void
}

export function SidebarGroupManager({ onClose }: Props) {
  const groups = useSidebarGroupStore(s => s.groups)
  const createGroup = useSidebarGroupStore(s => s.createGroup)
  const renameGroup = useSidebarGroupStore(s => s.renameGroup)
  const deleteGroup = useSidebarGroupStore(s => s.deleteGroup)
  const assignRoom = useSidebarGroupStore(s => s.assignRoom)
  const unassignRoom = useSidebarGroupStore(s => s.unassignRoom)
  const allRooms = useRoomStore(s => s.rooms)
  const rooms = allRooms.filter(r => !r.isDm)

  const [newGroupName, setNewGroupName] = useState('')
  const [creating, setCreating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')

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

  function getGroupForRoom(roomId: string): string | null {
    return groups.find(g => g.roomIds.includes(roomId))?.id ?? null
  }

  async function handleRoomGroupChange(roomId: string, groupId: string | '') {
    try {
      if (groupId === '') {
        await unassignRoom(roomId)
      } else {
        await assignRoom(groupId, roomId)
      }
    } catch {
      setError('Failed to update room grouping.')
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div
        className="w-full max-w-md rounded-lg border bg-background shadow-xl p-6 relative flex flex-col"
        style={{ borderColor: 'hsl(var(--border))', maxHeight: '80vh' }}
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
          Create groups to organize your topics in the sidebar.
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

        <div className="flex-1 overflow-y-auto flex flex-col gap-3 min-h-0">
          {/* Existing groups */}
          {groups.map(group => (
            <div
              key={group.id}
              className="rounded border p-3 flex flex-col gap-2"
              style={{ borderColor: 'hsl(var(--border))' }}
            >
              <div className="flex items-center gap-2">
                <GripVertical className="w-4 h-4 text-muted-foreground shrink-0" />
                {editingId === group.id ? (
                  <input
                    autoFocus
                    value={editingName}
                    onChange={e => setEditingName(e.target.value)}
                    onBlur={() => handleRename(group.id)}
                    onKeyDown={e => { if (e.key === 'Enter') handleRename(group.id); if (e.key === 'Escape') setEditingId(null) }}
                    maxLength={50}
                    className="flex-1 rounded border px-2 py-0.5 text-sm focus:outline-none focus:ring-1"
                    style={{ borderColor: 'hsl(var(--border))' }}
                  />
                ) : (
                  <span
                    className="flex-1 text-sm font-medium cursor-pointer hover:underline"
                    onClick={() => { setEditingId(group.id); setEditingName(group.name) }}
                    title="Click to rename"
                  >
                    {group.name}
                  </span>
                )}
                <button
                  onClick={() => handleDelete(group.id)}
                  className="text-muted-foreground hover:text-destructive transition-colors"
                  title="Delete group"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          ))}

          {groups.length === 0 && (
            <p className="text-sm text-muted-foreground text-center py-2">
              No groups yet. Create one above.
            </p>
          )}

          {/* Assign rooms to groups */}
          {rooms.length > 0 && groups.length > 0 && (
            <div className="mt-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-2">
                Topic Assignments
              </p>
              {rooms.map(room => (
                <div key={room.id} className="flex items-center gap-2 py-1">
                  <span className="flex-1 text-sm truncate">
                    {room.isPrivate ? '🔒' : '#'} {room.name}
                  </span>
                  <select
                    value={getGroupForRoom(room.id) ?? ''}
                    onChange={e => handleRoomGroupChange(room.id, e.target.value)}
                    className="rounded border px-2 py-0.5 text-xs bg-background"
                    style={{ borderColor: 'hsl(var(--border))' }}
                  >
                    <option value="">No group</option>
                    {groups.map(g => (
                      <option key={g.id} value={g.id}>{g.name}</option>
                    ))}
                  </select>
                </div>
              ))}
            </div>
          )}
        </div>

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
