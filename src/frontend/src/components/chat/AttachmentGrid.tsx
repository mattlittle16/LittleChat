import { useEffect, useState } from 'react'
import { AuthedImg } from './AuthedImg'
import { ImageLightbox } from './ImageLightbox'
import { getAccessToken } from '../../services/apiClient'
import type { Attachment } from '../../types'

interface AttachmentGridProps {
  attachments: Attachment[]
}

async function authedDownload(url: string, fileName: string) {
  const token = getAccessToken()
  const res = await fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!res.ok) return
  const blob = await res.blob()
  const objectUrl = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = objectUrl
  a.download = fileName
  a.click()
  URL.revokeObjectURL(objectUrl)
}

function VideoPlayer({ attachment }: { attachment: Attachment }) {
  const [videoUrl, setVideoUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const token = getAccessToken()
    fetch(`/api/files/video-token/${attachment.attachmentId}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
      .then(res => res.ok ? res.json() : Promise.reject())
      .then((data: { videoUrl: string }) => setVideoUrl(data.videoUrl))
      .catch(() => setFailed(true))
  }, [attachment.attachmentId])

  if (failed) {
    // Fall back to download chip
    return (
      <button
        onClick={() => authedDownload(attachment.url, attachment.fileName)}
        className="inline-flex items-center gap-1 text-xs text-primary hover:underline max-w-full"
      >
        <span>📎</span>
        <span className="truncate">{attachment.fileName}</span>
        <span className="text-muted-foreground flex-shrink-0">
          ({(attachment.fileSize / 1024).toFixed(1)} KB)
        </span>
      </button>
    )
  }

  if (!videoUrl) {
    return (
      <div className="max-w-sm h-12 rounded-md border bg-muted/30 flex items-center justify-center text-xs text-muted-foreground">
        Loading video…
      </div>
    )
  }

  return (
    <video
      src={videoUrl}
      controls
      preload="metadata"
      className="rounded-lg max-h-80 max-w-sm w-full"
    />
  )
}

export function AttachmentGrid({ attachments }: AttachmentGridProps) {
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null)

  if (attachments.length === 0) return null

  const images = attachments.filter(a => a.isImage)
  const videos = attachments.filter(a =>
    !a.isImage && (a.contentType === 'video/mp4' || a.fileName.toLowerCase().endsWith('.mp4'))
  )
  const files = attachments.filter(a =>
    !a.isImage &&
    a.contentType !== 'video/mp4' &&
    !a.fileName.toLowerCase().endsWith('.mp4')
  )

  function openLightbox(img: Attachment) {
    const idx = images.indexOf(img)
    if (idx !== -1) setLightboxIndex(idx)
  }

  return (
    <div className="mt-1 flex flex-col gap-1">
      {/* Image grid */}
      {images.length > 0 && (
        <div className={`grid gap-1 max-w-sm ${images.length === 1 ? 'grid-cols-1' : images.length === 2 ? 'grid-cols-2' : 'grid-cols-3'}`}>
          {images.map(att => (
            <button
              key={att.attachmentId}
              className="flex items-center justify-center overflow-hidden rounded-md border bg-muted/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              onClick={() => openLightbox(att)}
              aria-label={`View ${att.fileName}`}
            >
              <AuthedImg
                src={att.url}
                alt={att.fileName}
                className="max-h-40 max-w-full object-contain"
              />
            </button>
          ))}
        </div>
      )}

      {/* Inline video players */}
      {videos.map(att => (
        <VideoPlayer key={att.attachmentId} attachment={att} />
      ))}

      {/* Non-image, non-video file chips */}
      {files.map(att => (
        <button
          key={att.attachmentId}
          onClick={() => authedDownload(att.url, att.fileName)}
          className="inline-flex items-center gap-1 text-xs text-primary hover:underline max-w-full"
        >
          <span>📎</span>
          <span className="truncate">{att.fileName}</span>
          <span className="text-muted-foreground flex-shrink-0">
            ({(att.fileSize / 1024).toFixed(1)} KB)
          </span>
        </button>
      ))}

      {/* Lightbox */}
      {lightboxIndex !== null && (
        <ImageLightbox
          images={images}
          currentIndex={lightboxIndex}
          onClose={() => setLightboxIndex(null)}
          onNavigate={setLightboxIndex}
        />
      )}
    </div>
  )
}
