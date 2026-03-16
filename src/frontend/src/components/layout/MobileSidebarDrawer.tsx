import { useEffect, useRef } from 'react'
import { X } from 'lucide-react'
import { Sidebar } from './Sidebar'

interface Props {
  open: boolean
  onClose: () => void
}

export function MobileSidebarDrawer({ open, onClose }: Props) {
  const backdropRef = useRef<HTMLDivElement>(null)

  // Close on Escape
  useEffect(() => {
    if (!open) return
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-40 flex md:hidden">
      {/* Backdrop */}
      <div
        ref={backdropRef}
        className="absolute inset-0 bg-black/50"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer panel */}
      <div className="relative flex w-72 max-w-[85vw] flex-col bg-background shadow-xl">
        {/* Close button */}
        <div className="flex items-center justify-end px-3 py-2 border-b">
          <button
            onClick={onClose}
            className="rounded p-1 hover:bg-muted/60 text-foreground/70 hover:text-foreground transition-colors"
            aria-label="Close sidebar"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Sidebar content fills the drawer */}
        <div className="flex-1 overflow-hidden">
          <Sidebar onNavigate={onClose} />
        </div>
      </div>
    </div>
  )
}
