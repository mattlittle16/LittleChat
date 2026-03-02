import { useEffect, useRef, useState } from 'react'
import { api } from '../../services/apiClient'
import { useRoomStore } from '../../stores/roomStore'
import { SearchResult } from './SearchResult'
import type { SearchResultDto } from '../../types'

interface SearchModalProps {
  onClose: () => void
}

export function SearchModal({ onClose }: SearchModalProps) {
  const { activeRoomId, setActiveRoom } = useRoomStore()
  const [query, setQuery] = useState('')
  const [scope, setScope] = useState<'room' | 'global'>('room')
  const [results, setResults] = useState<SearchResultDto[]>([])
  const [loading, setLoading] = useState(false)
  const [searched, setSearched] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const abortRef = useRef<AbortController | null>(null)

  // Focus input on open
  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  // Close on Escape
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  async function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    const q = query.trim()
    if (!q) return

    abortRef.current?.abort()
    abortRef.current = new AbortController()

    setLoading(true)
    setSearched(true)

    try {
      const params = new URLSearchParams({ q, scope })
      if (scope === 'room' && activeRoomId) params.set('roomId', activeRoomId)

      const data = await api.get<SearchResultDto[]>(`/api/search?${params}`)
      setResults(data)
    } catch {
      setResults([])
    } finally {
      setLoading(false)
    }
  }

  function handleResultClick(result: SearchResultDto) {
    setActiveRoom(result.roomId)
    onClose()
  }

  return (
    // Backdrop
    <div
      className="fixed inset-0 z-50 flex items-start justify-center pt-[10vh] bg-black/40"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className="w-full max-w-lg bg-background rounded-lg shadow-xl flex flex-col max-h-[80vh] overflow-hidden border">
        {/* Search form */}
        <form onSubmit={handleSearch} className="flex items-center gap-2 p-3 border-b">
          <svg className="w-4 h-4 text-muted-foreground flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-4.35-4.35M17 11A6 6 0 1 1 5 11a6 6 0 0 1 12 0z" />
          </svg>
          <input
            ref={inputRef}
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Search messages…"
            className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
          />
          <button
            type="button"
            onClick={() => setScope(s => s === 'room' ? 'global' : 'room')}
            className={`rounded-full px-2.5 py-0.5 text-xs font-medium border transition-colors
              ${scope === 'global'
                ? 'bg-primary text-primary-foreground border-primary'
                : 'bg-muted text-muted-foreground border-border hover:border-foreground'}`}
            title="Toggle search scope"
          >
            {scope === 'room' ? 'This room' : 'All rooms'}
          </button>
        </form>

        {/* Results */}
        <div className="flex-1 overflow-y-auto">
          {loading && (
            <p className="text-center text-sm text-muted-foreground py-8">Searching…</p>
          )}
          {!loading && searched && results.length === 0 && (
            <p className="text-center text-sm text-muted-foreground py-8">No results found.</p>
          )}
          {!loading && results.map(r => (
            <SearchResult
              key={r.messageId}
              result={r}
              query={query}
              isGlobal={scope === 'global'}
              onClick={() => handleResultClick(r)}
            />
          ))}
          {!searched && (
            <p className="text-center text-sm text-muted-foreground py-8">
              Press Enter to search{scope === 'room' ? ' in this room' : ' all rooms'}.
            </p>
          )}
        </div>
      </div>
    </div>
  )
}
