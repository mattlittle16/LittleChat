import { setAccessToken } from './apiClient'

const TOKEN_KEY = 'access_token'
const TOKEN_EXPIRY_KEY = 'access_token_expires_at'
// JWT access tokens from Authentik are typically valid for 1 hour
const DEFAULT_TTL_MS = 60 * 60 * 1000

export function login() {
  window.location.href = '/auth/login'
}

export function logout() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(TOKEN_EXPIRY_KEY)
  setAccessToken(null)
  window.location.href = '/auth/login'
}

export function storeToken(token: string, expiresInSeconds?: number) {
  const expiresAt = Date.now() + (expiresInSeconds ? expiresInSeconds * 1000 : DEFAULT_TTL_MS)
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

/** Called on app startup — restores token from localStorage into in-memory store. */
export function restoreSession(): boolean {
  const token = getToken()
  if (token) {
    setAccessToken(token)
    return true
  }
  return false
}
