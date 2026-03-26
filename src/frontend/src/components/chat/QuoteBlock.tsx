import type { QuoteData } from '../../types'

interface Props {
  quote: QuoteData
  onJumpTo?: (messageId: string) => void
}

export function QuoteBlock({ quote, onJumpTo }: Props) {
  const canJump = quote.originalAvailable && !!onJumpTo && !!quote.originalMessageId

  const handleClick = () => {
    if (canJump && quote.originalMessageId) onJumpTo!(quote.originalMessageId)
  }

  return (
    <div
      className={`border-l-4 border-primary/60 bg-muted/40 rounded-r px-2 py-1 mb-1 text-sm ${canJump ? 'cursor-pointer hover:bg-muted/60' : ''}`}
      onClick={canJump ? handleClick : undefined}
    >
      <p className="font-semibold text-foreground/80 text-xs mb-0.5">{quote.authorDisplayName}</p>
      {quote.originalAvailable ? (
        <p className="text-muted-foreground line-clamp-2">{quote.contentSnapshot}</p>
      ) : (
        <p className="text-muted-foreground italic">Original message no longer available</p>
      )}
    </div>
  )
}
