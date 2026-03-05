import { useNotificationPreferencesStore } from '../stores/notificationPreferencesStore'
import { useRoomStore } from '../stores/roomStore'

let lastChimeAt = 0
let audioEl: HTMLAudioElement | null = null

async function getAudio(): Promise<HTMLAudioElement | null> {
  if (audioEl) return audioEl
  try {
    const mod = await import('../assets/notification.mp3')
    audioEl = new Audio(mod.default as string)
    return audioEl
  } catch {
    return null
  }
}

export function playChime(): void {
  const now = Date.now()
  if (now - lastChimeAt < 3000) return
  lastChimeAt = now
  getAudio().then(audio => {
    if (!audio) return
    audio.currentTime = 0
    audio.play().catch(() => {
      // Browser may block autoplay before user interaction — silently ignore
    })
  })
}

export function showBrowserNotification(
  senderName: string,
  body: string,
  roomId: string
): void {
  const { preferences } = useNotificationPreferencesStore.getState()
  if (!preferences?.browserNotificationsEnabled) return
  if (typeof Notification === 'undefined') return
  if (Notification.permission !== 'granted') return

  const truncated = body.length > 100 ? body.slice(0, 100) + '\u2026' : body
  const notification = new Notification(senderName, { body: truncated })
  notification.onclick = () => {
    window.focus()
    useRoomStore.getState().setActiveRoom(roomId)
    notification.close()
  }
}

export { lastChimeAt }
