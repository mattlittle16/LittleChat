import { create } from 'zustand'
import type { OnboardingStatus } from '../types'

interface State {
  id: string | null
  onboardingStatus: OnboardingStatus | null
  setId: (id: string) => void
  setOnboardingStatus: (status: OnboardingStatus) => void
}

export const useCurrentUserStore = create<State>((set) => ({
  id: null,
  onboardingStatus: null,
  setId: (id) => set({ id }),
  setOnboardingStatus: (onboardingStatus) => set({ onboardingStatus }),
}))
