// Mirrors DTO shapes from contracts/rest-api.md and contracts/realtime-events.md

export interface User {
  id: string
  displayName: string
  avatarUrl: string | null
  profileImageUrl: string | null
}

export type OnboardingStatus = 'not_started' | 'remind_later' | 'dismissed'

export interface UserStatus {
  emoji: string | null
  text: string | null
  color: string | null
}

export interface UserProfile {
  id: string
  displayName: string
  email: string | null
  avatarUrl: string | null
  profileImageUrl: string | null
  cropX: number | null
  cropY: number | null
  cropZoom: number | null
  createdAt: string
  onboardingStatus: OnboardingStatus
  status: UserStatus | null
}

export interface Room {
  id: string
  name: string
  isDm: boolean
  unreadCount: number
  hasMention: boolean
  lastMessagePreview: string | null
  createdAt: string // ISO8601
  // Topic fields (012-topics-overhaul)
  isPrivate: boolean
  ownerId: string | null
  isProtected: boolean
  memberCount: number
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

export interface QuoteData {
  originalMessageId: string | null
  authorDisplayName: string
  contentSnapshot: string
  originalAvailable: boolean
}

export interface PollOption {
  optionId: string
  text: string
  displayOrder: number
  voteCount: number
  voterDisplayNames: string[]
}

export interface PollData {
  pollId: string
  question: string
  voteMode: 'single' | 'multi'
  options: PollOption[]
  currentUserVotedOptionIds: string[]
}

export interface LinkPreviewData {
  url: string
  title: string | null
  description: string | null
  thumbnailUrl: string | null
  isDismissed: boolean
}

export type MessageType = 'text' | 'system' | 'poll'

export interface Message {
  id: string
  roomId: string
  author: User
  content: string
  attachments: Attachment[]
  reactions: Reaction[]
  createdAt: string // ISO8601
  editedAt: string | null
  isSystem?: boolean
  messageType?: MessageType
  quote?: QuoteData
  poll?: PollData
  linkPreview?: LinkPreviewData
}

// Client-side outbox entry (IndexedDB, mirrors data-model.md OutboxMessage)
export interface OutboxMessage {
  clientId: string
  roomId: string
  content: string
  createdAt: number // Date.now()
  status: 'pending' | 'sending' | 'failed'
  quotedMessageId?: string
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

// 012-topics-overhaul: sidebar groups
export interface SidebarGroup {
  id: string
  name: string
  displayOrder: number
  isCollapsed: boolean
  /** Ordered list of room IDs as returned by the API (display order). Do NOT re-sort client-side. */
  roomIds: string[]
}

/** 013-topic-dnd-membership: member info returned by GET /api/rooms/{roomId}/members */
export interface RoomMember {
  userId: string
  displayName: string
  avatarUrl: string | null
  isOwner: boolean
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

export type NotificationType = 'mention' | 'topic_alert' | 'unread_dm' | 'reaction' | 'quote'

// 021-enriched-messaging additional types

export interface Highlight {
  id: string
  roomId: string
  messageId: string
  highlightedByDisplayName: string
  highlightedAt: string
  isDeleted: boolean
  authorDisplayName: string | null
  contentPreview: string | null
  messageCreatedAt: string | null
}

export interface BookmarkFolder {
  id: string
  name: string
  bookmarks: Bookmark[]
}

export interface Bookmark {
  id: string
  messageId: string
  folderId: string | null
  roomId: string
  roomName: string
  authorDisplayName: string
  contentPreview: string
  messageCreatedAt: string
  createdAt: string
  isDeleted: boolean
  placeholderReason: 'message_deleted' | 'room_deleted' | null
}

export interface BookmarksResponse {
  folders: BookmarkFolder[]
  unfiled: Bookmark[]
}

export interface DigestMessage {
  id: string
  author: { id: string | null; displayName: string; avatarUrl: string | null }
  content: string
  messageType: MessageType
  createdAt: string
  quote: QuoteData | null
  poll: PollData | null
}

export interface DigestGroup {
  roomId: string
  roomName: string
  messages: DigestMessage[]
}

export interface DailyDigest {
  date: string
  groups: DigestGroup[]
}

export interface Notification {
  id: string
  recipientUserId: string
  type: NotificationType
  messageId: string | null
  roomId: string
  roomName: string
  fromUserId: string | null
  fromDisplayName: string
  contentPreview: string
  isRead: boolean
  createdAt: string
  expiresAt: string
}
