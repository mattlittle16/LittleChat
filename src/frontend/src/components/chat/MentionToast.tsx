import { create } from 'zustand'
import { useRoomStore } from '../../stores/roomStore'

interface ToastData {
  id: string
  roomId: string
  roomName: string
  fromDisplayName: string
  contentPreview: string
}

interface MentionToastState {
  toasts: ToastData[]
  push: (toast: Omit<ToastData, 'id'>) => void
  dismiss: (id: string) => void
}

// eslint-disable-next-line react-refresh/only-export-components
export const useMentionToastStore = create<MentionToastState>((set) => ({
  toasts: [],
  push: (toast) => {
    const id = crypto.randomUUID()
    set(s => ({ toasts: [...s.toasts, { ...toast, id }] }))
    // Auto-dismiss after 5s
    setTimeout(() => set(s => ({ toasts: s.toasts.filter(t => t.id !== id) })), 5000)
  },
  dismiss: (id) => set(s => ({ toasts: s.toasts.filter(t => t.id !== id) })),
}))

export function MentionToastContainer() {
  const { toasts, dismiss } = useMentionToastStore()
  const setActiveRoom = useRoomStore(s => s.setActiveRoom)

  if (toasts.length === 0) return null

  return (
    <div className="fixed bottom-4 right-4 flex flex-col gap-2 z-50">
      {toasts.map(toast => (
        <div
          key={toast.id}
          className="flex items-start gap-3 rounded-lg border bg-background shadow-lg px-4 py-3 w-80 animate-in slide-in-from-bottom-2"
        >
          <span className="text-lg leading-none mt-0.5">@</span>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold truncate">
              {toast.fromDisplayName} mentioned you in #{toast.roomName || 'a room'}
            </p>
            <p className="text-xs text-muted-foreground truncate mt-0.5">{toast.contentPreview}</p>
            <button
              onClick={() => { setActiveRoom(toast.roomId); dismiss(toast.id) }}
              className="mt-1 text-xs text-primary hover:underline"
            >
              Go to message →
            </button>
          </div>
          <button
            onClick={() => dismiss(toast.id)}
            className="text-muted-foreground hover:text-foreground text-xs flex-shrink-0"
            aria-label="Dismiss"
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  )
}

/** Call from useSignalR to push a mention toast. */
// eslint-disable-next-line react-refresh/only-export-components
export function showMentionToast(data: Omit<ToastData, 'id'>) {
  useMentionToastStore.getState().push(data)
}
