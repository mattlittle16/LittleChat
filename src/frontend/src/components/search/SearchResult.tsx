import type { SearchResultDto } from '../../types'

interface SearchResultProps {
  result: SearchResultDto
  query: string
  isGlobal: boolean
  onClick: () => void
}

function highlightKeyword(text: string, keyword: string): React.ReactNode {
  if (!keyword.trim()) return text
  const regex = new RegExp(`(${keyword.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi')
  const parts = text.split(regex)
  return parts.map((part, i) =>
    regex.test(part)
      ? <mark key={i} className="bg-yellow-200 dark:bg-yellow-800 rounded-sm px-0.5">{part}</mark>
      : part
  )
}

function formatRelativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime()
  const minutes = Math.floor(diff / 60_000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

// Truncate content to ~200 chars for the excerpt
function excerpt(content: string, maxLen = 200): string {
  if (content.length <= maxLen) return content
  return content.slice(0, maxLen).trimEnd() + '…'
}

export function SearchResult({ result, query, isGlobal, onClick }: SearchResultProps) {
  return (
    <button
      onClick={onClick}
      className="w-full text-left px-4 py-3 hover:bg-muted/60 border-b last:border-b-0 transition-colors"
    >
      <div className="flex items-center justify-between gap-2 mb-1">
        <span className="text-sm font-semibold truncate">{result.authorDisplayName}</span>
        <div className="flex items-center gap-2 flex-shrink-0">
          {isGlobal && (
            <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
              #{result.roomName}
            </span>
          )}
          <span className="text-xs text-muted-foreground">{formatRelativeTime(result.createdAt)}</span>
        </div>
      </div>
      <p className="text-sm text-muted-foreground leading-snug">
        {highlightKeyword(excerpt(result.content), query)}
      </p>
    </button>
  )
}
