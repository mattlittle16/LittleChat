import { useCallback, useEffect, useRef, useState } from 'react'
import { X, Search, Users } from 'lucide-react'
import { useRoomStore } from '../../stores/roomStore'

interface DiscoverTopicItem {
  id: string
  name: string
  memberCount: number
  createdAt: string
}

interface Props {
  onClose: () => void
}

export function TopicDiscoveryDialog({ onClose }: Props) {
  const [searchTerm, setSearchTerm] = useState('')
  const [results, setResults] = useState<DiscoverTopicItem[]>([])
  const [loading, setLoading] = useState(false)
  const [joiningId, setJoiningId] = useState<string | null>(null)
  const [joinedIds, setJoinedIds] = useState<Set<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const discoverTopics = useRoomStore(s => s.discoverTopics)
  const joinTopic = useRoomStore(s => s.joinTopic)

  const loadResults = useCallback((term: string) => {
    setLoading(true)
    setError(null)
    discoverTopics(term)
      .then(setResults)
      .catch(() => setError('Failed to load topics.'))
      .finally(() => setLoading(false))
  }, [discoverTopics])

  useEffect(() => {
    loadResults('')
  }, [loadResults])

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => loadResults(searchTerm), 300)
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current) }
  }, [searchTerm, loadResults])

  async function handleJoin(topicId: string) {
    setJoiningId(topicId)
    try {
      await joinTopic(topicId)
      setJoinedIds(prev => new Set([...prev, topicId]))
    } catch {
      setError('Failed to join topic. Please try again.')
    } finally {
      setJoiningId(null)
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

        <h2 className="text-base font-semibold mb-1">Browse Topics</h2>
        <p className="text-xs text-muted-foreground mb-4">
          Find and join public topics.
        </p>

        <div
          className="flex items-center gap-2 rounded border px-3 py-1.5 mb-4"
          style={{ borderColor: 'hsl(var(--border))' }}
        >
          <Search className="w-4 h-4 text-muted-foreground shrink-0" />
          <input
            autoFocus
            value={searchTerm}
            onChange={e => setSearchTerm(e.target.value)}
            placeholder="Search topics…"
            className="flex-1 text-sm bg-transparent focus:outline-none"
          />
        </div>

        {error && (
          <p className="text-sm mb-3" style={{ color: 'hsl(var(--destructive))' }}>{error}</p>
        )}

        <div className="flex-1 overflow-y-auto flex flex-col gap-1 min-h-0">
          {loading && (
            <p className="text-sm text-muted-foreground py-4 text-center">Loading…</p>
          )}

          {!loading && results.length === 0 && (
            <p className="text-sm text-muted-foreground py-4 text-center">No public topics found.</p>
          )}

          {!loading && results.map(topic => {
            const joined = joinedIds.has(topic.id)
            const isJoining = joiningId === topic.id
            return (
              <div
                key={topic.id}
                className="flex items-center justify-between rounded px-3 py-2 hover:bg-muted/40"
              >
                <div className="flex flex-col min-w-0">
                  <span className="text-sm font-medium truncate">{topic.name}</span>
                  <span className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Users className="w-3 h-3" />
                    {topic.memberCount} {topic.memberCount === 1 ? 'member' : 'members'}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => handleJoin(topic.id)}
                  disabled={joined || isJoining}
                  className="ml-3 shrink-0 rounded px-3 py-1 text-xs font-medium disabled:opacity-60"
                  style={joined
                    ? { background: 'hsl(var(--muted))', color: 'hsl(var(--muted-foreground))' }
                    : { background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }
                  }
                >
                  {isJoining ? 'Joining…' : joined ? 'Joined' : 'Join'}
                </button>
              </div>
            )
          })}
        </div>

        <div className="flex justify-end mt-4">
          <button
            type="button"
            onClick={onClose}
            className="rounded px-3 py-1.5 text-sm"
            style={{ color: 'hsl(var(--muted-foreground))' }}
          >
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
