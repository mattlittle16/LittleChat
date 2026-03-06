import { useEffect } from 'react'
import { useRoomStore } from '../stores/roomStore'

const FAVICON_PATH = '/favicon.svg'
let cachedImg: HTMLImageElement | null = null

function loadFaviconImage(): Promise<HTMLImageElement> {
  if (cachedImg) return Promise.resolve(cachedImg)
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => { cachedImg = img; resolve(img) }
    img.onerror = reject
    img.src = FAVICON_PATH
  })
}

function getFaviconLink(): HTMLLinkElement {
  let link = document.querySelector<HTMLLinkElement>('link[rel~="icon"]')
  if (!link) {
    link = document.createElement('link')
    link.rel = 'icon'
    document.head.appendChild(link)
  }
  return link
}

async function setFaviconBadge(count: number, signal: { cancelled: boolean }) {
  // Draw at 2x then let the browser scale down — keeps text crisp
  const size = 64
  const canvas = document.createElement('canvas')
  canvas.width = size
  canvas.height = size
  const ctx = canvas.getContext('2d')!

  try {
    const img = await loadFaviconImage()
    if (signal.cancelled) return
    // Draw icon at 75% size top-left, leaving bottom-right for the badge
    ctx.drawImage(img, 0, 0, size * 0.75, size * 0.75)
  } catch {
    if (signal.cancelled) return
  }

  const label = count > 99 ? '99+' : String(count)
  const radius = 21
  const badgeX = size - radius - 2
  const badgeY = size - radius - 2

  // White ring so badge pops off the icon
  ctx.beginPath()
  ctx.arc(badgeX, badgeY, radius + 2, 0, Math.PI * 2)
  ctx.fillStyle = '#ffffff'
  ctx.fill()

  ctx.beginPath()
  ctx.arc(badgeX, badgeY, radius, 0, Math.PI * 2)
  ctx.fillStyle = count > 1 ? '#e53e3e' : '#6b7280'
  ctx.fill()

  ctx.fillStyle = '#ffffff'
  ctx.font = `bold ${label.length > 2 ? 16 : 23}px sans-serif`
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  ctx.fillText(label, badgeX, badgeY + 1)

  getFaviconLink().href = canvas.toDataURL('image/png')
}


export function useFaviconBadge() {
  const totalUnread = useRoomStore(s =>
    s.rooms.reduce((sum, r) => sum + (r.unreadCount ?? 0), 0)
  )

  useEffect(() => {
    const signal = { cancelled: false }
    setFaviconBadge(totalUnread, signal)
    return () => { signal.cancelled = true }
  }, [totalUnread])
}
