import { useEffect, useRef, useState } from 'react'

const POLL_INTERVAL_MS = 60_000
const BACKOFF_INTERVAL_MS = 5 * 60_000
const MAX_FAILURES_BEFORE_BACKOFF = 3
const COUNTDOWN_SECONDS = 10
const TYPING_DEBOUNCE_MS = 3_000

const IS_DEV = __APP_VERSION__ === 'dev'

export function useUpdateDetection() {
  const [updateAvailable, setUpdateAvailable] = useState(false)
  const [countdown, setCountdown] = useState(COUNTDOWN_SECONDS)
  const [countdownPaused, setCountdownPaused] = useState(false)

  const consecutiveFailures = useRef(0)
  const typingActiveUntil = useRef(0)
  const pollIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const countdownIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  // Debug escape hatch: run `localStorage.setItem('littlechat_debug_update', '1')` in the
  // browser console to immediately trigger the update banner without a real deploy.
  useEffect(() => {
    const id = setInterval(() => {
      if (localStorage.getItem('littlechat_debug_update')) {
        setUpdateAvailable(true)
        clearInterval(id)
      }
    }, 1_000)
    return () => clearInterval(id)
  }, [])

  // Polling — disabled in local dev
  useEffect(() => {
    if (IS_DEV) return

    async function checkForUpdate() {
      try {
        const res = await fetch('/version.json', { cache: 'no-store' })
        if (!res.ok) throw new Error(`HTTP ${res.status}`)
        const data = (await res.json()) as { version: string }
        consecutiveFailures.current = 0

        if (data.version !== __APP_VERSION__) {
          if (pollIntervalRef.current) clearInterval(pollIntervalRef.current)
          setUpdateAvailable(true)
        }
      } catch {
        consecutiveFailures.current += 1
        if (consecutiveFailures.current >= MAX_FAILURES_BEFORE_BACKOFF) {
          if (pollIntervalRef.current) clearInterval(pollIntervalRef.current)
          pollIntervalRef.current = setInterval(() => { void checkForUpdate() }, BACKOFF_INTERVAL_MS)
          consecutiveFailures.current = 0
        }
      }
    }

    pollIntervalRef.current = setInterval(() => { void checkForUpdate() }, POLL_INTERVAL_MS)

    return () => {
      if (pollIntervalRef.current) clearInterval(pollIntervalRef.current)
    }
  }, [])

  // Typing detection — pause countdown while user is actively composing
  useEffect(() => {
    if (!updateAvailable) return

    function onInput() {
      typingActiveUntil.current = Date.now() + TYPING_DEBOUNCE_MS
    }

    document.addEventListener('input', onInput, { capture: true })
    return () => document.removeEventListener('input', onInput, { capture: true })
  }, [updateAvailable])

  // Countdown tick
  useEffect(() => {
    if (!updateAvailable) return

    countdownIntervalRef.current = setInterval(() => {
      const isTyping = Date.now() < typingActiveUntil.current
      setCountdownPaused(isTyping)

      if (!isTyping) {
        setCountdown(prev => {
          if (prev <= 1) {
            window.location.reload()
            return 0
          }
          return prev - 1
        })
      }
    }, 1_000)

    return () => {
      if (countdownIntervalRef.current) clearInterval(countdownIntervalRef.current)
    }
  }, [updateAvailable])

  return { updateAvailable, countdown, countdownPaused }
}
