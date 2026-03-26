import { X, ExternalLink } from 'lucide-react'
import type { LinkPreviewData } from '../../types'
import { dismissLinkPreview } from '../../services/enrichedMessagingApiService'
import { useLinkPreviewStore } from '../../stores/linkPreviewStore'

interface Props {
  messageId: string
  preview: LinkPreviewData
  isCurrentUserSender: boolean
}

export function LinkPreviewCard({ messageId, preview, isCurrentUserSender }: Props) {
  const { dismissPreview } = useLinkPreviewStore()

  if (preview.isDismissed) return null

  const handleDismiss = async (e: React.MouseEvent) => {
    e.preventDefault()
    e.stopPropagation()
    try {
      await dismissLinkPreview(messageId)
      dismissPreview(messageId)
    } catch { /* ignore */ }
  }

  return (
    <a
      href={preview.url}
      target="_blank"
      rel="noopener noreferrer"
      className="block mt-2 border border-border rounded-lg overflow-hidden hover:bg-muted/30 transition-colors max-w-sm"
    >
      {preview.thumbnailUrl && (
        <img
          src={preview.thumbnailUrl}
          alt=""
          className="w-full h-32 object-cover"
          onError={e => (e.currentTarget.style.display = 'none')}
        />
      )}
      <div className="p-2 relative">
        {preview.title && <p className="text-sm font-medium line-clamp-2">{preview.title}</p>}
        {preview.description && <p className="text-xs text-muted-foreground line-clamp-2 mt-0.5">{preview.description}</p>}
        <p className="text-xs text-primary mt-1 flex items-center gap-1">
          <ExternalLink size={10} /> {new URL(preview.url).hostname}
        </p>
        {isCurrentUserSender && (
          <button
            onClick={handleDismiss}
            className="absolute top-2 right-2 text-muted-foreground hover:text-foreground p-0.5 rounded hover:bg-muted"
          >
            <X size={12} />
          </button>
        )}
      </div>
    </a>
  )
}
