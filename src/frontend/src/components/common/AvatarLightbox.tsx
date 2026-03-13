import { createPortal } from 'react-dom'
import { AuthedImg } from '../chat/AuthedImg'

interface AvatarLightboxProps {
  src: string
  alt: string
  authed: boolean
  onClose: () => void
}

export function AvatarLightbox({ src, alt, authed, onClose }: AvatarLightboxProps) {
  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/80"
      onClick={onClose}
    >
      <div onClick={e => e.stopPropagation()}>
        {authed ? (
          <AuthedImg
            src={src}
            alt={alt}
            style={{ maxWidth: '80vw', maxHeight: '80vh', borderRadius: 8, display: 'block' }}
          />
        ) : (
          <img
            src={src}
            alt={alt}
            style={{ maxWidth: '80vw', maxHeight: '80vh', borderRadius: 8, display: 'block' }}
          />
        )}
      </div>
    </div>,
    document.body,
  )
}
