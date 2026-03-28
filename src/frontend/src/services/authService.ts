import { setAccessToken } from './apiClient'

const TOKEN_KEY        = 'littlechat_access_token'
const TOKEN_EXPIRY_KEY = 'littlechat_access_token_expires_at'

let _refreshTimer: ReturnType<typeof setTimeout> | null = null
let _refreshInFlight = false

export function isRefreshInFlight(): boolean {
  return _refreshInFlight
}

// ── Proactive refresh timer ───────────────────────────────────────────────────

function scheduleTokenRefresh() {
  if (_refreshTimer) clearTimeout(_refreshTimer)

  const expiresAt = Number(localStorage.getItem(TOKEN_EXPIRY_KEY) ?? 0)
  if (!expiresAt) return

  // Refresh 60 seconds before expiry
  const delay = expiresAt - Date.now() - 60_000
  if (delay <= 0) {
    // Already near/past expiry — refresh immediately
    void proactiveRefresh()
    return
  }

  _refreshTimer = setTimeout(() => void proactiveRefresh(), delay)
}

async function proactiveRefresh() {
  _refreshInFlight = true
  try {
    const res = await fetch('/auth/refresh', { method: 'POST', credentials: 'include' })
    if (!res.ok) {
      clearSession()
      window.dispatchEvent(new Event('session-expired'))
      return
    }
    const { access_token } = await res.json()
    storeToken(access_token)
  } catch {
    // Network error — will retry on next API call via apiClient reactive refresh
  } finally {
    _refreshInFlight = false
  }
}

// Listen for reactive refreshes performed by apiClient so the timer stays in sync
window.addEventListener('token-refreshed', (e) => {
  storeToken((e as CustomEvent<string>).detail)
})

// ── Public API ────────────────────────────────────────────────────────────────

export function login() {
  window.location.href = '/auth/login'
}

export function logout() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(TOKEN_EXPIRY_KEY)
  if (_refreshTimer) clearTimeout(_refreshTimer)
  setAccessToken(null)
  window.location.href = '/auth/logout'
}

export function storeToken(token: string) {
  let expiresAt: number
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    expiresAt = payload.exp ? payload.exp * 1000 : Date.now() + 60 * 60 * 1000
  } catch {
    expiresAt = Date.now() + 60 * 60 * 1000
  }
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(TOKEN_EXPIRY_KEY, String(expiresAt))
  setAccessToken(token)
  scheduleTokenRefresh()
  window.dispatchEvent(new Event('session-restored'))
}

export function getToken(): string | null {
  const token = localStorage.getItem(TOKEN_KEY)
  if (!token) return null

  const expiresAt = Number(localStorage.getItem(TOKEN_EXPIRY_KEY) ?? 0)
  if (expiresAt && Date.now() > expiresAt) {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(TOKEN_EXPIRY_KEY)
    return null
  }

  return token
}

export function isAuthenticated(): boolean {
  return getToken() !== null
}

/** Decodes the JWT payload and returns the preferred_username claim (display name). */
export function getCurrentUserDisplayName(): string | null {
  const token = getToken()
  if (!token) return null
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return payload.preferred_username ?? null
  } catch {
    return null
  }
}

/** Decodes the JWT payload and returns the sub claim (current user ID). */
export function getCurrentUserId(): string | null {
  const token = getToken()
  if (!token) return null
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return payload.sub ?? null
  } catch {
    return null
  }
}

/** Clears the stored auth session (used on force-logout). */
export function clearSession(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(TOKEN_EXPIRY_KEY)
  if (_refreshTimer) clearTimeout(_refreshTimer)
  setAccessToken(null)
}

/** Called on app startup — restores token from localStorage into in-memory store. */
export function restoreSession(): boolean {
  const token = getToken()
  if (token) {
    setAccessToken(token)
    scheduleTokenRefresh()
    return true
  }
  return false
}
