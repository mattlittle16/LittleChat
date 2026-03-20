import { useEffect, useState } from 'react'
import { getAuditLog, type AuditLogEntry, type PaginatedResult } from '../../services/adminApiService'

function formatAction(action: string): string {
  return action.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase())
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

export function AdminAuditLogView() {
  const [result, setResult] = useState<PaginatedResult<AuditLogEntry> | null>(null)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [hasLoaded, setHasLoaded] = useState(false)

  async function fetchLog(p: number) {
    setLoading(true)
    setError(null)
    try {
      const data = await getAuditLog({ from: from || undefined, to: to || undefined, page: p, pageSize: 50 })
      setResult(data)
      setHasLoaded(true)
    } catch {
      setError('Failed to load audit log.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchLog(page)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function handleApply() {
    setPage(1)
    fetchLog(1)
  }

  return (
    <div className="space-y-4">
      {/* Date range filter */}
      <div className="flex items-end gap-3 flex-wrap">
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">From</label>
          <input
            type="date"
            value={from}
            onChange={e => setFrom(e.target.value)}
            className="text-sm rounded-md border border-border bg-muted/90 dark:bg-white/[0.06] text-foreground px-3 py-2 focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-muted-foreground">To</label>
          <input
            type="date"
            value={to}
            onChange={e => setTo(e.target.value)}
            className="text-sm rounded-md border border-border bg-muted/90 dark:bg-white/[0.06] text-foreground px-3 py-2 focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
        <button
          onClick={handleApply}
          disabled={loading}
          className="px-4 py-2 text-sm rounded-md bg-primary text-primary-foreground hover:bg-primary/90 transition-colors disabled:opacity-40"
        >
          Apply
        </button>
        {(from || to) && (
          <button
            onClick={() => { setFrom(''); setTo(''); setTimeout(() => fetchLog(1), 0) }}
            className="px-3 py-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            Clear
          </button>
        )}
      </div>

      {loading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {error && <p className="text-sm text-destructive">{error}</p>}

      {!loading && result && (
        <>
          {result.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {hasLoaded && (from || to) ? 'No actions in this date range.' : 'No audit log entries yet.'}
            </p>
          ) : (
            <div className="rounded-md border border-border overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-muted/90 dark:bg-white/[0.06] border-b border-border">
                  <tr>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Time</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Admin</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Action</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Target</th>
                  </tr>
                </thead>
                <tbody>
                  {result.items.map(entry => {
                    const isSystemAction = !entry.adminName
                    return (
                      <tr
                        key={entry.id}
                        className={`border-t border-border transition-colors ${
                          isSystemAction
                            ? 'opacity-50 hover:opacity-70'
                            : 'odd:bg-muted/30 hover:bg-muted/50'
                        }`}
                      >
                        <td className="px-4 py-2 text-muted-foreground whitespace-nowrap">
                          {formatDate(entry.occurredAt)}
                        </td>
                        <td className="px-4 py-2">
                          {entry.adminName
                            ? entry.adminName
                            : <span className="text-xs text-muted-foreground italic">system</span>
                          }
                        </td>
                        <td className="px-4 py-2 font-medium">{formatAction(entry.action)}</td>
                        <td className="px-4 py-2 text-muted-foreground">{entry.targetName ?? '—'}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          {result.totalPages > 1 && (
            <div className="flex items-center gap-3 justify-end">
              <button
                onClick={() => { const p = Math.max(1, page - 1); setPage(p); fetchLog(p) }}
                disabled={page === 1}
                className="text-sm px-3 py-1 rounded border border-border disabled:opacity-40 hover:bg-muted/50 transition-colors"
              >
                Previous
              </button>
              <span className="text-sm text-muted-foreground">
                Page {result.page} of {result.totalPages}
              </span>
              <button
                onClick={() => { const p = Math.min(result.totalPages, page + 1); setPage(p); fetchLog(p) }}
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
