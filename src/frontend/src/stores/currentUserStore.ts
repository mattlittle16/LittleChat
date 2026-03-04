import { create } from 'zustand'

interface State {
  id: string | null
  setId: (id: string) => void
}

export const useCurrentUserStore = create<State>((set) => ({
  id: null,
  setId: (id) => set({ id }),
}))
