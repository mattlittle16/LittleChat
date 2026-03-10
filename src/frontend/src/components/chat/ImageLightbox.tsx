import { useEffect, useCallback } from 'react'
import { AuthedImg } from './AuthedImg'
import type { Attachment } from '../../types'

interface ImageLightboxProps {
  images: Attachment[]
  currentIndex: number
  onClose: () => void
  onNavigate: (index: number) => void
}

export function ImageLightbox({ images, currentIndex, onClose, onNavigate }: ImageLightboxProps) {
  const current = images[currentIndex]
  const hasPrev = currentIndex > 0
  const hasNext = currentIndex < images.length - 1

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    if (e.key === 'Escape') onClose()
    if (e.key === 'ArrowLeft' && hasPrev) onNavigate(currentIndex - 1)
    if (e.key === 'ArrowRight' && hasNext) onNavigate(currentIndex + 1)
  }, [currentIndex, hasPrev, hasNext, onClose, onNavigate])

  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [handleKeyDown])

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/80"
      onClick={onClose}
    >
      {/* Prev button */}
      {hasPrev && (
        <button
          className="absolute left-4 top-1/2 -translate-y-1/2 rounded-full bg-white/10 hover:bg-white/25 p-3 text-white text-xl leading-none"
          onClick={e => { e.stopPropagation(); onNavigate(currentIndex - 1) }}
          aria-label="Previous image"
        >
          ‹
        </button>
      )}

      {/* Image */}
      <div
        className="max-w-[90vw] max-h-[90vh] flex items-center justify-center"
        onClick={e => e.stopPropagation()}
      >
        <AuthedImg
          src={current.url}
          alt={current.fileName}
          className="object-contain max-w-[90vw] max-h-[90vh] rounded shadow-xl"
        />
      </div>

      {/* Next button */}
      {hasNext && (
        <button
          className="absolute right-4 top-1/2 -translate-y-1/2 rounded-full bg-white/10 hover:bg-white/25 p-3 text-white text-xl leading-none"
          onClick={e => { e.stopPropagation(); onNavigate(currentIndex + 1) }}
          aria-label="Next image"
        >
          ›
        </button>
      )}

      {/* Close button */}
      <button
        className="absolute top-4 right-4 rounded-full bg-white/10 hover:bg-white/25 p-2 text-white leading-none"
        onClick={onClose}
        aria-label="Close"
      >
        ✕
      </button>

      {/* Counter */}
      {images.length > 1 && (
        <div className="absolute bottom-4 left-1/2 -translate-x-1/2 text-white/70 text-sm">
          {currentIndex + 1} / {images.length}
        </div>
      )}
    </div>
  )
}
