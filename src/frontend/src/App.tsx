import { useEffect, useState } from 'react'
import { AuthCallbackPage } from './pages/AuthCallbackPage'
import { isAuthenticated, login, restoreSession } from './services/authService'

// Placeholder — replaced in Phase 4 (US2) with the real chat layout
function ChatLayout() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <p className="text-lg font-medium">Welcome to LittleChat</p>
    </div>
  )
}

function LandingPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4">
      <h1 className="text-3xl font-bold">LittleChat</h1>
      <p className="text-muted-foreground">A private chat for your group.</p>
      <button
        onClick={login}
        className="rounded-md bg-primary px-6 py-2 text-primary-foreground hover:opacity-90"
      >
        Sign In
      </button>
    </div>
  )
}

export default function App() {
  const path = window.location.pathname

  // Handle OIDC callback before anything else
  if (path === '/auth/callback') {
    return <AuthCallbackPage />
  }

  return <AuthenticatedApp />
}

function AuthenticatedApp() {
  const [authenticated, setAuthenticated] = useState<boolean | null>(null)

  useEffect(() => {
    // Restore token from localStorage on load (survives browser close/reopen per US1)
    const hasSession = restoreSession()
    setAuthenticated(hasSession || isAuthenticated())
  }, [])

  if (authenticated === null) {
    // Brief flash while we check localStorage — render nothing
    return null
  }

  return authenticated ? <ChatLayout /> : <LandingPage />
}
