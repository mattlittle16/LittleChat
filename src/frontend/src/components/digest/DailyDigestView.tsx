import { useEffect, useState } from 'react'
import { BookOpen, ChevronDown, ChevronRight } from 'lucide-react'
import { getDigest } from '../../services/enrichedMessagingApiService'
import type { DailyDigest, DigestGroup } from '../../types'
import { PollMessage } from '../chat/PollMessage'
import { QuoteBlock } from '../chat/QuoteBlock'

interface Props {
  onNavigate: (roomId: string, messageId: string) => void
}

export function DailyDigestView({ onNavigate }: Props) {
  const [digest, setDigest] = useState<DailyDigest | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getDigest().then(setDigest).catch(() => setDigest({ date: '', groups: [] })).finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="flex items-center justify-center h-full text-muted-foreground text-sm p-8">Loading digest…</div>

  if (!digest || digest.groups.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center h-full text-muted-foreground p-8 text-center">
        <BookOpen size={32} className="mb-3 opacity-30" />
        <p className="text-sm">Nothing to catch up on from yesterday.</p>
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full">
      <div className="px-3 py-2 border-b border-border">
        <h2 className="font-semibold text-sm flex items-center gap-2">
          <BookOpen size={16} /> Daily Digest — {digest.date}
        </h2>
      </div>
      <div className="flex-1 overflow-y-auto p-2 space-y-3">
        {digest.groups.map(g => <DigestRoomGroup key={g.roomId} group={g} onNavigate={onNavigate} />)}
      </div>
    </div>
  )
}

function DigestRoomGroup({ group, onNavigate }: { group: DigestGroup; onNavigate: (roomId: string, messageId: string) => void }) {
  const [open, setOpen] = useState(true)

  return (
    <div className="border border-border rounded-lg overflow-hidden">
      <button
        className="w-full flex items-center gap-2 px-3 py-2 bg-muted/30 hover:bg-muted/50 text-sm font-medium"
        onClick={() => setOpen(o => !o)}
      >
        {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        {group.roomName}
        <span className="text-muted-foreground font-normal ml-auto">{group.messages.length} message{group.messages.length !== 1 ? 's' : ''}</span>
      </button>
      {open && (
        <div className="divide-y divide-border">
          {group.messages.map(m => (
            <div key={m.id} className="px-3 py-2 hover:bg-muted/20 cursor-pointer" onClick={() => onNavigate(group.roomId, m.id)}>
              <div className="flex items-baseline gap-2 mb-1">
                <span className="text-xs font-medium">{m.author.displayName}</span>
                <span className="text-xs text-muted-foreground">{new Date(m.createdAt).toLocaleTimeString()}</span>
              </div>
              {m.quote && <QuoteBlock quote={m.quote} />}
              {m.messageType === 'poll' && m.poll ? (
                <PollMessage poll={m.poll} voting={false} />
              ) : (
                <p className="text-sm text-foreground">{m.content}</p>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
