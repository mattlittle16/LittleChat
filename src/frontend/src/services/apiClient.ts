// In-memory token store — set by AuthCallbackPage, read on every request
let _accessToken: string | null = null

export function setAccessToken(token: string | null) {
  _accessToken = token
}

export function getAccessToken(): string | null {
  return _accessToken ?? localStorage.getItem('littlechat_access_token')
}

interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

async function attemptTokenRefresh(): Promise<boolean> {
  try {
    const res = await fetch('/auth/refresh', { method: 'POST', credentials: 'include' })
    if (!res.ok) return false
    const { access_token } = await res.json()
    // Update in-memory token immediately so the retry uses the new token
    setAccessToken(access_token)
    // Notify authService to update localStorage and reschedule the proactive timer
    window.dispatchEvent(new CustomEvent<string>('token-refreshed', { detail: access_token }))
    return true
  } catch {
    return false
  }
}

async function request<T>(path: string, init: RequestInit = {}, isRetry = false): Promise<T> {
  const token = getAccessToken()
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(init.headers as Record<string, string>),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  }

  const response = await fetch(path, { ...init, headers })

  if (!response.ok) {
    if (response.status === 401 && !isRetry) {
      const refreshed = await attemptTokenRefresh()
      if (refreshed) return request<T>(path, init, true)
      window.dispatchEvent(new Event('session-expired'))
    } else if (response.status === 401) {
      window.dispatchEvent(new Event('session-expired'))
    }

    let problem: ProblemDetails = { status: response.status }
    try { problem = await response.json() } catch { /* ignore */ }
    throw new ApiError(response.status, problem)
  }

  // 204 No Content
  if (response.status === 204) return undefined as T

  return response.json() as Promise<T>
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
