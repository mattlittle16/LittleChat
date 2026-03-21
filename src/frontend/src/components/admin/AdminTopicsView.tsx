import { useCallback, useEffect, useRef, useState } from 'react'
import { Search, ChevronDown, ChevronRight, UserMinus, UserPlus, Plus, Trash2 } from 'lucide-react'
import {
  getTopics, getTopicMembers, addTopicMember, removeTopicMember, getUsers,
  createTopic, deleteTopic,
  type AdminTopic, type AdminTopicMember, type AdminUser, type PaginatedResult,
} from '../../services/adminApiService'

export function AdminTopicsView() {
  const [result, setResult] = useState<PaginatedResult<AdminTopic> | null>(null)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [expandedTopicId, setExpandedTopicId] = useState<string | null>(null)
  const [membersLoading, setMembersLoading] = useState(false)
  const membersCache = useRef<Record<string, AdminTopicMember[]>>({})
  const [membersCacheVersion, setMembersCacheVersion] = useState(0)

  const [removingId, setRemovingId] = useState<string | null>(null)
  const [addSearch, setAddSearch] = useState('')
  const [addResults, setAddResults] = useState<AdminUser[]>([])
  const [addSearchLoading, setAddSearchLoading] = useState(false)
  const [addingId, setAddingId] = useState<string | null>(null)
  const [memberError, setMemberError] = useState<string | null>(null)

  const [createName, setCreateName] = useState('')
  const [creating, setCreating] = useState(false)
  const [createError, setCreateError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null)

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const addDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const fetchTopics = useCallback(async (q: string, p: number) => {
    setLoading(true)
    setError(null)
    try {
      const data = await getTopics({ q: q || undefined, page: p, pageSize: 50 })
      setResult(data)
    } catch {
      setError('Failed to load topics.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchTopics(search, page)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, fetchTopics])

  function handleSearchChange(value: string) {
    setSearch(value)
    setPage(1)
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => fetchTopics(value, 1), 300)
  }

  async function fetchMembers(topicId: string) {
    setMembersLoading(true)
    try {
      const data = await getTopicMembers(topicId)
      membersCache.current[topicId] = data.members
      setMembersCacheVersion(v => v + 1)
    } catch {
      membersCache.current[topicId] = []
      setMembersCacheVersion(v => v + 1)
    } finally {
      setMembersLoading(false)
    }
  }

  async function toggleMembers(topicId: string) {
    if (expandedTopicId === topicId) {
      setExpandedTopicId(null)
      setAddSearch('')
      setAddResults([])
      setMemberError(null)
      return
    }
    setExpandedTopicId(topicId)
    setAddSearch('')
    setAddResults([])
    setMemberError(null)
    if (!membersCache.current[topicId]) {
      await fetchMembers(topicId)
    }
  }

  async function handleRemoveMember(topicId: string, userId: string) {
    setRemovingId(userId)
    setMemberError(null)
    try {
      await removeTopicMember(topicId, userId)
      delete membersCache.current[topicId]
      await fetchMembers(topicId)
    } catch {
      setMemberError('Failed to remove member.')
    } finally {
      setRemovingId(null)
    }
  }

  async function handleAddMember(topicId: string, user: AdminUser) {
    setAddingId(user.id)
    setMemberError(null)
    try {
      await addTopicMember(topicId, user.id)
      delete membersCache.current[topicId]
      await fetchMembers(topicId)
      setAddSearch('')
      setAddResults([])
    } catch {
      setMemberError('Failed to add member.')
    } finally {
      setAddingId(null)
    }
  }

  async function handleCreateTopic(e: React.FormEvent) {
    e.preventDefault()
    const name = createName.trim()
    if (!name) return
    setCreating(true)
    setCreateError(null)
    try {
      await createTopic(name)
      setCreateName('')
      fetchTopics(search, page)
    } catch {
      setCreateError('Failed to create topic.')
    } finally {
      setCreating(false)
    }
  }

  async function handleDeleteTopic(topicId: string) {
    if (confirmDeleteId !== topicId) {
      setConfirmDeleteId(topicId)
      return
    }
    setDeletingId(topicId)
    setConfirmDeleteId(null)
    try {
      await deleteTopic(topicId)
      if (expandedTopicId === topicId) setExpandedTopicId(null)
      fetchTopics(search, page)
    } catch {
      // silently ignore — topic list will refresh on next action
    } finally {
      setDeletingId(null)
    }
  }

  function handleAddSearchChange(topicId: string, value: string) {
    setAddSearch(value)
    if (addDebounceRef.current) clearTimeout(addDebounceRef.current)
    if (!value.trim()) { setAddResults([]); return }
    addDebounceRef.current = setTimeout(async () => {
      setAddSearchLoading(true)
      try {
        const data = await getUsers({ q: value, pageSize: 10 })
        const currentMembers = membersCache.current[topicId] ?? []
        const memberIds = new Set(currentMembers.map(m => m.id))
        setAddResults(data.items.filter(u => !memberIds.has(u.id)))
      } catch {
        setAddResults([])
      } finally {
        setAddSearchLoading(false)
      }
    }, 300)
  }

  const expandedMembers = expandedTopicId
    ? (membersCache.current[expandedTopicId] ?? null)
    : null
  // membersCacheVersion is read to trigger re-renders when cache updates
  void membersCacheVersion

  return (
    <div className="space-y-4">
      {/* Search + Create */}
      <div className="flex items-center gap-3 flex-wrap">
        <div className="relative max-w-sm flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <input
            type="text"
            placeholder="Search topics…"
            value={search}
            onChange={e => handleSearchChange(e.target.value)}
            className="w-full pl-9 pr-3 py-2 text-sm rounded-md border border-border bg-muted/90 dark:bg-white/[0.06] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
        <form onSubmit={handleCreateTopic} className="flex items-center gap-2">
          <input
            type="text"
            placeholder="New topic name…"
            value={createName}
            onChange={e => setCreateName(e.target.value)}
            className="pl-3 pr-3 py-2 text-sm rounded-md border border-border bg-muted/90 dark:bg-white/[0.06] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring w-44"
          />
          <button
            type="submit"
            disabled={creating || !createName.trim()}
            className="flex items-center gap-1.5 px-3 py-2 text-sm rounded-md border border-border hover:bg-muted/50 transition-colors disabled:opacity-40"
          >
            <Plus className="w-4 h-4" />
            {creating ? 'Creating…' : 'Create'}
          </button>
        </form>
      </div>
      {createError && <p className="text-sm text-destructive">{createError}</p>}

      {loading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {error && <p className="text-sm text-destructive">{error}</p>}

      {!loading && result && (
        <>
          {result.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No topics found.</p>
          ) : (
            <div className="rounded-md border border-border overflow-hidden">
              {result.items.map(topic => (
                <div key={topic.id}>
                  <div className="flex items-center border-b border-border last:border-b-0">
                    <button
                      onClick={() => toggleMembers(topic.id)}
                      className="flex-1 flex items-center gap-3 px-4 py-3 text-sm hover:bg-muted/50 transition-colors text-left"
                    >
                      {expandedTopicId === topic.id
                        ? <ChevronDown className="w-4 h-4 text-muted-foreground shrink-0" />
                        : <ChevronRight className="w-4 h-4 text-muted-foreground shrink-0" />
                      }
                      <span className="flex-1 font-medium">{topic.name}</span>
                      <span className="text-muted-foreground text-xs">{topic.memberCount} member{topic.memberCount !== 1 ? 's' : ''}</span>
                    </button>
                    <button
                      onClick={() => handleDeleteTopic(topic.id)}
                      disabled={deletingId === topic.id}
                      onBlur={() => { if (confirmDeleteId === topic.id) setConfirmDeleteId(null) }}
                      className={`flex items-center gap-1 text-xs px-3 py-3 transition-colors disabled:opacity-40 shrink-0 ${
                        confirmDeleteId === topic.id
                          ? 'text-white bg-destructive hover:bg-destructive/90'
                          : 'text-destructive hover:bg-destructive/10'
                      }`}
                      title="Delete topic"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                      {deletingId === topic.id ? 'Deleting…' : confirmDeleteId === topic.id ? 'Confirm?' : 'Delete'}
                    </button>
                  </div>

                  {expandedTopicId === topic.id && (
                    <div
                      className="px-6 py-3 bg-muted/20 border-b border-border space-y-3"
                    >
                      {memberError && (
                        <p className="text-xs text-destructive">{memberError}</p>
                      )}

                      {/* Member list */}
                      {membersLoading && expandedMembers === null ? (
                        <p className="text-xs text-muted-foreground">Loading members…</p>
                      ) : expandedMembers === null || expandedMembers.length === 0 ? (
                        <p className="text-xs text-muted-foreground">No members.</p>
                      ) : (
                        <ul className="space-y-1">
                          {expandedMembers.map(member => (
                            <li key={member.id} className="flex items-center justify-between gap-2 text-sm">
                              <span>{member.displayName}</span>
                              <button
                                onClick={() => handleRemoveMember(topic.id, member.id)}
                                disabled={removingId === member.id}
                                className="flex items-center gap-1 text-xs px-2 py-0.5 rounded text-destructive hover:bg-destructive/10 transition-colors disabled:opacity-40"
                                title="Remove from topic"
                              >
                                <UserMinus className="w-3 h-3" />
                                {removingId === member.id ? 'Removing…' : 'Remove'}
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}

                      {/* Add user */}
                      <div className="pt-1 border-t border-border">
                        <p className="text-xs font-medium text-muted-foreground mb-1.5">Add user</p>
                        <div className="relative">
                          <UserPlus className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground" />
                          <input
                            type="text"
                            placeholder="Search by name…"
                            value={addSearch}
                            onChange={e => handleAddSearchChange(topic.id, e.target.value)}
                            className="w-full max-w-xs pl-8 pr-3 py-1.5 text-sm rounded-md border border-border bg-muted/90 dark:bg-white/[0.06] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                          />
                          {(addSearchLoading || addResults.length > 0) && (
                            <div
                              className="absolute z-10 mt-1 w-full max-w-xs rounded-md border border-border bg-background shadow-md overflow-hidden"
                            >
                              {addSearchLoading ? (
                                <p className="px-3 py-2 text-xs text-muted-foreground">Searching…</p>
                              ) : addResults.length === 0 ? (
                                <p className="px-3 py-2 text-xs text-muted-foreground">No users found.</p>
                              ) : (
                                addResults.map(user => (
                                  <button
                                    key={user.id}
                                    onClick={() => handleAddMember(topic.id, user)}
                                    disabled={addingId === user.id}
                                    className="w-full text-left px-3 py-2 text-sm hover:bg-muted/50 transition-colors disabled:opacity-40 flex items-center justify-between"
                                  >
                                    <span>{user.displayName}</span>
                                    {addingId === user.id && (
                                      <span className="text-xs text-muted-foreground">Adding…</span>
                                    )}
                                  </button>
                                ))
                              )}
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {result.totalPages > 1 && (
            <div className="flex items-center gap-3 justify-end">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                className="text-sm px-3 py-1 rounded border border-border disabled:opacity-40 hover:bg-muted/50 transition-colors"
              >
                Previous
              </button>
              <span className="text-sm text-muted-foreground">
                Page {result.page} of {result.totalPages}
              </span>
              <button
                onClick={() => setPage(p => Math.min(result.totalPages, p + 1))}
                disabled={page === result.totalPages}
                className="text-sm px-3 py-1 rounded border border-border disabled:opacity-40 hover:bg-muted/50 transition-colors"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
