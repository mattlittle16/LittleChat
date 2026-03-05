import { useNotificationPreferencesStore } from '../../stores/notificationPreferencesStore'
import { playChime } from '../../services/notificationService'
import type { RoomSoundLevel } from '../../types'

interface Props {
  onBack: () => void
}

export function NotificationSettingsPage({ onBack }: Props) {
  const { preferences, savePreferences } = useNotificationPreferencesStore()

  return (
    <div className="flex flex-1 flex-col min-w-0 overflow-y-auto">
      <header className="flex h-12 items-center border-b px-4 flex-shrink-0 gap-3">
        <button
          onClick={onBack}
          className="text-muted-foreground hover:text-foreground transition-colors"
          title="Back to chat"
        >
          ←
        </button>
        <span className="font-semibold text-sm">Notification Settings</span>
      </header>

      <div className="flex flex-col gap-8 p-8 max-w-lg">
        {!preferences ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : (
          <>
            {/* DM notifications */}
            <section>
              <h3 className="mb-3 text-sm font-medium">Direct Messages</h3>
              <div className="flex flex-col gap-2">
                {([
                  ['all_messages', 'All New DMs', 'Play a sound for every new DM'],
                  ['muted', 'Muted', 'No sounds for DMs'],
                ] as const).map(([val, label, desc]) => (
                  <label key={val} className="flex items-start gap-3 cursor-pointer">
                    <input
                      type="radio"
                      name="dmSound"
                      checked={preferences.dmSoundEnabled === (val === 'all_messages')}
                      onChange={() => savePreferences({ dmSoundEnabled: val === 'all_messages' })}
                      className="accent-primary mt-0.5"
                    />
                    <div>
                      <div className="text-sm">{label}</div>
                      <div className="text-xs text-muted-foreground">{desc}</div>
                    </div>
                  </label>
                ))}
              </div>
            </section>

            {/* Room notifications */}
            <section>
              <h3 className="mb-3 text-sm font-medium">Channels</h3>
              <div className="flex flex-col gap-2">
                {([
                  ['all_messages', 'All Messages', 'Play a sound for every message'],
                  ['mentions_only', '@Mentions Only', 'Only play a sound when you are mentioned'],
                  ['muted', 'Muted', 'No sounds for channel messages'],
                ] as [RoomSoundLevel, string, string][]).map(([val, label, desc]) => (
                  <label key={val} className="flex items-start gap-3 cursor-pointer">
                    <input
                      type="radio"
                      name="roomSound"
                      checked={preferences.roomSoundLevel === val}
                      onChange={() => savePreferences({ roomSoundLevel: val })}
                      className="accent-primary mt-0.5"
                    />
                    <div>
                      <div className="text-sm">{label}</div>
                      <div className="text-xs text-muted-foreground">{desc}</div>
                    </div>
                  </label>
                ))}
              </div>
            </section>

            {/* Do Not Disturb */}
            <section>
              <label className="flex items-center justify-between cursor-pointer">
                <div>
                  <div className="text-sm font-medium">Do Not Disturb</div>
                  <div className="text-xs text-muted-foreground mt-0.5">Suppress all sounds and notifications</div>
                </div>
                <input
                  type="checkbox"
                  checked={preferences.dndEnabled}
                  onChange={e => savePreferences({ dndEnabled: e.target.checked })}
                  className="w-4 h-4 accent-primary"
                />
              </label>
            </section>

            {/* Browser notifications */}
            <section>
              {(() => {
                const notifSupported = typeof Notification !== 'undefined'
                const blocked = notifSupported && Notification.permission === 'denied'
                return (
                  <>
                    <label className={`flex items-center justify-between ${blocked ? 'opacity-50' : 'cursor-pointer'}`}>
                      <div>
                        <div className="text-sm font-medium">Browser Notifications</div>
                        <div className="text-xs text-muted-foreground mt-0.5">Desktop popups when the tab is in the background</div>
                      </div>
                      <input
                        type="checkbox"
                        disabled={blocked}
                        checked={preferences.browserNotificationsEnabled}
                        onChange={async (e) => {
                          if (!e.target.checked) {
                            savePreferences({ browserNotificationsEnabled: false })
                            return
                          }
                          const permission = Notification.permission === 'granted'
                            ? 'granted'
                            : await Notification.requestPermission()
                          if (permission === 'granted') {
                            savePreferences({ browserNotificationsEnabled: true })
                          }
                        }}
                        className="w-4 h-4 accent-primary"
                      />
                    </label>
                    {blocked && (
                      <p className="mt-2 text-xs text-destructive">
                        Notifications are blocked in your browser — click the lock icon in the address bar to allow them.
                      </p>
                    )}
                  </>
                )
              })()}
            </section>

            {/* Preview sound */}
            <section>
              <button
                onClick={playChime}
                className="rounded border px-4 py-2 text-sm hover:bg-muted/60 transition-colors"
              >
                Preview Sound
              </button>
            </section>
          </>
        )}
      </div>
    </div>
  )
}
