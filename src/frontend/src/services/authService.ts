import { setAccessToken } from './apiClient'

const TOKEN_KEY = 'access_token'
const TOKEN_EXPIRY_KEY = 'access_token_expires_at'

export function login() {
  window.location.href = '/auth/login'
}

export function logout() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(TOKEN_EXPIRY_KEY)
  setAccessToken(null)
  window.location.href = '/auth/logout'
}

export function storeToken(token: string) {
  // Read expiry from the JWT `exp` claim (seconds since epoch) — authoritative from the IDP
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
  setAccessToken(null)
}

/** Called on app startup — restores token from localStorage into in-memory store. */
export function restoreSession(): boolean {
  const token = getToken()
  if (token) {
    setAccessToken(token)
    return true
  }
  return false
}
