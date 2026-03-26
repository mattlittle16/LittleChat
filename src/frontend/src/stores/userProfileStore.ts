import { create } from 'zustand'
import { api } from '../services/apiClient'
import type { UserStatus } from '../types'

interface ProfileEntry {
  displayName: string
  profileImageUrl: string | null
  status?: UserStatus | null
}

interface State {
  profiles: Record<string, ProfileEntry>
  usersFetchedAt: number
  setProfile: (userId: string, data: ProfileEntry) => void
  updateUser: (userId: string, partial: Partial<ProfileEntry>) => void
  fetchAllUsers: (forceRefresh?: boolean) => Promise<void>
}

// In-flight promise shared across all concurrent callers — prevents duplicate requests
// when multiple components call fetchAllUsers() before the first one resolves.
let fetchInFlight: Promise<void> | null = null

export const useUserProfileStore = create<State>((set, get) => ({
  profiles: {},
  usersFetchedAt: 0,

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

  fetchAllUsers: (forceRefresh = false) => {
    if (!forceRefresh && Date.now() - get().usersFetchedAt < 60_000) return Promise.resolve()
    if (fetchInFlight) return fetchInFlight
    fetchInFlight = api
      .get<Array<{ id: string; displayName: string; profileImageUrl: string | null; status?: UserStatus | null }>>('/api/users')
      .then(users => {
        set(state => {
          const updated = { ...state.profiles }
          users.forEach(u => { updated[u.id] = { displayName: u.displayName, profileImageUrl: u.profileImageUrl, status: u.status ?? null } })
          return { profiles: updated, usersFetchedAt: Date.now() }
        })
      })
      .catch(() => { /* ignore — stale profiles are acceptable */ })
      .finally(() => { fetchInFlight = null })
    return fetchInFlight
  },
}))
