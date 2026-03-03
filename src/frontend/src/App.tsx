import { useState } from 'react'
import { AuthCallbackPage } from './pages/AuthCallbackPage'
import { ChatLayout } from './components/layout/ChatLayout'
import { isAuthenticated, login, restoreSession } from './services/authService'
import { useTheme } from './hooks/useTheme'

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

  return authenticated ? <ChatLayout /> : <LandingPage />
}
