export function SessionExpiredModal() {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="bg-card border border-border rounded-xl shadow-2xl p-8 max-w-sm w-full mx-4 text-center">
        <h2 className="text-xl font-semibold text-foreground mb-3">Session Expired</h2>
        <p className="text-muted-foreground mb-6">
          Your session has expired. Please log in again to continue.
        </p>
        <a
          href="/auth/login"
          className="inline-block w-full py-2 px-4 rounded-lg bg-primary text-primary-foreground font-medium hover:bg-primary/90 transition-colors"
        >
          Log in again
        </a>
      </div>
    </div>
  )
}
