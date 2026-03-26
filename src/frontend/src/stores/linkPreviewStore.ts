import { create } from 'zustand'
import type { LinkPreviewData } from '../types'

interface LinkPreviewState {
  previews: Record<string, LinkPreviewData> // keyed by messageId
  setPreview: (messageId: string, preview: LinkPreviewData) => void
  dismissPreview: (messageId: string) => void
}

export const useLinkPreviewStore = create<LinkPreviewState>((set) => ({
  previews: {},
  setPreview: (messageId, preview) =>
    set((s) => ({ previews: { ...s.previews, [messageId]: preview } })),
  dismissPreview: (messageId) =>
    set((s) => ({
      previews: {
        ...s.previews,
        [messageId]: { ...(s.previews[messageId] ?? {}), isDismissed: true } as LinkPreviewData,
      },
    })),
}))
