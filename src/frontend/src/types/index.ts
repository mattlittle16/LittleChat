// Mirrors DTO shapes from contracts/rest-api.md and contracts/realtime-events.md

export interface User {
  id: string
  displayName: string
  avatarUrl: string | null
}

export interface Room {
  id: string
  name: string
  isDm: boolean
  unreadCount: number
  hasMention: boolean
  lastMessagePreview: string | null
  createdAt: string // ISO8601
  // DM-only fields
  otherUserId: string | null
  otherUserDisplayName: string | null
  otherUserAvatarUrl: string | null
}

export interface UserSearchResult {
  id: string
  displayName: string
  avatarUrl: string | null
  isOnline: boolean
}

export interface Attachment {
  attachmentId: string
  fileName: string
  fileSize: number
  contentType: string
  isImage: boolean
  url: string // "/api/files/attachments/{attachmentId}"
}

export interface Reaction {
  emoji: string
  count: number
  users: string[] // display names
}

export interface Message {
  id: string
  roomId: string
  author: User
  content: string
  attachments: Attachment[]
  reactions: Reaction[]
  createdAt: string // ISO8601
  editedAt: string | null
}

// Client-side outbox entry (IndexedDB, mirrors data-model.md OutboxMessage)
export interface OutboxMessage {
  clientId: string
  roomId: string
  content: string
  createdAt: number // Date.now()
  status: 'pending' | 'sending' | 'failed'
}

// Search result shape from GET /api/search
export interface SearchResultDto {
  messageId: string
  roomId: string
  roomName: string
  authorDisplayName: string
  content: string
  createdAt: string // ISO8601
}

// Pagination cursor for message history
export interface MessagePage {
  messages: Message[]
  hasMore: boolean
  nextCursor: string | null
}

// Notification preferences types (005-notification-settings)
export type RoomSoundLevel = 'all_messages' | 'mentions_only' | 'muted'
export type ConversationOverrideLevel = 'all_messages' | 'mentions_only' | 'muted'

export interface NotificationPreferences {
  dmSoundEnabled: boolean
  roomSoundLevel: RoomSoundLevel
  dndEnabled: boolean
  browserNotificationsEnabled: boolean
}

export interface ConversationOverride {
  roomId: string
  level: ConversationOverrideLevel
}
