import * as signalR from '@microsoft/signalr'
import { getAccessToken } from './apiClient'
import { useOutboxStore } from '../stores/outboxStore'

let _connection: signalR.HubConnection | null = null

function buildConnection(roomId: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`/hubs/chat?roomId=${encodeURIComponent(roomId)}`, {
      accessTokenFactory: () => getAccessToken() ?? '',
    })
    .withAutomaticReconnect([0, 2000, 10000, 30000])
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
  })

  _connection.onclose(() => {
    onClose()
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
