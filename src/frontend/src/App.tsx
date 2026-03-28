import { useEffect, useState } from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { AuthCallbackPage } from './pages/AuthCallbackPage'
import { AdminLayout } from './components/admin/AdminLayout'
import { ChatLayout } from './components/layout/ChatLayout'
import { LandingPage } from './components/LandingPage'
import { SessionExpiredModal } from './components/SessionExpiredModal'
import { UpdateBanner } from './components/UpdateBanner'
import { isAuthenticated, isRefreshInFlight, restoreSession } from './services/authService'
import { useAdminAuth } from './hooks/useAdminAuth'
import { useFaviconBadge } from './hooks/useFaviconBadge'
import { useUpdateDetection } from './hooks/useUpdateDetection'
import { ErrorBoundary } from './components/common/ErrorBoundary'

export default function App() {
  useFaviconBadge()
  return <AuthenticatedApp />
}

function AuthenticatedApp() {
  const [authenticated] = useState<boolean>(() => restoreSession() || isAuthenticated())
  const [sessionExpired, setSessionExpired] = useState(false)
  const { updateAvailable, countdown, countdownPaused } = useUpdateDetection()
  const { isAdmin } = useAdminAuth()

  useEffect(() => {
    const handler = () => setSessionExpired(true)
    window.addEventListener('session-expired', handler)
    return () => window.removeEventListener('session-expired', handler)
  }, [])

  useEffect(() => {
    const handler = () => setSessionExpired(false)
    window.addEventListener('session-restored', handler)
    return () => window.removeEventListener('session-restored', handler)
  }, [])

  useEffect(() => {
    if (!authenticated) return
    const interval = setInterval(() => {
      if (!isAuthenticated() && !isRefreshInFlight()) setSessionExpired(true)
    }, 5_000)
    return () => clearInterval(interval)
  }, [authenticated])

  return (
    <>
      {updateAvailable && <UpdateBanner countdown={countdown} countdownPaused={countdownPaused} />}
      <Routes>
        <Route path="/auth/callback" element={<AuthCallbackPage />} />
        <Route
          path="/admin/*"
          element={
            authenticated && isAdmin
              ? <ErrorBoundary name="Admin"><AdminLayout /></ErrorBoundary>
              : <Navigate to="/" replace />
          }
        />
        <Route
          path="/*"
          element={
            authenticated
              ? <ErrorBoundary name="Chat"><ChatLayout /></ErrorBoundary>
              : <LandingPage />
          }
        />
      </Routes>
      {sessionExpired && <SessionExpiredModal />}
    </>
  )
}
