import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { forceLogout, type AdminUser } from '../../services/adminApiService'

interface ForceLogoutConfirmDialogProps {
  user: AdminUser
  isSelf: boolean
  onClose: () => void
  onSuccess: () => void
}

export function ForceLogoutConfirmDialog({ user, isSelf, onClose, onSuccess }: ForceLogoutConfirmDialogProps) {
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [hours, setHours] = useState(1)

  async function handleConfirm() {
    setLoading(true)
    setError(null)
    try {
      await forceLogout(user.id, hours)
      onSuccess()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to ban user.'
      setError(msg)
    } finally {
      setLoading(false)
    }
  }

  function handleHoursChange(e: React.ChangeEvent<HTMLInputElement>) {
    const val = parseInt(e.target.value, 10)
    if (!isNaN(val) && val >= 1) setHours(val)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />

      {/* Dialog */}
      <div
        className="relative z-10 w-full max-w-md mx-4 rounded-lg border bg-background p-6 shadow-lg"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <div className="flex items-start gap-3 mb-4">
          <AlertTriangle className="w-5 h-5 text-destructive mt-0.5 shrink-0" />
          <div>
            <h2 className="text-base font-semibold">Ban User</h2>
            <p className="text-sm text-muted-foreground mt-1">
              <span className="font-medium text-foreground">{user.displayName}</span>{' '}
              will be immediately disconnected and blocked from logging in for the selected duration.
            </p>
          </div>
        </div>

        {isSelf && (
          <div className="mb-4 px-3 py-2 rounded-md text-sm text-amber-700 bg-amber-50 border border-amber-200">
            Warning: This will immediately end your own session.
          </div>
        )}

        <div className="mb-4">
          <label className="block text-sm font-medium mb-1.5" htmlFor="ban-hours">
            Ban duration (hours)
          </label>
          <input
            id="ban-hours"
            type="number"
            min={1}
            value={hours}
            onChange={handleHoursChange}
            onKeyDown={e => { if (['.', '-', 'e', 'E', '+'].includes(e.key)) e.preventDefault() }}
            className="w-32 px-3 py-1.5 text-sm rounded-md border bg-background text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            style={{ borderColor: 'hsl(var(--border))' }}
          />
        </div>

        {error && <p className="mb-4 text-sm text-destructive">{error}</p>}

        <div className="flex justify-end gap-3">
          <button
            onClick={onClose}
            disabled={loading}
            className="px-4 py-2 text-sm rounded-md border hover:bg-muted/50 transition-colors disabled:opacity-40"
            style={{ borderColor: 'hsl(var(--border))' }}
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            disabled={loading}
            className="px-4 py-2 text-sm rounded-md bg-destructive text-destructive-foreground hover:bg-destructive/90 transition-colors disabled:opacity-40"
          >
            {loading ? 'Banning…' : 'Ban'}
          </button>
        </div>
      </div>
    </div>
  )
}
