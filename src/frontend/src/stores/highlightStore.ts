import { create } from 'zustand'
import type { Highlight } from '../types'

interface HighlightState {
  highlights: Record<string, Highlight[]> // keyed by roomId
  setHighlights: (roomId: string, highlights: Highlight[]) => void
  addHighlight: (roomId: string, highlight: Highlight) => void
  removeHighlight: (roomId: string, highlightId: string) => void
}

export const useHighlightStore = create<HighlightState>((set) => ({
  highlights: {},
  setHighlights: (roomId, highlights) =>
    set((s) => ({ highlights: { ...s.highlights, [roomId]: highlights } })),
  addHighlight: (roomId, highlight) =>
    set((s) => ({
      highlights: {
        ...s.highlights,
        [roomId]: [highlight, ...(s.highlights[roomId] ?? [])].filter(
          (h, i, arr) => arr.findIndex(x => x.id === h.id) === i
        ),
      },
    })),
  removeHighlight: (roomId, highlightId) =>
    set((s) => ({
      highlights: {
        ...s.highlights,
        [roomId]: (s.highlights[roomId] ?? []).filter(h => h.id !== highlightId),
      },
    })),
}))
