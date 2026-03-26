import { create } from 'zustand'
import type { PollData, PollOption } from '../types'

interface PollState {
  polls: Record<string, PollData> // keyed by pollId
  setPoll: (pollId: string, poll: PollData) => void
  updateVotes: (pollId: string, options: PollOption[], currentUserVotedOptionIds: string[]) => void
  updateOptionCounts: (pollId: string, options: PollOption[]) => void
  clearVote: (pollId: string) => void
}

export const usePollStore = create<PollState>((set) => ({
  polls: {},
  setPoll: (pollId, poll) =>
    set((s) => ({ polls: { ...s.polls, [pollId]: poll } })),
  updateVotes: (pollId, options, currentUserVotedOptionIds) =>
    set((s) => {
      const existing = s.polls[pollId]
      if (!existing) return s
      return {
        polls: {
          ...s.polls,
          [pollId]: { ...existing, options, currentUserVotedOptionIds },
        },
      }
    }),
  updateOptionCounts: (pollId, options) =>
    set((s) => {
      const existing = s.polls[pollId]
      if (!existing) return s
      return { polls: { ...s.polls, [pollId]: { ...existing, options } } }
    }),
  clearVote: (pollId) =>
    set((s) => {
      const existing = s.polls[pollId]
      if (!existing) return s
      return {
        polls: {
          ...s.polls,
          [pollId]: { ...existing, currentUserVotedOptionIds: [] },
        },
      }
    }),
}))
