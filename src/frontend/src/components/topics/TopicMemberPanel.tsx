import { useEffect, useRef, useState } from 'react'
import { X, Crown, UserMinus, UserPlus } from 'lucide-react'
import { api } from '../../services/apiClient'
import { useCurrentUserStore } from '../../stores/currentUserStore'
import type { RoomMember, UserSearchResult } from '../../types'

interface Props {
  roomId: string
  onClose: () => void
}

export function TopicMemberPanel({ roomId, onClose }: Props) {
  const [members, setMembers] = useState<RoomMember[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [searchQuery, setSearchQuery] = useState('')
  const [searchResults, setSearchResults] = useState<UserSearchResult[]>([])
  const [searching, setSearching] = useState(false)
  const [addError, setAddError] = useState<string | null>(null)

  const [kickConfirmId, setKickConfirmId] = useState<string | null>(null)

  const currentUserId = useCurrentUserStore(s => s.id)

  async function fetchMembers() {
    try {
      const data = await api.get<RoomMember[]>(`/api/rooms/${roomId}/members`)
      setMembers(data)
    } catch {
      setError('Failed to load members.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchMembers()
  }, [roomId]) // eslint-disable-line react-hooks/exhaustive-deps

  // Re-fetch when MemberListChanged fires for this room
  useEffect(() => {
    function handler(e: Event) {
      const evt = e as CustomEvent<{ roomId: string }>
      if (evt.detail.roomId === roomId) {
        fetchMembers()
      }
    }
    window.addEventListener('memberListChanged', handler)
    return () => window.removeEventListener('memberListChanged', handler)
  }, [roomId]) // eslint-disable-line react-hooks/exhaustive-deps

  const currentUserIsOwner = !!currentUserId && members.some(m => m.userId === currentUserId && m.isOwner)

  // Search for users to add (debounced)
  const searchTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(() => {
    if (searchTimeoutRef.current) clearTimeout(searchTimeoutRef.current)
    if (!searchQuery.trim()) {
      setSearchResults([])
      return
    }
    searchTimeoutRef.current = setTimeout(async () => {
      setSearching(true)
      try {
        const results = await api.get<UserSearchResult[]>(`/api/users?q=${encodeURIComponent(searchQuery.trim())}`)
        // Exclude already-members
        const memberIds = new Set(members.map(m => m.userId))
        setSearchResults(results.filter(u => !memberIds.has(u.id)))
      } catch {
        setSearchResults([])
      } finally {
        setSearching(false)
      }
    }, 250)
    return () => { if (searchTimeoutRef.current) clearTimeout(searchTimeoutRef.current) }
  }, [searchQuery, members])

  async function handleAddUser(userId: string, displayName: string) {
    setAddError(null)
    try {
      await api.post(`/api/rooms/${roomId}/invite`, { targetUserId: userId })
      setSearchQuery('')
      setSearchResults([])
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status
      if (status === 400) {
        setAddError(`${displayName} is already a member.`)
      } else {
        setAddError('Failed to add member.')
      }
    }
  }

  async function handleKick(targetUserId: string) {
    try {
      await api.delete(`/api/rooms/${roomId}/members/${targetUserId}`)
      setKickConfirmId(null)
      setMembers(prev => prev.filter(m => m.userId !== targetUserId))
    } catch {
      setError('Failed to remove member.')
    }
  }

  return (
    <aside
      className="w-64 flex-shrink-0 flex flex-col border-l bg-background h-full"
      style={{ borderColor: 'hsl(var(--border))' }}
    >
      {/* Header */}
      <div
        className="flex items-center justify-between px-4 py-3 border-b flex-shrink-0"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <span className="text-sm font-semibold">Members</span>
        <button
          onClick={onClose}
          className="text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Close member panel"
        >
          <X className="w-4 h-4" />
        </button>
      </div>

      {/* Add member search */}
      <div className="px-3 py-2 border-b flex-shrink-0" style={{ borderColor: 'hsl(var(--border))' }}>
        <div className="relative">
          <input
            type="text"
            placeholder="Add a member…"
            value={searchQuery}
            onChange={e => { setSearchQuery(e.target.value); setAddError(null) }}
            className="w-full rounded border px-3 py-1.5 text-xs focus:outline-none focus:ring-1"
            style={{ borderColor: 'hsl(var(--border))' }}
          />
          {searching && (
            <span className="absolute right-2 top-1/2 -translate-y-1/2 text-xs text-muted-foreground">…</span>
          )}
        </div>
        {addError && <p className="mt-1 text-xs" style={{ color: 'hsl(var(--destructive))' }}>{addError}</p>}
        {searchResults.length > 0 && (
          <div
            className="mt-1 rounded border bg-background shadow-md text-xs overflow-hidden"
            style={{ borderColor: 'hsl(var(--border))' }}
          >
            {searchResults.map(user => (
              <button
                key={user.id}
                onClick={() => handleAddUser(user.id, user.displayName)}
                className="w-full flex items-center gap-2 px-3 py-1.5 text-left hover:bg-muted/60 transition-colors"
              >
                <UserPlus className="w-3 h-3 flex-shrink-0 text-muted-foreground" />
                <span className="truncate">{user.displayName}</span>
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Member list */}
      <div className="flex-1 overflow-y-auto py-1">
        {loading && (
          <p className="px-4 py-2 text-xs text-muted-foreground">Loading…</p>
        )}
        {error && (
          <p className="px-4 py-2 text-xs" style={{ color: 'hsl(var(--destructive))' }}>{error}</p>
        )}
        {!loading && !error && members.map(member => (
          <div
            key={member.userId}
            className="group flex items-center gap-2 px-3 py-1.5 hover:bg-muted/30 transition-colors"
          >
            {/* Avatar placeholder */}
            <div
              className="w-6 h-6 rounded-full flex-shrink-0 flex items-center justify-center text-xs font-semibold"
              style={{ background: 'hsl(var(--muted))', color: 'hsl(var(--muted-foreground))' }}
            >
              {member.displayName.charAt(0).toUpperCase()}
            </div>

            <span className="flex-1 text-xs truncate">{member.displayName}</span>

            {member.isOwner && (
              <span title="Topic owner" aria-label="Topic owner">
                <Crown
                  className="w-3 h-3 flex-shrink-0"
                  style={{ color: 'hsl(var(--primary))' }}
                />
              </span>
            )}

            {/* Kick button — only shown to owner, and not for themselves */}
            {currentUserIsOwner && !member.isOwner && (
              kickConfirmId === member.userId ? (
                <div className="flex items-center gap-1 ml-1">
                  <button
                    onClick={() => handleKick(member.userId)}
                    className="text-xs rounded px-1 py-0.5 hover:opacity-90"
                    style={{ background: 'hsl(var(--destructive))', color: 'hsl(var(--destructive-foreground))' }}
                  >
                    Yes
                  </button>
                  <button
                    onClick={() => setKickConfirmId(null)}
                    className="text-xs rounded px-1 py-0.5"
                    style={{ background: 'hsl(var(--muted))', color: 'hsl(var(--foreground))' }}
                  >
                    No
                  </button>
                </div>
              ) : (
                <button
                  onClick={() => setKickConfirmId(member.userId)}
                  className="opacity-0 group-hover:opacity-100 transition-opacity text-muted-foreground
                    hover:text-destructive"
                  title={`Remove ${member.displayName}`}
                  aria-label={`Remove ${member.displayName}`}
                >
                  <UserMinus className="w-3 h-3" />
                </button>
              )
            )}
          </div>
        ))}
        {!loading && !error && members.length === 0 && (
          <p className="px-4 py-2 text-xs text-muted-foreground">No members found.</p>
        )}
      </div>

      <div
        className="px-3 py-2 border-t flex-shrink-0"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <p className="text-xs text-muted-foreground">
          {members.length} {members.length === 1 ? 'member' : 'members'}
        </p>
      </div>
    </aside>
  )
}
