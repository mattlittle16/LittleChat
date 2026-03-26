import { X } from 'lucide-react'

interface PendingQuote {
  authorDisplayName: string
  contentSnapshot: string
}

interface Props {
  quote: PendingQuote | null
  onDismiss: () => void
}

export function QuoteComposer({ quote, onDismiss }: Props) {
  if (!quote) return null

  return (
    <div className="flex items-center gap-2 px-3 py-1.5 bg-muted/50 border-t border-border text-sm">
      <div className="flex-1 min-w-0">
        <span className="font-semibold text-foreground/80 mr-1">{quote.authorDisplayName}:</span>
        <span className="text-muted-foreground truncate">{quote.contentSnapshot.slice(0, 100)}</span>
      </div>
      <button onClick={onDismiss} className="text-muted-foreground hover:text-foreground flex-shrink-0">
        <X size={14} />
      </button>
    </div>
  )
}
