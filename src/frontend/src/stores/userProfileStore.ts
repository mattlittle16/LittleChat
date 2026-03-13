import { create } from 'zustand'

interface ProfileEntry {
  displayName: string
  profileImageUrl: string | null
}

interface State {
  profiles: Record<string, ProfileEntry>
  setProfile: (userId: string, data: ProfileEntry) => void
  updateUser: (userId: string, partial: Partial<ProfileEntry>) => void
}

export const useUserProfileStore = create<State>((set, get) => ({
  profiles: {},

  setProfile: (userId, data) =>
    set(state => ({ profiles: { ...state.profiles, [userId]: data } })),

  updateUser: (userId, partial) => {
    const existing = get().profiles[userId]
    if (!existing) return
    set(state => ({
      profiles: {
        ...state.profiles,
        [userId]: { ...existing, ...partial },
      },
    }))
  },
}))
