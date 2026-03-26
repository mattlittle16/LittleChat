import { api } from './apiClient'
import type {
  PollOption,
  BookmarksResponse,
  BookmarkFolder,
  Bookmark,
  DailyDigest,
  Highlight,
  UserStatus,
} from '../types'

// ── Polls ─────────────────────────────────────────────────────────────────────

export interface CreatePollRequest {
  roomId: string
  question: string
  options: string[]
  voteMode: 'single' | 'multi'
}

export interface CreatePollResponse {
  messageId: string
  pollId: string
}

export interface VoteResponse {
  pollId: string
  options: PollOption[]
  currentUserVotedOptionIds: string[]
}

export const createPoll = (req: CreatePollRequest): Promise<CreatePollResponse> =>
  api.post('/api/polls', req)

export const castVote = (pollId: string, optionId: string): Promise<VoteResponse> =>
  api.post(`/api/polls/${pollId}/vote`, { optionId })

export const getPoll = (pollId: string): Promise<VoteResponse> =>
  api.get(`/api/polls/${pollId}`)

// ── Highlights ────────────────────────────────────────────────────────────────

export const getHighlights = (roomId: string): Promise<Highlight[]> =>
  api.get(`/api/rooms/${roomId}/highlights`)

export const addHighlight = (roomId: string, messageId: string): Promise<Highlight> =>
  api.post(`/api/rooms/${roomId}/highlights`, { messageId })

export const removeHighlight = (roomId: string, highlightId: string): Promise<void> =>
  api.delete(`/api/rooms/${roomId}/highlights/${highlightId}`)

// ── Bookmarks ─────────────────────────────────────────────────────────────────

export const getBookmarks = (): Promise<BookmarksResponse> =>
  api.get('/api/bookmarks')

export const addBookmark = (messageId: string, folderId: string | null): Promise<Bookmark> =>
  api.post('/api/bookmarks', { messageId, folderId })

export const removeBookmark = (bookmarkId: string): Promise<void> =>
  api.delete(`/api/bookmarks/${bookmarkId}`)

export const moveBookmark = (bookmarkId: string, folderId: string | null): Promise<void> =>
  api.patch(`/api/bookmarks/${bookmarkId}`, { folderId })

export const createBookmarkFolder = (name: string): Promise<BookmarkFolder> =>
  api.post('/api/bookmark-folders', { name })

export const deleteBookmarkFolder = (folderId: string): Promise<void> =>
  api.delete(`/api/bookmark-folders/${folderId}`)

// ── User Status ───────────────────────────────────────────────────────────────

export const setStatus = (status: UserStatus): Promise<UserStatus> =>
  api.put('/api/users/me/status', status)

export const clearStatus = (): Promise<void> =>
  api.delete('/api/users/me/status')

// ── Daily Digest ──────────────────────────────────────────────────────────────

export const getDigest = (): Promise<DailyDigest> =>
  api.get('/api/digest')

// ── Link Preview ──────────────────────────────────────────────────────────────

export const dismissLinkPreview = (messageId: string): Promise<void> =>
  api.delete(`/api/messages/${messageId}/link-preview`)
