import * as signalR from '@microsoft/signalr'
import { getAccessToken } from './apiClient'
import { useOutboxStore } from '../stores/outboxStore'
import { useUserProfileStore } from '../stores/userProfileStore'

let _connection: signalR.HubConnection | null = null

function buildConnection(roomId: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`/hubs/chat?roomId=${encodeURIComponent(roomId)}`, {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect({
      // Retry indefinitely: 0s, 2s, 10s, then 30s forever.
      // An array would give up after the last entry — an object never returns null so
      // SignalR keeps trying until the connection is explicitly stopped.
      nextRetryDelayInMilliseconds: (ctx) => {
        if (ctx.previousRetryCount === 0) return 0
        if (ctx.previousRetryCount === 1) return 2_000
        if (ctx.previousRetryCount === 2) return 10_000
        return 30_000
      },
    })
    .configureLogging(signalR.LogLevel.Warning)
    .build()
}

export function getConnection(): signalR.HubConnection | null {
  return _connection
}

export async function startConnection(
  roomId: string,
  onReconnecting: () => void,
  onReconnected: () => void,
  onClose: () => void,
): Promise<signalR.HubConnection> {
  if (_connection) {
    await _connection.stop()
  }

  _connection = buildConnection(roomId)

  _connection.onreconnecting(() => {
    onReconnecting()
  })

  _connection.onreconnected(() => {
    onReconnected()
    // Drain any messages that queued up while disconnected (Constitution Principle V)
    useOutboxStore.getState().drainOutbox()
    // Fix stale presence refcounts after a server crash — forces refcount back to 1
    // so the next disconnect correctly broadcasts offline. Safe no-op on a healthy reconnect.
    _connection?.invoke('ReassertPresence').catch(() => {})
  })

  _connection.onclose(() => {
    onClose()
  })

  _connection.on('UserProfileUpdated', ({ userId, displayName, profileImageUrl }: { userId: string; displayName: string; profileImageUrl: string | null }) => {
    useUserProfileStore.getState().updateUser(userId, { displayName, profileImageUrl })
  })

  await _connection.start()
  return _connection
}

export async function stopConnection(): Promise<void> {
  if (_connection) {
    await _connection.stop()
    _connection = null
  }
}
