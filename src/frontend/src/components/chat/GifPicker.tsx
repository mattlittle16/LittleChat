import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { searchGifs, type GifSearchResult } from '../../services/gifService'

interface GifPickerProps {
  searchTerm: string
  onSelect: (gifUrl: string) => void
  onDismiss: () => void
}

export function GifPicker({ searchTerm, onSelect, onDismiss }: GifPickerProps) {
  const [results, setResults] = useState<GifSearchResult[] | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!searchTerm.trim()) return
    let cancelled = false
    setLoading(true)
    setError(false)
    searchGifs(searchTerm).then(data => {
      if (cancelled) return
      setLoading(false)
      if (data === null) {
        setError(true)
      } else {
        setResults(data)
      }
    })
    return () => { cancelled = true }
  }, [searchTerm])

  // Dismiss on Escape
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onDismiss()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [onDismiss])

  // Dismiss on click-outside
  useEffect(() => {
    function onMouseDown(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        onDismiss()
      }
    }
    document.addEventListener('mousedown', onMouseDown)
    return () => document.removeEventListener('mousedown', onMouseDown)
  }, [onDismiss])

  return createPortal(
    <div
      ref={containerRef}
      style={{
        position: 'fixed',
        bottom: '80px',
        left: '50%',
        transform: 'translateX(-50%)',
        zIndex: 9999,
        width: '480px',
        maxWidth: 'calc(100vw - 32px)',
      }}
      className="rounded-lg border bg-background shadow-xl overflow-hidden"
    >
      <div className="flex items-center justify-between px-3 py-2 border-b">
        <span className="text-sm font-semibold text-muted-foreground">
          GIFs for &ldquo;{searchTerm}&rdquo;
        </span>
        <button
          onClick={onDismiss}
          className="text-muted-foreground hover:text-foreground text-sm px-1"
          aria-label="Close GIF picker"
        >
          ✕
        </button>
      </div>

      <div className="p-2 max-h-64 overflow-y-auto">
        {loading && (
          <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
            Searching…
          </div>
        )}

        {error && (
          <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
            GIF search is unavailable, try again later
          </div>
        )}

        {!loading && !error && results !== null && results.length === 0 && (
          <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
            No GIFs found
          </div>
        )}

        {!loading && !error && results && results.length > 0 && (
          <div className="grid grid-cols-3 gap-1.5">
            {results.map(gif => (
              <button
                key={gif.id}
                onClick={() => onSelect(gif.gifUrl)}
                className="rounded overflow-hidden aspect-video bg-muted hover:ring-2 hover:ring-primary transition-all"
                title={gif.title}
              >
                <img
                  src={gif.previewUrl}
                  alt={gif.title}
                  className="w-full h-full object-cover"
                  loading="lazy"
                />
              </button>
            ))}
          </div>
        )}
      </div>
    </div>,
    document.body
  )
}
