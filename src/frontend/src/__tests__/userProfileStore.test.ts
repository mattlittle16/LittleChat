import { describe, it, expect, beforeEach, vi } from 'vitest'

vi.mock('../services/apiClient', () => ({
  api: {
    get: vi.fn(),
  },
}))

import { api } from '../services/apiClient'
const mockApi = api as { get: ReturnType<typeof vi.fn> }

// Import store after mock is in place
import { useUserProfileStore } from '../stores/userProfileStore'

const USERS = [
  { id: 'u1', displayName: 'Alice', profileImageUrl: null },
  { id: 'u2', displayName: 'Bob', profileImageUrl: '/img/bob.png' },
]

beforeEach(() => {
  useUserProfileStore.setState({ profiles: {}, usersFetchedAt: 0 })
  vi.clearAllMocks()
  // Ensure the module-level fetchInFlight is null by awaiting any resolved mocks
})

describe('setProfile', () => {
  it('stores a profile entry', () => {
    useUserProfileStore.getState().setProfile('u1', { displayName: 'Alice', profileImageUrl: null })
    expect(useUserProfileStore.getState().profiles['u1']).toEqual({ displayName: 'Alice', profileImageUrl: null })
  })
})

describe('updateUser', () => {
  it('merges a partial update into the existing profile', () => {
    useUserProfileStore.getState().setProfile('u1', { displayName: 'Alice', profileImageUrl: null })
    useUserProfileStore.getState().updateUser('u1', { profileImageUrl: '/new.png' })
    expect(useUserProfileStore.getState().profiles['u1']).toEqual({
      displayName: 'Alice',
      profileImageUrl: '/new.png',
    })
  })

  it('is a no-op for unknown user ids', () => {
    useUserProfileStore.getState().updateUser('unknown', { displayName: 'Ghost' })
    expect(useUserProfileStore.getState().profiles['unknown']).toBeUndefined()
  })
})

describe('fetchAllUsers', () => {
  it('fetches users and stores profiles on first call', async () => {
    mockApi.get.mockResolvedValueOnce(USERS)
    await useUserProfileStore.getState().fetchAllUsers()
    const { profiles } = useUserProfileStore.getState()
    expect(profiles['u1']).toEqual({ displayName: 'Alice', profileImageUrl: null })
    expect(profiles['u2']).toEqual({ displayName: 'Bob', profileImageUrl: '/img/bob.png' })
    expect(mockApi.get).toHaveBeenCalledWith('/api/users')
  })

  it('skips the API call when cache is fresh (< 60s)', async () => {
    useUserProfileStore.setState({ profiles: {}, usersFetchedAt: Date.now() - 1000 })
    await useUserProfileStore.getState().fetchAllUsers()
    expect(mockApi.get).not.toHaveBeenCalled()
  })

  it('fetches again when cache is stale (> 60s)', async () => {
    mockApi.get.mockResolvedValueOnce(USERS)
    useUserProfileStore.setState({ profiles: {}, usersFetchedAt: Date.now() - 61_000 })
    await useUserProfileStore.getState().fetchAllUsers()
    expect(mockApi.get).toHaveBeenCalled()
  })

  it('fetches again when forceRefresh is true, bypassing the cache', async () => {
    mockApi.get.mockResolvedValueOnce(USERS)
    useUserProfileStore.setState({ profiles: {}, usersFetchedAt: Date.now() - 1000 })
    await useUserProfileStore.getState().fetchAllUsers(true)
    expect(mockApi.get).toHaveBeenCalled()
  })

  it('does not make a second API call when a fetch is already in flight', async () => {
    let resolve!: (users: typeof USERS) => void
    const deferred = new Promise<typeof USERS>(r => { resolve = r })
    mockApi.get.mockReturnValueOnce(deferred)

    // Start two concurrent calls
    const p1 = useUserProfileStore.getState().fetchAllUsers()
    const p2 = useUserProfileStore.getState().fetchAllUsers()

    resolve(USERS)
    await Promise.all([p1, p2])

    expect(mockApi.get).toHaveBeenCalledTimes(1)
  })

  it('silently ignores API errors and leaves profiles unchanged', async () => {
    mockApi.get.mockRejectedValueOnce(new Error('network error'))
    useUserProfileStore.setState({ profiles: { 'u1': { displayName: 'Old Alice', profileImageUrl: null } }, usersFetchedAt: 0 })
    await useUserProfileStore.getState().fetchAllUsers()
    // Stale profiles remain
    expect(useUserProfileStore.getState().profiles['u1']?.displayName).toBe('Old Alice')
  })
})
