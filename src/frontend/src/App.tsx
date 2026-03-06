import { useEffect, useState } from 'react'
import { AuthCallbackPage } from './pages/AuthCallbackPage'
import { ChatLayout } from './components/layout/ChatLayout'
import { LandingPage } from './components/LandingPage'
import { SessionExpiredModal } from './components/SessionExpiredModal'
import { isAuthenticated, restoreSession } from './services/authService'
import { useTheme } from './hooks/useTheme'

export default function App() {
  useTheme()
  const path = window.location.pathname

  // Handle OIDC callback before anything else
  if (path === '/auth/callback') {
    return <AuthCallbackPage />
  }

  return <AuthenticatedApp />
}

function AuthenticatedApp() {
  // Lazy initializer: restoreSession() and isAuthenticated() are synchronous localStorage
  // reads, so we can compute the initial value without a useEffect.
  const [authenticated] = useState<boolean>(() => restoreSession() || isAuthenticated())
  const [sessionExpired, setSessionExpired] = useState(false)

  useEffect(() => {
    const handler = () => setSessionExpired(true)
    window.addEventListener('session-expired', handler)
    return () => window.removeEventListener('session-expired', handler)
  }, [])

  return (
    <>
      {authenticated ? <ChatLayout /> : <LandingPage />}
      {sessionExpired && <SessionExpiredModal />}
    </>
  )
}
