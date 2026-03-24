import { describe, it, expect, beforeEach, vi } from 'vitest'
import {
  storeToken,
  getToken,
  isAuthenticated,
  clearSession,
  restoreSession,
  getCurrentUserDisplayName,
  getCurrentUserId,
} from '../services/authService'

// authService delegates calls to setAccessToken in apiClient
vi.mock('../services/apiClient', () => ({
  setAccessToken: vi.fn(),
}))

import { setAccessToken } from '../services/apiClient'

// ── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Build a minimal JWT with the given payload.  The signature part is faked —
 * authService never validates the signature, it only decodes the payload.
 */
function makeJwt(payload: Record<string, unknown>): string {
  const encoded = btoa(JSON.stringify(payload))
  return `header.${encoded}.signature`
}

function futureMs(offsetSeconds = 3600) {
  return Math.floor(Date.now() / 1000) + offsetSeconds
}

function pastMs(offsetSeconds = 3600) {
  return Math.floor(Date.now() / 1000) - offsetSeconds
}

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  localStorage.clear()
  vi.clearAllMocks()
})

// ── storeToken ────────────────────────────────────────────────────────────────

describe('storeToken', () => {
  it('persists the token in localStorage', () => {
    const jwt = makeJwt({ sub: 'u1', exp: futureMs() })
    storeToken(jwt)
    expect(localStorage.getItem('littlechat_access_token')).toBe(jwt)
  })

  it('derives expiry from the JWT exp claim', () => {
    const expSeconds = futureMs(7200)
    storeToken(makeJwt({ exp: expSeconds }))
    const stored = Number(localStorage.getItem('littlechat_access_token_expires_at'))
    expect(stored).toBe(expSeconds * 1000)
  })

  it('falls back to 1-hour expiry when exp claim is absent', () => {
    const before = Date.now()
    storeToken(makeJwt({ sub: 'u1' }))
    const after = Date.now()
    const stored = Number(localStorage.getItem('littlechat_access_token_expires_at'))
    expect(stored).toBeGreaterThanOrEqual(before + 60 * 60 * 1000 - 10)
    expect(stored).toBeLessThanOrEqual(after + 60 * 60 * 1000 + 10)
  })

  it('calls setAccessToken with the token', () => {
    const jwt = makeJwt({ sub: 'u1', exp: futureMs() })
    storeToken(jwt)
    expect(setAccessToken).toHaveBeenCalledWith(jwt)
  })

  it('does not throw when the token is not a valid JWT', () => {
    expect(() => storeToken('not.a.jwt')).not.toThrow()
  })
})

// ── getToken ──────────────────────────────────────────────────────────────────

describe('getToken', () => {
  it('returns the stored token when it is not expired', () => {
    const jwt = makeJwt({ exp: futureMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(futureMs() * 1000))
    expect(getToken()).toBe(jwt)
  })

  it('returns null and removes token when the token is expired', () => {
    const jwt = makeJwt({ exp: pastMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(Date.now() - 1000))

    expect(getToken()).toBeNull()
    expect(localStorage.getItem('littlechat_access_token')).toBeNull()
    expect(localStorage.getItem('littlechat_access_token_expires_at')).toBeNull()
  })

  it('returns null when localStorage is empty', () => {
    expect(getToken()).toBeNull()
  })
})

// ── isAuthenticated ───────────────────────────────────────────────────────────

describe('isAuthenticated', () => {
  it('returns true when a valid token is stored', () => {
    const jwt = makeJwt({ exp: futureMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(futureMs() * 1000))
    expect(isAuthenticated()).toBe(true)
  })

  it('returns false when no token is stored', () => {
    expect(isAuthenticated()).toBe(false)
  })

  it('returns false when the token is expired', () => {
    localStorage.setItem('littlechat_access_token', makeJwt({ exp: pastMs() }))
    localStorage.setItem('littlechat_access_token_expires_at', String(Date.now() - 1000))
    expect(isAuthenticated()).toBe(false)
  })
})

// ── clearSession ──────────────────────────────────────────────────────────────

describe('clearSession', () => {
  it('removes both localStorage keys', () => {
    localStorage.setItem('littlechat_access_token', 'tok')
    localStorage.setItem('littlechat_access_token_expires_at', '99999')
    clearSession()
    expect(localStorage.getItem('littlechat_access_token')).toBeNull()
    expect(localStorage.getItem('littlechat_access_token_expires_at')).toBeNull()
  })

  it('calls setAccessToken(null)', () => {
    clearSession()
    expect(setAccessToken).toHaveBeenCalledWith(null)
  })
})

// ── restoreSession ────────────────────────────────────────────────────────────

describe('restoreSession', () => {
  it('returns true and calls setAccessToken when a valid token is stored', () => {
    const jwt = makeJwt({ exp: futureMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(futureMs() * 1000))

    expect(restoreSession()).toBe(true)
    expect(setAccessToken).toHaveBeenCalledWith(jwt)
  })

  it('returns false and does not call setAccessToken when no session exists', () => {
    expect(restoreSession()).toBe(false)
    expect(setAccessToken).not.toHaveBeenCalled()
  })
})

// ── getCurrentUserDisplayName ─────────────────────────────────────────────────

describe('getCurrentUserDisplayName', () => {
  it('returns the preferred_username from a valid JWT', () => {
    const jwt = makeJwt({ preferred_username: 'alice', exp: futureMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(futureMs() * 1000))
    expect(getCurrentUserDisplayName()).toBe('alice')
  })

  it('returns null when no token is stored', () => {
    expect(getCurrentUserDisplayName()).toBeNull()
  })

  it('returns null when the preferred_username claim is absent', () => {
    const jwt = makeJwt({ sub: 'u1', exp: futureMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(futureMs() * 1000))
    expect(getCurrentUserDisplayName()).toBeNull()
  })
})

// ── getCurrentUserId ──────────────────────────────────────────────────────────

describe('getCurrentUserId', () => {
  it('returns the sub from a valid JWT', () => {
    const jwt = makeJwt({ sub: 'uid-123', exp: futureMs() })
    localStorage.setItem('littlechat_access_token', jwt)
    localStorage.setItem('littlechat_access_token_expires_at', String(futureMs() * 1000))
    expect(getCurrentUserId()).toBe('uid-123')
  })

  it('returns null when no token is stored', () => {
    expect(getCurrentUserId()).toBeNull()
  })
})
