import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useNotificationPreferencesStore } from '../stores/notificationPreferencesStore'
import type { NotificationPreferences } from '../types'

vi.mock('../services/apiClient', () => ({
  api: {
    get: vi.fn(),
    put: vi.fn().mockResolvedValue(undefined),
    delete: vi.fn().mockResolvedValue(undefined),
  },
}))

import { api } from '../services/apiClient'
const mockApi = api as unknown as {
  get: ReturnType<typeof vi.fn>
  put: ReturnType<typeof vi.fn>
  delete: ReturnType<typeof vi.fn>
}

const DEFAULT_PREFS: NotificationPreferences = {
  dmSoundEnabled: true,
  roomSoundLevel: 'mentions_only',
  dndEnabled: false,
  browserNotificationsEnabled: false,
}

beforeEach(() => {
  useNotificationPreferencesStore.setState({ preferences: null, overrides: {} })
  vi.clearAllMocks()
})

describe('effectiveLevelForRoom', () => {
  it('returns muted when DND is enabled regardless of overrides', () => {
    useNotificationPreferencesStore.setState({
      preferences: { ...DEFAULT_PREFS, dndEnabled: true },
      overrides: { 'r1': 'all_messages' },
    })
    expect(useNotificationPreferencesStore.getState().effectiveLevelForRoom('r1', false)).toBe('muted')
  })

  it('returns the override level when one is set', () => {
    useNotificationPreferencesStore.setState({
      preferences: { ...DEFAULT_PREFS, roomSoundLevel: 'muted' },
      overrides: { 'r1': 'all_messages' },
    })
    expect(useNotificationPreferencesStore.getState().effectiveLevelForRoom('r1', false)).toBe('all_messages')
  })

  it('returns all_messages for a DM when dmSoundEnabled is true', () => {
    useNotificationPreferencesStore.setState({
      preferences: { ...DEFAULT_PREFS, dmSoundEnabled: true },
      overrides: {},
    })
    expect(useNotificationPreferencesStore.getState().effectiveLevelForRoom('dm-1', true)).toBe('all_messages')
  })

  it('returns muted for a DM when dmSoundEnabled is false', () => {
    useNotificationPreferencesStore.setState({
      preferences: { ...DEFAULT_PREFS, dmSoundEnabled: false },
      overrides: {},
    })
    expect(useNotificationPreferencesStore.getState().effectiveLevelForRoom('dm-1', true)).toBe('muted')
  })

  it('returns the global roomSoundLevel for a topic with no override', () => {
    useNotificationPreferencesStore.setState({
      preferences: { ...DEFAULT_PREFS, roomSoundLevel: 'all_messages' },
      overrides: {},
    })
    expect(useNotificationPreferencesStore.getState().effectiveLevelForRoom('r1', false)).toBe('all_messages')
  })

  it('falls back to defaults when preferences are null', () => {
    useNotificationPreferencesStore.setState({ preferences: null, overrides: {} })
    // Default roomSoundLevel is 'mentions_only'
    expect(useNotificationPreferencesStore.getState().effectiveLevelForRoom('r1', false)).toBe('mentions_only')
  })
})

describe('setOverride', () => {
  it('sets an override level for a room', async () => {
    await useNotificationPreferencesStore.getState().setOverride('r1', 'muted')
    expect(useNotificationPreferencesStore.getState().overrides['r1']).toBe('muted')
    expect(mockApi.put).toHaveBeenCalledWith('/api/notifications/overrides/r1', { level: 'muted' })
  })

  it('removes the override when level is follow_global', async () => {
    useNotificationPreferencesStore.setState({ preferences: null, overrides: { 'r1': 'muted' } })
    await useNotificationPreferencesStore.getState().setOverride('r1', 'follow_global')
    expect(useNotificationPreferencesStore.getState().overrides['r1']).toBeUndefined()
    expect(mockApi.delete).toHaveBeenCalledWith('/api/notifications/overrides/r1')
  })
})

describe('savePreferences', () => {
  it('applies an optimistic update before the API call resolves', () => {
    useNotificationPreferencesStore.setState({ preferences: DEFAULT_PREFS, overrides: {} })
    // Don't await — check state synchronously after the call starts
    useNotificationPreferencesStore.getState().savePreferences({ dndEnabled: true })
    // State should be updated immediately (optimistically)
    expect(useNotificationPreferencesStore.getState().preferences?.dndEnabled).toBe(true)
  })

  it('merges partial update, preserving unchanged fields', async () => {
    useNotificationPreferencesStore.setState({ preferences: DEFAULT_PREFS, overrides: {} })
    await useNotificationPreferencesStore.getState().savePreferences({ dmSoundEnabled: false })
    const prefs = useNotificationPreferencesStore.getState().preferences!
    expect(prefs.dmSoundEnabled).toBe(false)
    expect(prefs.roomSoundLevel).toBe('mentions_only') // unchanged
  })

  it('initialises from defaults when preferences are null', async () => {
    await useNotificationPreferencesStore.getState().savePreferences({ dndEnabled: true })
    expect(useNotificationPreferencesStore.getState().preferences?.dndEnabled).toBe(true)
    expect(useNotificationPreferencesStore.getState().preferences?.dmSoundEnabled).toBe(true) // from default
  })
})
