import { useEffect, useRef, useState } from 'react'

interface AvatarCropEditorProps {
  imageFile: File
  onConfirm: (croppedBlob: Blob) => void
  onCancel: () => void
}

const CIRCLE_SIZE = 240
const OUTPUT_SIZE = 512

export function AvatarCropEditor({ imageFile, onConfirm, onCancel }: AvatarCropEditorProps) {
  const [imageUrl, setImageUrl] = useState<string | null>(null)
  const [offsetX, setOffsetX] = useState(0)
  const [offsetY, setOffsetY] = useState(0)
  const [zoom, setZoom] = useState(1)

  const dragging = useRef(false)
  const lastPos = useRef({ x: 0, y: 0 })
  const imgRef = useRef<HTMLImageElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const minZoomRef = useRef(1)

  useEffect(() => {
    const url = URL.createObjectURL(imageFile)
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setImageUrl(url)
    return () => {
      URL.revokeObjectURL(url)
    }
  }, [imageFile])

  function handleImageLoad() {
    const img = imgRef.current
    if (!img) return
    // Fit the shorter dimension to the circle so the full image is visible at minimum zoom
    const fitZoom = CIRCLE_SIZE / Math.min(img.naturalWidth, img.naturalHeight)
    minZoomRef.current = fitZoom
    setZoom(fitZoom)
    setOffsetX(0)
    setOffsetY(0)
  }

  function clamp(offset: number, imgSize: number): number {
    const halfCircle = CIRCLE_SIZE / 2
    const scaledHalf = (imgSize * zoom) / 2
    const max = scaledHalf - halfCircle
    return Math.max(-max, Math.min(max, offset))
  }

  function handlePointerDown(e: React.PointerEvent) {
    dragging.current = true
    lastPos.current = { x: e.clientX, y: e.clientY }
    ;(e.target as HTMLElement).setPointerCapture(e.pointerId)
  }

  function handlePointerMove(e: React.PointerEvent) {
    if (!dragging.current) return
    const dx = e.clientX - lastPos.current.x
    const dy = e.clientY - lastPos.current.y
    lastPos.current = { x: e.clientX, y: e.clientY }

    const natW = imgRef.current?.naturalWidth ?? CIRCLE_SIZE
    const natH = imgRef.current?.naturalHeight ?? CIRCLE_SIZE

    setOffsetX(prev => clamp(prev + dx, natW))
    setOffsetY(prev => clamp(prev + dy, natH))
  }

  function handlePointerUp() {
    dragging.current = false
  }

  function handleWheel(e: WheelEvent) {
    e.preventDefault()
    setZoom(prev => {
      const next = prev - e.deltaY * 0.001
      return Math.max(minZoomRef.current, Math.min(5, next))
    })
  }

  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    el.addEventListener('wheel', handleWheel, { passive: false })
    return () => el.removeEventListener('wheel', handleWheel)
  }, [])

  function handleConfirm() {
    const img = imgRef.current
    if (!img) return

    const natW = img.naturalWidth
    const natH = img.naturalHeight

    // Center of the crop circle in natural image coordinates
    const cropCX = natW / 2 - offsetX / zoom
    const cropCY = natH / 2 - offsetY / zoom

    // Size of the crop region in natural image coordinates
    const srcSize = CIRCLE_SIZE / zoom
    const srcX = cropCX - srcSize / 2
    const srcY = cropCY - srcSize / 2

    const canvas = document.createElement('canvas')
    canvas.width = OUTPUT_SIZE
    canvas.height = OUTPUT_SIZE
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    ctx.drawImage(img, srcX, srcY, srcSize, srcSize, 0, 0, OUTPUT_SIZE, OUTPUT_SIZE)

    canvas.toBlob(blob => {
      if (blob) onConfirm(blob)
    }, 'image/jpeg', 0.92)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70">
      <div
        className="rounded-xl bg-background p-6 flex flex-col items-center gap-4 shadow-xl"
        style={{ minWidth: 320 }}
      >
        <h3 className="text-sm font-semibold">Adjust Photo</h3>

        {/* Circular crop container */}
        <div
          ref={containerRef}
          style={{
            width: CIRCLE_SIZE,
            height: CIRCLE_SIZE,
            borderRadius: '50%',
            overflow: 'hidden',
            position: 'relative',
            cursor: 'grab',
            background: '#111',
            userSelect: 'none',
          }}
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          onPointerLeave={handlePointerUp}
        >
          {imageUrl && (
            <img
              ref={imgRef}
              src={imageUrl}
              alt="Crop preview"
              draggable={false}
              onLoad={handleImageLoad}
              style={{
                position: 'absolute',
                top: '50%',
                left: '50%',
                transform: `translate(calc(-50% + ${offsetX}px), calc(-50% + ${offsetY}px)) scale(${zoom})`,
                transformOrigin: 'center',
                maxWidth: 'none',
                pointerEvents: 'none',
              }}
            />
          )}
        </div>

        <p className="text-xs text-muted-foreground">Drag to reposition · Scroll to zoom</p>

        <div className="flex gap-2">
          <button
            onClick={handleConfirm}
            className="rounded bg-primary px-4 py-1.5 text-sm text-primary-foreground hover:opacity-90"
          >
            Apply
          </button>
          <button
            onClick={onCancel}
            className="rounded border px-4 py-1.5 text-sm hover:bg-muted/60"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  )
}
