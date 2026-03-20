import { useCallback, useEffect, useRef, useState } from 'react'
import { Search } from 'lucide-react'
import { getUsers, unbanUser, type AdminUser, type PaginatedResult } from '../../services/adminApiService'
import { ForceLogoutConfirmDialog } from './ForceLogoutConfirmDialog'
import { getCurrentUserId } from '../../services/authService'

export function AdminUsersView() {
  const [result, setResult] = useState<PaginatedResult<AdminUser> | null>(null)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [logoutTarget, setLogoutTarget] = useState<AdminUser | null>(null)
  const [unbanningId, setUnbanningId] = useState<string | null>(null)

  function formatBanExpiry(bannedUntil: string): string {
    const date = new Date(bannedUntil)
    return date.toLocaleString(undefined, {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    })
  }
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const currentUserId = getCurrentUserId()

  const fetchUsers = useCallback(async (q: string, p: number) => {
    setLoading(true)
    setError(null)
    try {
      const data = await getUsers({ q: q || undefined, page: p, pageSize: 50 })
      setResult(data)
    } catch {
      setError('Failed to load users.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchUsers(search, page)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, fetchUsers])

  function handleSearchChange(value: string) {
    setSearch(value)
    setPage(1)
    if (debounceRef.current) clearTimeout(debounceRef.current)
    debounceRef.current = setTimeout(() => {
      fetchUsers(value, 1)
    }, 300)
  }

  async function handleUnban(user: AdminUser) {
    setUnbanningId(user.id)
    try {
      await unbanUser(user.id)
      fetchUsers(search, page)
    } catch {
      setError(`Failed to unban ${user.displayName}.`)
    } finally {
      setUnbanningId(null)
    }
  }

  return (
    <div className="space-y-4">
      {/* Search */}
      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
        <input
          type="text"
          placeholder="Search by name…"
          value={search}
          onChange={e => handleSearchChange(e.target.value)}
          className="w-full pl-9 pr-3 py-2 text-sm rounded-md border border-border bg-muted/90 dark:bg-white/[0.06] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </div>

      {/* Table */}
      {loading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {error && <p className="text-sm text-destructive">{error}</p>}
      {!loading && result && (
        <>
          {result.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No users found.</p>
          ) : (
            <div className="rounded-md border border-border overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-muted/90 dark:bg-white/[0.06] border-b border-border">
                  <tr>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Display Name</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody>
                  {result.items.map(user => (
                    <tr
                      key={user.id}
                      className="border-t border-border odd:bg-muted/30 hover:bg-muted/50 transition-colors"
                    >
                      <td className="px-4 py-2">
                        <div className="flex flex-col gap-0.5">
                          <div className="flex items-center gap-2">
                            <span>{user.displayName}</span>
                            {user.bannedUntil && (
                              <span className="text-xs px-1.5 py-0.5 rounded bg-destructive/15 text-destructive font-medium">
                                Banned
                              </span>
                            )}
                          </div>
                          {user.bannedUntil && (
                            <span className="text-xs text-muted-foreground">
                              Until {formatBanExpiry(user.bannedUntil)}
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-2 text-right align-top">
                        {user.bannedUntil ? (
                          <button
                            onClick={() => handleUnban(user)}
                            disabled={unbanningId === user.id}
                            className="text-xs px-3 py-1 rounded bg-muted text-foreground hover:bg-muted/70 transition-colors disabled:opacity-40"
                          >
                            {unbanningId === user.id ? 'Unbanning…' : 'Unban'}
                          </button>
                        ) : (
                          <button
                            onClick={() => setLogoutTarget(user)}
                            className="text-xs px-3 py-1 rounded bg-destructive/10 text-destructive hover:bg-destructive/20 transition-colors"
                          >
                            Ban
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Pagination */}
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

      {/* Force logout dialog */}
      {logoutTarget && (
        <ForceLogoutConfirmDialog
          user={logoutTarget}
          isSelf={logoutTarget.id === currentUserId}
          onClose={() => setLogoutTarget(null)}
          onSuccess={() => {
            setLogoutTarget(null)
            fetchUsers(search, page)
          }}
        />
      )}
    </div>
  )
}
