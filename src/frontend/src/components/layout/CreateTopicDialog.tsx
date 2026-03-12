import { useEffect, useRef, useState } from 'react'
import { X } from 'lucide-react'
import { useRoomStore } from '../../stores/roomStore'
import { api } from '../../services/apiClient'
import type { UserSearchResult } from '../../types'

interface Props {
  onClose: () => void
  onCreated?: (roomId: string) => void
}

export function CreateTopicDialog({ onClose, onCreated }: Props) {
  const [name, setName] = useState('')
  const [isPrivate, setIsPrivate] = useState(false)
  const [userSearch, setUserSearch] = useState('')
  const [searchResults, setSearchResults] = useState<UserSearchResult[]>([])
  const [selectedUsers, setSelectedUsers] = useState<UserSearchResult[]>([])
  const [nameError, setNameError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const searchRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const createTopic = useRoomStore(s => s.createTopic)
  const setActiveRoom = useRoomStore(s => s.setActiveRoom)

  useEffect(() => {
    if (!isPrivate) {
      setUserSearch('')
      setSearchResults([])
      setSelectedUsers([])
    }
  }, [isPrivate])

  useEffect(() => {
    if (!isPrivate || userSearch.trim().length < 1) {
      setSearchResults([])
      return
    }
    if (searchRef.current) clearTimeout(searchRef.current)
    searchRef.current = setTimeout(async () => {
      try {
        const results = await api.get<UserSearchResult[]>(`/api/users?q=${encodeURIComponent(userSearch)}`)
        setSearchResults(results.filter(u => !selectedUsers.some(s => s.id === u.id)))
      } catch {
        setSearchResults([])
      }
    }, 300)
    return () => { if (searchRef.current) clearTimeout(searchRef.current) }
  }, [userSearch, isPrivate, selectedUsers])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = name.trim()
    if (!trimmed) { setNameError('Topic name is required.'); return }
    if (trimmed.length > 100) { setNameError('Name must be 100 characters or fewer.'); return }
    setNameError(null)
    setSubmitting(true)
    try {
      const room = await createTopic(trimmed, isPrivate, selectedUsers.map(u => u.id))
      setActiveRoom(room.id)
      onCreated?.(room.id)
      onClose()
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status
      if (status === 409) {
        setNameError('A topic with this name already exists.')
      } else {
        setNameError('Failed to create topic. Please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  function toggleUser(user: UserSearchResult) {
    setSelectedUsers(prev =>
      prev.some(u => u.id === user.id)
        ? prev.filter(u => u.id !== user.id)
        : [...prev, user]
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div
        className="w-full max-w-md rounded-lg border bg-background shadow-xl p-6 relative"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <button
          onClick={onClose}
          className="absolute right-4 top-4 text-muted-foreground hover:text-foreground"
          aria-label="Close"
        >
          <X className="w-4 h-4" />
        </button>

        <h2 className="text-base font-semibold mb-4">Create Topic</h2>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium">Topic name</label>
            <input
              autoFocus
              value={name}
              onChange={e => { setName(e.target.value); setNameError(null) }}
              placeholder="e.g. announcements"
              maxLength={100}
              className="rounded border px-3 py-1.5 text-sm focus:outline-none focus:ring-1"
              style={{ borderColor: nameError ? 'hsl(var(--destructive))' : 'hsl(var(--border))' }}
            />
            {nameError && <p className="text-xs" style={{ color: 'hsl(var(--destructive))' }}>{nameError}</p>}
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-sm font-medium">Visibility</label>
            <div className="flex gap-4">
              {(['public', 'private'] as const).map(opt => (
                <label key={opt} className="flex items-center gap-1.5 text-sm cursor-pointer">
                  <input
                    type="radio"
                    name="visibility"
                    value={opt}
                    checked={isPrivate === (opt === 'private')}
                    onChange={() => setIsPrivate(opt === 'private')}
                  />
                  {opt.charAt(0).toUpperCase() + opt.slice(1)}
                </label>
              ))}
            </div>
          </div>

          {isPrivate && (
            <div className="flex flex-col gap-2">
              <label className="text-sm font-medium">Invite members</label>
              <input
                value={userSearch}
                onChange={e => setUserSearch(e.target.value)}
                placeholder="Search by name…"
                className="rounded border px-3 py-1.5 text-sm focus:outline-none focus:ring-1"
                style={{ borderColor: 'hsl(var(--border))' }}
              />
              {searchResults.length > 0 && (
                <div className="rounded border text-sm max-h-36 overflow-y-auto" style={{ borderColor: 'hsl(var(--border))' }}>
                  {searchResults.map(u => (
                    <label key={u.id} className="flex items-center gap-2 px-3 py-1.5 hover:bg-muted/60 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={selectedUsers.some(s => s.id === u.id)}
                        onChange={() => toggleUser(u)}
                      />
                      {u.displayName}
                    </label>
                  ))}
                </div>
              )}
              {selectedUsers.length > 0 && (
                <div className="flex flex-wrap gap-1">
                  {selectedUsers.map(u => (
                    <span
                      key={u.id}
                      className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs"
                      style={{ background: 'hsl(var(--primary) / 0.15)', color: 'hsl(var(--primary))' }}
                    >
                      {u.displayName}
                      <button type="button" onClick={() => toggleUser(u)} className="opacity-70 hover:opacity-100">×</button>
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}

          <div className="flex justify-end gap-2 mt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded px-3 py-1.5 text-sm"
              style={{ color: 'hsl(var(--muted-foreground))' }}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded px-3 py-1.5 text-sm font-medium disabled:opacity-50"
              style={{ background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }}
            >
              {submitting ? 'Creating…' : 'Create'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
