import { create } from 'zustand'
import { api } from '../services/apiClient'
import type { NotificationPreferences, ConversationOverrideLevel, RoomSoundLevel } from '../types'

interface NotificationPreferencesState {
  preferences: NotificationPreferences | null
  overrides: Record<string, ConversationOverrideLevel>
  loadPreferences: () => Promise<void>
  loadOverrides: () => Promise<void>
  savePreferences: (prefs: Partial<NotificationPreferences>) => Promise<void>
  setOverride: (roomId: string, level: ConversationOverrideLevel | 'follow_global') => Promise<void>
  effectiveLevelForRoom: (roomId: string, isDm: boolean) => RoomSoundLevel
}

const DEFAULT_PREFERENCES: NotificationPreferences = {
  dmSoundEnabled: true,
  roomSoundLevel: 'mentions_only',
  dndEnabled: false,
  browserNotificationsEnabled: false,
}

export const useNotificationPreferencesStore = create<NotificationPreferencesState>((set, get) => ({
  preferences: null,
  overrides: {},

  loadPreferences: async () => {
    try {
      const prefs = await api.get<NotificationPreferences>('/api/notifications/preferences')
      set({ preferences: prefs })
    } catch {
      set({ preferences: DEFAULT_PREFERENCES })
    }
  },

  loadOverrides: async () => {
    const list = await api.get<Array<{ roomId: string; level: ConversationOverrideLevel }>>('/api/notifications/overrides')
    const map: Record<string, ConversationOverrideLevel> = {}
    for (const o of list) map[o.roomId] = o.level
    set({ overrides: map })
  },

  savePreferences: async (prefs) => {
    // Optimistic update
    set(s => ({
      preferences: s.preferences
        ? { ...s.preferences, ...prefs }
        : { ...DEFAULT_PREFERENCES, ...prefs },
    }))
    await api.put<void>('/api/notifications/preferences', prefs)
  },

  setOverride: async (roomId, level) => {
    if (level === 'follow_global') {
      set(s => {
        const next = { ...s.overrides }
        delete next[roomId]
        return { overrides: next }
      })
      await api.delete(`/api/notifications/overrides/${roomId}`)
    } else {
      set(s => ({ overrides: { ...s.overrides, [roomId]: level } }))
      await api.put<void>(`/api/notifications/overrides/${roomId}`, { level })
    }
  },

  effectiveLevelForRoom: (roomId, isDm) => {
    const { preferences, overrides } = get()
    const prefs = preferences ?? DEFAULT_PREFERENCES
    if (prefs.dndEnabled) return 'muted'
    const override = overrides[roomId]
    if (override) return override
    if (isDm) return prefs.dmSoundEnabled ? 'all_messages' : 'muted'
    return prefs.roomSoundLevel
  },
}))
