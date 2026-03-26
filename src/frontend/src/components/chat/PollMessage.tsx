import { useEffect, useState } from 'react'
import type { PollData } from '../../types'
import { castVote } from '../../services/enrichedMessagingApiService'
import { usePollStore } from '../../stores/pollStore'

interface Props {
  poll: PollData
  voting?: boolean
}

export function PollMessage({ poll: initialPoll, voting = true }: Props) {
  const store = usePollStore()
  const poll = store.polls[initialPoll.pollId] ?? initialPoll
  const [loading, setLoading] = useState(false)

  // Seed the store on mount so real-time PollVoteUpdated broadcasts can find and update this poll
  useEffect(() => {
    if (!store.polls[initialPoll.pollId]) {
      store.setPoll(initialPoll.pollId, initialPoll)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialPoll.pollId])

  const totalVotes = poll.options.reduce((s, o) => s + o.voteCount, 0)

  const handleVote = async (optionId: string) => {
    if (!voting || loading) return
    setLoading(true)
    try {
      const result = await castVote(poll.pollId, optionId)
      // Use setPoll so the UI always updates, even when this poll wasn't previously in the store
      store.setPoll(poll.pollId, {
        ...poll,
        options: result.options,
        currentUserVotedOptionIds: result.currentUserVotedOptionIds,
      })
    } finally {
      setLoading(false)
    }
  }

  const isVoted = (optionId: string) =>
    poll.currentUserVotedOptionIds.includes(optionId)

  return (
    <div className="rounded-lg border border-border bg-card p-3 max-w-sm">
      <p className="font-semibold text-sm mb-3">{poll.question}</p>
      <div className="space-y-2">
        {poll.options.map((opt) => {
          const pct = totalVotes > 0 ? Math.round((opt.voteCount / totalVotes) * 100) : 0
          const voted = isVoted(opt.optionId)
          return (
            <button
              key={opt.optionId}
              disabled={!voting || loading}
              onClick={() => handleVote(opt.optionId)}
              className={`w-full text-left rounded px-3 py-2 text-sm relative overflow-hidden transition-colors
                ${voting ? 'hover:bg-primary/10 cursor-pointer' : 'cursor-default'}
                ${voted ? 'border border-primary font-medium' : 'border border-border'}
              `}
            >
              <div
                className="absolute inset-y-0 left-0 bg-primary/15 transition-all"
                style={{ width: `${pct}%` }}
              />
              <span className="relative flex justify-between">
                <span>{opt.text}</span>
                <span className="text-muted-foreground text-xs">{opt.voteCount} ({pct}%)</span>
              </span>
            </button>
          )
        })}
      </div>
      <p className="text-xs text-muted-foreground mt-2">{totalVotes} vote{totalVotes !== 1 ? 's' : ''} · {poll.voteMode === 'single' ? 'Single choice' : 'Multiple choice'}</p>
    </div>
  )
}
