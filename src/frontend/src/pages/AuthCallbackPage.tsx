import { useEffect } from 'react'
import { storeToken } from '../services/authService'

export function AuthCallbackPage() {
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    const token = params.get('access_token')

    if (token) {
      storeToken(token)
      // Redirect to main app, removing the token from the URL
      window.location.replace('/')
    } else {
      // No token in URL — redirect to login
      window.location.replace('/auth/login')
    }
  }, [])

  return (
    <div className="flex min-h-screen items-center justify-center">
      <p className="text-muted-foreground">Signing in…</p>
    </div>
  )
}
