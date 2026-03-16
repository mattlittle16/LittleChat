import { useEffect, useMemo, useRef, useState } from 'react'
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
import { BellOff, Search, Settings, ChevronDown, ChevronRight, Lock, GripVertical } from 'lucide-react'
import { useRoomStore } from '../../stores/roomStore'
import { usePresenceStore } from '../../stores/presenceStore'
import { useNotificationPreferencesStore } from '../../stores/notificationPreferencesStore'
import { useCurrentUserStore } from '../../stores/currentUserStore'
import { useSidebarGroupStore } from '../../stores/sidebarGroupStore'
import { useUserProfileStore } from '../../stores/userProfileStore'
import { ComposeDialog } from './ComposeDialog'
import { CreateTopicDialog } from './CreateTopicDialog'
import { TransferOwnershipDialog } from '../topics/TransferOwnershipDialog'
import { TopicDiscoveryDialog } from '../topics/TopicDiscoveryDialog'
import { SidebarGroupManager } from '../topics/SidebarGroupManager'
import { UserProfileDialog } from '../profile/UserProfileDialog'
import { UserAvatar } from '../common/UserAvatar'
import { ThemeToggle } from '../ThemeToggle'
import logoSvg from '../../assets/logo.svg'
import type { ConversationOverrideLevel, Room } from '../../types'

const sidebarStyle = {
  background: 'hsl(var(--sidebar-bg))',
  color: 'hsl(var(--sidebar-fg))',
}

const GROUP_HEADING_PREFIX = 'group-heading:'

function groupHeadingId(groupId: string) {
  return `${GROUP_HEADING_PREFIX}${groupId}`
}

function isGroupHeadingId(id: string) {
  return id.startsWith(GROUP_HEADING_PREFIX)
}

function extractGroupId(headingId: string) {
  return headingId.slice(GROUP_HEADING_PREFIX.length)
}

interface SidebarProps {
  onNavigate?: () => void
}

export function Sidebar({ onNavigate }: SidebarProps = {}) {
  const { rooms, activeRoomId, loadRooms, setActiveRoom } = useRoomStore()
  const [creating, setCreating] = useState(false)
  const [composing, setComposing] = useState(false)
  const [browsing, setBrowsing] = useState(false)
  const [managingGroups, setManagingGroups] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const { groups, fetchGroups, setCollapsed, assignRoom, unassignRoom, reorderRooms } = useSidebarGroupStore()
  const [activeDragId, setActiveDragId] = useState<string | null>(null)
  const currentUserId = useCurrentUserStore(s => s.id)
  const currentProfile = useUserProfileStore(s => currentUserId ? s.profiles[currentUserId] : undefined)

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } })
  )

  useEffect(() => {
    loadRooms()
    fetchGroups()
  }, [loadRooms, fetchGroups])

  // Restore last-viewed room on mount
  useEffect(() => {
    const savedId = localStorage.getItem('littlechat_active_room')
    if (savedId && rooms.some(r => r.id === savedId)) {
      setActiveRoom(savedId)
    } else if (rooms.length > 0 && !activeRoomId) {
      setActiveRoom(rooms[0].id)
    }
  }, [rooms, activeRoomId, setActiveRoom])

  const topicRooms = rooms.filter(r => !r.isDm)
  const dmRooms = rooms.filter(r => r.isDm)

  const assignedRoomIds = useMemo(() => new Set(groups.flatMap(g => g.roomIds)), [groups])
  const ungroupedRooms = useMemo(() => topicRooms.filter(r => !assignedRoomIds.has(r.id)), [topicRooms, assignedRoomIds])
  const ungroupedIds = useMemo(() => ungroupedRooms.map(r => r.id), [ungroupedRooms])

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

    const activeRoomId2 = String(active.id)
    const overId = String(over.id)

    const sourceBucket = findBucketForRoom(activeRoomId2)
    if (!sourceBucket) return

    // Case 1: dropped on a group heading → assign to that group
    if (isGroupHeadingId(overId)) {
      const targetGroupId = extractGroupId(overId)
      if (sourceBucket.type === 'group' && sourceBucket.groupId === targetGroupId) return
      await assignRoom(targetGroupId, activeRoomId2)
      return
    }

    // Case 2: dropped on another room item
    const overRoomId = overId
    const targetBucket = findBucketForRoom(overRoomId)
    if (!targetBucket) return

    const movingBetweenBuckets =
      sourceBucket.type !== targetBucket.type ||
      (sourceBucket.type === 'group' && targetBucket.type === 'group' && sourceBucket.groupId !== targetBucket.groupId)

    if (movingBetweenBuckets) {
      // Cross-bucket move: assign to target group or unassign
      if (targetBucket.type === 'group') {
        await assignRoom(targetBucket.groupId, activeRoomId2)
      } else {
        await unassignRoom(activeRoomId2)
      }
      return
    }

    // Same-bucket reorder
    if (activeRoomId2 === overRoomId) return

    if (sourceBucket.type === 'group') {
      const group = groups.find(g => g.id === sourceBucket.groupId)
      if (!group) return
      const oldIndex = group.roomIds.indexOf(activeRoomId2)
      const newIndex = group.roomIds.indexOf(overRoomId)
      if (oldIndex === -1 || newIndex === -1) return
      const newOrder = arrayMove([...group.roomIds], oldIndex, newIndex)
      await reorderRooms(sourceBucket.groupId, newOrder)
    } else {
      const oldIndex = ungroupedIds.indexOf(activeRoomId2)
      const newIndex = ungroupedIds.indexOf(overRoomId)
      if (oldIndex === -1 || newIndex === -1) return
      const newOrder = arrayMove([...ungroupedIds], oldIndex, newIndex)
      await reorderRooms(null, newOrder)
    }
  }

  const activeDragRoom = activeDragId ? rooms.find(r => r.id === activeDragId) : null

  return (
    <>
      <aside className="flex h-full w-60 flex-col" style={sidebarStyle}>
        {/* Logo */}
        <div className="flex items-center gap-2.5 px-4 py-3" style={{ borderBottom: '1px solid hsl(var(--sidebar-muted-fg) / 0.2)' }}>
          <img src={logoSvg} alt="" className="h-6 w-6 flex-shrink-0" />
          <span className="font-semibold text-sm tracking-tight" style={{ color: 'hsl(var(--sidebar-fg))' }}>
            LittleChat
          </span>
        </div>

        {/* Topics section */}
        <div className="flex items-center justify-between px-4 py-2 mt-1">
          <span
            className="text-xs font-semibold uppercase tracking-wider"
            style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          >
            Topics
          </span>
          <div className="flex items-center gap-1">
            <button
              onClick={() => setBrowsing(true)}
              className="flex items-center justify-center w-5 h-5 rounded transition-colors"
              style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
              onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
              onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
              title="Browse Topics"
            >
              <Search className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={() => setManagingGroups(true)}
              className="flex items-center justify-center w-5 h-5 rounded transition-colors"
              style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
              onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
              onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
              title="Manage Groups"
            >
              <Settings className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={() => setCreating(c => !c)}
              className="text-lg leading-none transition-colors"
              style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
              onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
              onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
              title="Create Topic"
            >
              +
            </button>
          </div>
        </div>

        <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
          <nav className="flex-1 overflow-y-auto">
            {/* Grouped topics */}
            {groups.map(group => {
              // Preserve API order from group.roomIds
              const groupRooms = group.roomIds
                .map(id => topicRooms.find(r => r.id === id))
                .filter((r): r is Room => r !== undefined)
              return (
                <div key={group.id}>
                  <DroppableGroupHeading
                    group={group}
                    roomCount={groupRooms.length}
                    isActiveDrag={activeDragId !== null}
                    onToggleCollapse={() => setCollapsed(group.id, !group.isCollapsed)}
                  />
                  {!group.isCollapsed && (
                    <SortableContext items={group.roomIds} strategy={verticalListSortingStrategy}>
                      {groupRooms.map(room => (
                        <SortableRoomItem
                          key={room.id}
                          room={room}
                          isActive={room.id === activeRoomId}
                          onClick={() => { setActiveRoom(room.id); onNavigate?.() }}
                        />
                      ))}
                      {groupRooms.length === 0 && activeDragId && (
                        <p className="px-4 py-1 text-xs italic" style={{ color: 'hsl(var(--sidebar-muted-fg) / 0.6)' }}>
                          Drop topics here
                        </p>
                      )}
                    </SortableContext>
                  )}
                </div>
              )
            })}

            {/* Ungrouped topics */}
            <SortableContext items={ungroupedIds} strategy={verticalListSortingStrategy}>
              {ungroupedRooms.map(room => (
                <SortableRoomItem
                  key={room.id}
                  room={room}
                  isActive={room.id === activeRoomId}
                  onClick={() => { setActiveRoom(room.id); onNavigate?.() }}
                />
              ))}
            </SortableContext>

            {topicRooms.length === 0 && (
              <p className="px-4 py-1 text-xs" style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>
                No topics yet
              </p>
            )}

            {/* DMs section */}
            <div className="flex items-center justify-between px-4 py-2 mt-3">
              <span
                className="text-xs font-semibold uppercase tracking-wider"
                style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
              >
                Direct Messages
              </span>
              <button
                onClick={() => setComposing(true)}
                className="text-lg leading-none transition-colors"
                style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
                onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
                onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
                title="New DM"
              >
                +
              </button>
            </div>

            {dmRooms.map(room => (
              <DmItem
                key={room.id}
                room={room}
                isActive={room.id === activeRoomId}
                onClick={() => { setActiveRoom(room.id); onNavigate?.() }}
              />
            ))}
            {dmRooms.length === 0 && (
              <p className="px-4 py-1 text-xs" style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>
                No DMs yet
              </p>
            )}
          </nav>

          {/* DragOverlay: ghost of item being dragged */}
          <DragOverlay>
            {activeDragRoom ? (
              <div
                className="flex items-center gap-2 px-4 py-1.5 text-sm rounded shadow-lg opacity-80"
                style={{
                  background: 'hsl(var(--sidebar-active-bg))',
                  color: 'hsl(var(--sidebar-fg))',
                  width: '240px',
                }}
              >
                {activeDragRoom.isPrivate
                  ? <Lock className="w-3 h-3 shrink-0" style={{ color: 'hsl(var(--sidebar-muted-fg))' }} />
                  : <span style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>#</span>
                }
                <span className="flex-1 truncate">{activeDragRoom.name}</span>
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>

        {/* Bottom user/theme area */}
        <div
          className="flex items-center justify-between px-3 py-2"
          style={{ borderTop: '1px solid hsl(var(--sidebar-muted-fg) / 0.2)' }}
        >
          {currentUserId && (
            <button
              onClick={() => setProfileOpen(true)}
              className="flex items-center gap-2 rounded px-1 py-0.5 transition-colors hover:bg-sidebar-active-bg/50 min-w-0 flex-1"
              title="Edit profile"
            >
              <UserAvatar
                userId={currentUserId}
                displayName={currentProfile?.displayName ?? '?'}
                profileImageUrl={currentProfile?.profileImageUrl ?? null}
                size={24}
              />
              <span className="text-xs truncate" style={{ color: 'hsl(var(--sidebar-fg))' }}>
                {currentProfile?.displayName ?? ''}
              </span>
            </button>
          )}
          <ThemeToggle />
        </div>
      </aside>

      {composing && <ComposeDialog onClose={() => setComposing(false)} />}
      {creating && <CreateTopicDialog onClose={() => setCreating(false)} />}
      {browsing && <TopicDiscoveryDialog onClose={() => setBrowsing(false)} />}
      {managingGroups && <SidebarGroupManager onClose={() => setManagingGroups(false)} />}
      {profileOpen && currentUserId && (
        <UserProfileDialog userId={currentUserId} onClose={() => setProfileOpen(false)} />
      )}
    </>
  )
}

/** Group heading that also acts as a drop target */
function DroppableGroupHeading({
  group,
  roomCount,
  isActiveDrag,
  onToggleCollapse,
}: {
  group: { id: string; name: string; isCollapsed: boolean }
  roomCount: number
  isActiveDrag: boolean
  onToggleCollapse: () => void
}) {
  const { setNodeRef, isOver } = useDroppable({ id: groupHeadingId(group.id) })

  return (
    <button
      ref={setNodeRef}
      className="w-full flex items-center gap-1 px-3 py-1 text-xs font-semibold uppercase tracking-wider transition-colors rounded"
      style={{
        color: 'hsl(var(--sidebar-muted-fg))',
        background: isOver && isActiveDrag ? 'hsl(var(--sidebar-active-bg) / 0.4)' : 'transparent',
        outline: isOver && isActiveDrag ? '1px dashed hsl(var(--sidebar-muted-fg))' : 'none',
      }}
      onClick={onToggleCollapse}
    >
      {group.isCollapsed
        ? <ChevronRight className="w-3 h-3 shrink-0" />
        : <ChevronDown className="w-3 h-3 shrink-0" />}
      <span className="truncate">{group.name}</span>
      {group.isCollapsed && roomCount > 0 && (
        <span className="ml-auto text-xs opacity-60">{roomCount}</span>
      )}
    </button>
  )
}

/** Room item that participates in drag-and-drop sorting */
function SortableRoomItem({ room, isActive, onClick }: { room: Room; isActive: boolean; onClick: () => void }) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: room.id })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.3 : 1,
  }

  return (
    <div ref={setNodeRef} style={style}>
      <RoomItem
        room={room}
        isActive={isActive}
        onClick={onClick}
        dragHandleProps={{ ...attributes, ...listeners }}
      />
    </div>
  )
}

function RoomItem({
  room,
  isActive,
  onClick,
  dragHandleProps,
}: {
  room: Room
  isActive: boolean
  onClick: () => void
  dragHandleProps?: React.HTMLAttributes<HTMLElement>
}) {
  const [confirming, setConfirming] = useState(false)
  const [notifMenuOpen, setNotifMenuOpen] = useState(false)
  const [showTransfer, setShowTransfer] = useState(false)
  const confirmRef = useRef<HTMLDivElement>(null)
  const notifMenuRef = useRef<HTMLDivElement>(null)
  const setOverride = useNotificationPreferencesStore(s => s.setOverride)
  const overrides = useNotificationPreferencesStore(s => s.overrides)
  const currentOverride: ConversationOverrideLevel | undefined = overrides[room.id]
  const currentUserId = useCurrentUserStore(s => s.id)
  const isOwner = currentUserId !== null && room.ownerId === currentUserId
  const leaveTopic = useRoomStore(s => s.leaveTopic)

  // Close confirm popover when clicking outside
  useEffect(() => {
    if (!confirming) return
    function handleClickOutside(e: MouseEvent) {
      if (confirmRef.current && !confirmRef.current.contains(e.target as Node)) {
        setConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [confirming])

  // Close notif menu when clicking outside
  useEffect(() => {
    if (!notifMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (notifMenuRef.current && !notifMenuRef.current.contains(e.target as Node)) {
        setNotifMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [notifMenuOpen])

  async function handleConfirmDelete() {
    setConfirming(false)
    await useRoomStore.getState().deleteRoom(room.id)
  }

  async function handleLeave() {
    setConfirming(false)
    if (isOwner && room.memberCount > 1) {
      setShowTransfer(true)
    } else {
      await leaveTopic(room.id)
    }
  }

  return (
    <>
    <div className="relative group">
      {/* Drag handle — hidden until hover */}
      <button
        className="absolute left-0 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center
          justify-center w-4 h-full cursor-grab active:cursor-grabbing touch-none"
        style={{ color: 'hsl(var(--sidebar-muted-fg) / 0.5)' }}
        {...dragHandleProps}
        onClick={e => e.stopPropagation()}
        tabIndex={-1}
        aria-label="Drag to reorder"
      >
        <GripVertical className="w-3 h-3" />
      </button>

      <button
        onClick={onClick}
        className="w-full flex items-center gap-2 pl-5 pr-4 py-1.5 text-sm text-left transition-colors"
        style={{
          background: isActive ? 'hsl(var(--sidebar-active-bg))' : 'transparent',
          color: isActive ? 'hsl(var(--sidebar-fg))' : 'hsl(var(--sidebar-muted-fg))',
          fontWeight: isActive ? '500' : undefined,
        }}
        onMouseEnter={e => {
          if (!isActive) e.currentTarget.style.background = 'hsl(var(--sidebar-active-bg) / 0.5)'
        }}
        onMouseLeave={e => {
          if (!isActive) e.currentTarget.style.background = 'transparent'
        }}
      >
        {room.isPrivate
          ? <Lock className="w-3 h-3 shrink-0" style={{ color: 'hsl(var(--sidebar-muted-fg))' }} />
          : <span style={{ color: 'hsl(var(--sidebar-muted-fg))' }}>#</span>
        }
        <span className="flex-1 truncate">{room.name}</span>
        {currentOverride && (
          <BellOff className="w-3 h-3 flex-shrink-0 opacity-60 group-hover:hidden" style={{ color: 'hsl(var(--sidebar-muted-fg))' }} />
        )}
        <UnreadBadge room={room} />
      </button>

      {/* Hover-reveal notification override button */}
      <div className="absolute right-7 top-1/2 -translate-y-1/2 hidden group-hover:flex z-30" ref={notifMenuRef}>
        <button
          onClick={(e) => { e.stopPropagation(); setNotifMenuOpen(o => !o) }}
          className="flex items-center justify-center w-5 h-5 rounded transition-colors"
          style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          title="Notification settings"
        >
          <BellOff className="w-3 h-3" />
        </button>
        {notifMenuOpen && (
          <div
            className="absolute right-0 top-full mt-1 z-30 w-44 rounded border bg-background shadow-md text-xs"
            style={{ borderColor: 'hsl(var(--border))' }}
            onClick={e => e.stopPropagation()}
          >
            {(['follow_global', 'all_messages', 'mentions_only', 'muted'] as const).map(level => {
                const isActiveLvl = currentOverride === level || (level === 'follow_global' && !currentOverride)
                return (
                  <button
                    key={level}
                    onClick={() => { setOverride(room.id, level); setNotifMenuOpen(false) }}
                    className="w-full px-3 py-1.5 text-left hover:bg-muted/60 flex items-center gap-2"
                    style={{ color: 'hsl(var(--foreground))' }}
                  >
                    <span className="w-3 text-center text-green-500">{isActiveLvl ? '✓' : ''}</span>
                    {level === 'follow_global' ? 'Follow Global Setting' :
                     level === 'all_messages' ? 'All Messages' :
                     level === 'mentions_only' ? '@Mentions Only' : 'Muted'}
                  </button>
                )
              })}
          </div>
        )}
      </div>

      {/* Hover-reveal action button: Delete (owner) or Leave (non-owner) */}
      {!room.isProtected && (
        <button
          onClick={(e) => { e.stopPropagation(); setConfirming(true) }}
          className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center
            justify-center w-5 h-5 rounded transition-colors"
          style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--destructive))')}
          onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
          title={isOwner ? 'Delete topic' : 'Leave topic'}
          aria-label={isOwner ? 'Delete topic' : 'Leave topic'}
        >
          &#x2715;
        </button>
      )}

      {/* Two-step confirm popover */}
      {confirming && (
        <div
          ref={confirmRef}
          className="absolute right-0 top-full z-20 mt-1 flex items-center gap-1 rounded border px-2 py-1 shadow-md text-xs"
          style={{
            background: 'hsl(var(--background))',
            borderColor: 'hsl(var(--border))',
            color: 'hsl(var(--foreground))',
          }}
        >
          <span style={{ color: 'hsl(var(--muted-foreground))' }}>{isOwner ? 'Delete?' : 'Leave?'}</span>
          <button
            onClick={isOwner ? handleConfirmDelete : handleLeave}
            className="rounded px-1.5 py-0.5 hover:opacity-90"
            style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="rounded px-1.5 py-0.5"
            style={{ background: 'hsl(var(--muted))', color: 'hsl(var(--foreground))' }}
          >
            No
          </button>
        </div>
      )}
    </div>
    {showTransfer && currentUserId && (
      <TransferOwnershipDialog
        roomId={room.id}
        currentUserId={currentUserId}
        onClose={() => setShowTransfer(false)}
      />
    )}
    </>
  )
}

function DmItem({ room, isActive, onClick }: { room: Room; isActive: boolean; onClick: () => void }) {
  const name = room.otherUserDisplayName ?? room.name
  const online = usePresenceStore(s => room.otherUserId ? s.isOnline(room.otherUserId) : false)
  const otherProfile = useUserProfileStore(s => room.otherUserId ? s.profiles[room.otherUserId] : undefined)
  const [confirming, setConfirming] = useState(false)
  const [notifMenuOpen, setNotifMenuOpen] = useState(false)
  const confirmRef = useRef<HTMLDivElement>(null)
  const notifMenuRef = useRef<HTMLDivElement>(null)
  const setOverride = useNotificationPreferencesStore(s => s.setOverride)
  const overrides = useNotificationPreferencesStore(s => s.overrides)
  const currentOverride: ConversationOverrideLevel | undefined = overrides[room.id]

  // Close confirm popover when clicking outside
  useEffect(() => {
    if (!confirming) return
    function handleClickOutside(e: MouseEvent) {
      if (confirmRef.current && !confirmRef.current.contains(e.target as Node)) {
        setConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [confirming])

  // Close notif menu when clicking outside
  useEffect(() => {
    if (!notifMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (notifMenuRef.current && !notifMenuRef.current.contains(e.target as Node)) {
        setNotifMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [notifMenuOpen])

  async function handleConfirmDelete() {
    setConfirming(false)
    await useRoomStore.getState().deleteRoom(room.id)
  }

  return (
    <div className="relative group">
      <button
        onClick={onClick}
        className="w-full flex items-center gap-2 px-4 py-1.5 text-sm text-left transition-colors"
        style={{
          background: isActive ? 'hsl(var(--sidebar-active-bg))' : 'transparent',
          color: isActive ? 'hsl(var(--sidebar-fg))' : 'hsl(var(--sidebar-muted-fg))',
          fontWeight: isActive ? '500' : undefined,
        }}
        onMouseEnter={e => {
          if (!isActive) e.currentTarget.style.background = 'hsl(var(--sidebar-active-bg) / 0.5)'
        }}
        onMouseLeave={e => {
          if (!isActive) e.currentTarget.style.background = 'transparent'
        }}
      >
        <div className="relative flex-shrink-0">
          <UserAvatar
            userId={room.otherUserId ?? room.id}
            displayName={otherProfile?.displayName ?? name}
            profileImageUrl={otherProfile?.profileImageUrl ?? null}
            avatarUrl={room.otherUserAvatarUrl}
            size={20}
          />
          <span
            className={`absolute -bottom-0.5 -right-0.5 w-2 h-2 rounded-full ${online ? 'bg-green-400' : 'bg-red-400'}`}
            style={{ boxShadow: '0 0 0 2px hsl(var(--sidebar-bg))' }}
          />
        </div>
        <span className="flex-1 truncate">{name}</span>
        {currentOverride && (
          <BellOff className="w-3 h-3 flex-shrink-0 opacity-60 group-hover:hidden" style={{ color: 'hsl(var(--sidebar-muted-fg))' }} />
        )}
        <UnreadBadge room={room} />
      </button>

      {/* Hover-reveal notification override button */}
      <div className="absolute right-7 top-1/2 -translate-y-1/2 hidden group-hover:flex z-30" ref={notifMenuRef}>
        <button
          onClick={(e) => { e.stopPropagation(); setNotifMenuOpen(o => !o) }}
          className="flex items-center justify-center w-5 h-5 rounded transition-colors"
          style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
          title="Notification settings"
        >
          <BellOff className="w-3 h-3" />
        </button>
        {notifMenuOpen && (
          <div
            className="absolute right-0 top-full mt-1 z-30 w-44 rounded border bg-background shadow-md text-xs"
            style={{ borderColor: 'hsl(var(--border))' }}
            onClick={e => e.stopPropagation()}
          >
            {(['follow_global', 'all_messages', 'mentions_only', 'muted'] as const).map(level => {
                const isActiveLvl = currentOverride === level || (level === 'follow_global' && !currentOverride)
                return (
                  <button
                    key={level}
                    onClick={() => { setOverride(room.id, level); setNotifMenuOpen(false) }}
                    className="w-full px-3 py-1.5 text-left hover:bg-muted/60 flex items-center gap-2"
                    style={{ color: 'hsl(var(--foreground))' }}
                  >
                    <span className="w-3 text-center text-green-500">{isActiveLvl ? '✓' : ''}</span>
                    {level === 'follow_global' ? 'Follow Global Setting' :
                     level === 'all_messages' ? 'All Messages' :
                     level === 'mentions_only' ? '@Mentions Only' : 'Muted'}
                  </button>
                )
              })}
          </div>
        )}
      </div>

      {/* Hover-reveal delete button */}
      <button
        onClick={(e) => { e.stopPropagation(); setConfirming(true) }}
        className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center
          justify-center w-5 h-5 rounded transition-colors"
        style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
        onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--destructive))')}
        onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
        title="Delete conversation"
        aria-label="Delete conversation"
      >
        &#x2715;
      </button>

      {/* Two-step confirm popover */}
      {confirming && (
        <div
          ref={confirmRef}
          className="absolute right-0 top-full z-20 mt-1 flex items-center gap-1 rounded border px-2 py-1 shadow-md text-xs"
          style={{
            background: 'hsl(var(--background))',
            borderColor: 'hsl(var(--border))',
            color: 'hsl(var(--foreground))',
          }}
        >
          <span style={{ color: 'hsl(var(--muted-foreground))' }}>Delete?</span>
          <button
            onClick={handleConfirmDelete}
            className="rounded px-1.5 py-0.5 hover:opacity-90"
            style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
          >
            Yes
          </button>
          <button
            onClick={() => setConfirming(false)}
            className="rounded px-1.5 py-0.5"
            style={{ background: 'hsl(var(--muted))', color: 'hsl(var(--foreground))' }}
          >
            No
          </button>
        </div>
      )}
    </div>
  )
}

function UnreadBadge({ room }: { room: Room }) {
  if (room.hasMention && room.unreadCount === 0) {
    return (
      <span
        className="ml-auto rounded-full px-1.5 py-0.5 text-xs font-semibold group-hover:hidden"
        style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
      >
        @
      </span>
    )
  }
  if (room.unreadCount === 0) return null
  return (
    <span
      className="ml-auto rounded-full px-1.5 py-0.5 text-xs font-semibold group-hover:hidden"
      style={
        room.hasMention
          ? { background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }
          : { background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }
      }
    >
      {room.unreadCount > 99 ? '99+' : room.unreadCount}
    </span>
  )
}
