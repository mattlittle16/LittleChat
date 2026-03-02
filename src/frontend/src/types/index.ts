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
  createdAt: string // ISO8601
}

export interface Attachment {
  fileName: string
  fileSize: number
  url: string // "/api/files/{messageId}"
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
  attachment: Attachment | null
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

// Pagination cursor for message history
export interface MessagePage {
  messages: Message[]
  hasMore: boolean
  nextCursor: string | null
}
