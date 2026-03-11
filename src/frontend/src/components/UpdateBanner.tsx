interface UpdateBannerProps {
  countdown: number
  countdownPaused: boolean
}

export function UpdateBanner({ countdown, countdownPaused }: UpdateBannerProps) {
  return (
    <div className="fixed top-0 left-0 right-0 z-50 flex items-center justify-between gap-4 bg-primary px-4 py-2 text-primary-foreground shadow-md">
      <span className="text-sm font-medium">
        {countdownPaused
          ? 'Update available — paused while you type'
          : `Update available. Reloading in ${countdown}s…`}
      </span>
      <button
        onClick={() => window.location.reload()}
        className="shrink-0 rounded-md bg-primary-foreground px-3 py-1 text-sm font-medium text-primary hover:opacity-90 transition-opacity"
      >
        Reload now
      </button>
    </div>
  )
}
