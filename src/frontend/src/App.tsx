import { useEffect, useState } from 'react'
import { AuthCallbackPage } from './pages/AuthCallbackPage'
import { AdminLayout } from './components/admin/AdminLayout'
import { ChatLayout } from './components/layout/ChatLayout'
import { LandingPage } from './components/LandingPage'
import { SessionExpiredModal } from './components/SessionExpiredModal'
import { UpdateBanner } from './components/UpdateBanner'
import { isAuthenticated, restoreSession } from './services/authService'
import { useAdminAuth } from './hooks/useAdminAuth'
import { useFaviconBadge } from './hooks/useFaviconBadge'
import { useUpdateDetection } from './hooks/useUpdateDetection'

export default function App() {
  useFaviconBadge()
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
  const { updateAvailable, countdown, countdownPaused } = useUpdateDetection()
  const { isAdmin } = useAdminAuth()
  const path = window.location.pathname

  useEffect(() => {
    const handler = () => setSessionExpired(true)
    window.addEventListener('session-expired', handler)
    return () => window.removeEventListener('session-expired', handler)
  }, [])

  // Proactive check: catch expiry when the user is idle (no API calls being made)
  useEffect(() => {
    if (!authenticated) return
    const interval = setInterval(() => {
      if (!isAuthenticated()) setSessionExpired(true)
    }, 5_000)
    return () => clearInterval(interval)
  }, [authenticated])

  // If navigating to /admin but not authenticated or not admin, redirect to /
  useEffect(() => {
    if (path.startsWith('/admin') && (!authenticated || !isAdmin)) {
      window.location.href = '/'
    }
  }, [path, authenticated, isAdmin])

  function renderMain() {
    if (!authenticated) return <LandingPage />
    if (path.startsWith('/admin')) {
      if (!isAdmin) return null // redirect handled above
      return <AdminLayout />
    }
    return <ChatLayout />
  }

  return (
    <>
      {updateAvailable && <UpdateBanner countdown={countdown} countdownPaused={countdownPaused} />}
      {renderMain()}
      {sessionExpired && <SessionExpiredModal />}
    </>
  )
}
