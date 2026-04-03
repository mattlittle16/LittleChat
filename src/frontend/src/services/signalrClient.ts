import * as signalR from '@microsoft/signalr'
import { getAccessToken } from './apiClient'
import { clearSession } from './authService'
import { useOutboxStore } from '../stores/outboxStore'
import { useUserProfileStore } from '../stores/userProfileStore'
import { useRoomStore } from '../stores/roomStore'
import { usePollStore } from '../stores/pollStore'
import { useHighlightStore } from '../stores/highlightStore'
import { useLinkPreviewStore } from '../stores/linkPreviewStore'
import type { PollOption, LinkPreviewData } from '../types'

let _connection: signalR.HubConnection | null = null

function buildConnection(roomId: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`/hubs/chat?roomId=${encodeURIComponent(roomId)}`, {
      // Force WebSockets only — prevents silent fallback to long polling, which makes
      // continuous HTTP requests and causes significant CPU usage on some Windows networks.
      transport: signalR.HttpTransportType.WebSockets,
      skipNegotiation: true,
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
    _connection?.invoke('ReassertPresence').catch(err => console.error('[SignalR] ReassertPresence failed', err))
  })

  _connection.onclose(() => {
    onClose()
  })

  _connection.off('UserProfileUpdated')
  _connection.on('UserProfileUpdated', ({ userId, displayName, profileImageUrl }: { userId: string; displayName: string; profileImageUrl: string | null }) => {
    useUserProfileStore.getState().updateUser(userId, { displayName, profileImageUrl })
    useRoomStore.getState().updateOtherUserDisplayName(userId, displayName)
  })

  _connection.off('ForceLogout')
  _connection.on('ForceLogout', () => {
    clearSession()
    window.location.href = '/'
  })

  _connection.off('PollVoteUpdated')
  _connection.on('PollVoteUpdated', ({ pollId, options }: { pollId: string; options: PollOption[]; currentUserVotedOptionIds: string[] }) => {
    // Only update vote counts — each user's own currentUserVotedOptionIds is managed locally via handleVote
    usePollStore.getState().updateOptionCounts(pollId, options)
  })

  _connection.off('HighlightChanged')
  _connection.on('HighlightChanged', ({ action, highlightId, roomId, messageId, highlightedByDisplayName, highlightedAt, contentPreview, authorDisplayName }: { action: string; highlightId: string; roomId: string; messageId: string; highlightedByDisplayName: string; highlightedAt: string; contentPreview?: string | null; authorDisplayName?: string | null }) => {
    if (action === 'removed') {
      useHighlightStore.getState().removeHighlight(roomId, highlightId)
    } else {
      useHighlightStore.getState().addHighlight(roomId, {
        id: highlightId,
        roomId,
        messageId,
        highlightedByDisplayName,
        highlightedAt,
        isDeleted: false,
        authorDisplayName: authorDisplayName ?? null,
        contentPreview: contentPreview ?? null,
        messageCreatedAt: null,
      })
    }
  })

  _connection.off('UserStatusUpdated')
  _connection.on('UserStatusUpdated', ({ userId, emoji, text, color }: { userId: string; emoji: string | null; text: string | null; color: string | null }) => {
    const profiles = useUserProfileStore.getState().profiles
    if (profiles[userId]) {
      const status = emoji == null && text == null && color == null ? null : { emoji, text, color }
      useUserProfileStore.getState().setProfile(userId, { ...profiles[userId], status })
    }
  })

  _connection.off('LinkPreviewUpdated')
  _connection.on('LinkPreviewUpdated', ({ messageId, isDismissed, url, title, description, thumbnailUrl }: { messageId: string; isDismissed: boolean; url: string; title: string | null; description: string | null; thumbnailUrl: string | null }) => {
    const preview: LinkPreviewData = { url, title, description, thumbnailUrl, isDismissed }
    if (isDismissed) {
      useLinkPreviewStore.getState().dismissPreview(messageId)
    } else {
      useLinkPreviewStore.getState().setPreview(messageId, preview)
    }
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
