import { api } from './apiClient'

export interface AdminUser {
  id: string
  displayName: string
  avatarUrl: string | null
  bannedUntil: string | null
}

export interface AdminTopic {
  id: string
  name: string
  memberCount: number
}

export interface AdminTopicMember {
  id: string
  displayName: string
  avatarUrl: string | null
}

export interface AdminTopicMembersResult {
  topicId: string
  topicName: string
  members: AdminTopicMember[]
}

export interface AuditLogEntry {
  id: number
  adminId: string
  adminName: string
  action: string
  targetId: string | null
  targetName: string | null
  occurredAt: string
}

export interface PaginatedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ForceLogoutResult {
  userId: string
  displayName: string
  message: string
}

export function getUsers(params: { q?: string; page?: number; pageSize?: number } = {}): Promise<PaginatedResult<AdminUser>> {
  const query = new URLSearchParams()
  if (params.q) query.set('q', params.q)
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  return api.get<PaginatedResult<AdminUser>>(`/api/admin/users${qs ? `?${qs}` : ''}`)
}

export function forceLogout(userId: string, banDurationHours: number): Promise<ForceLogoutResult> {
  return api.post<ForceLogoutResult>(`/api/admin/users/${userId}/force-logout`, { banDurationHours })
}

export function unbanUser(userId: string): Promise<{ userId: string; displayName: string; message: string }> {
  return api.post(`/api/admin/users/${userId}/unban`, {})
}

export function getTopics(params: { q?: string; page?: number; pageSize?: number } = {}): Promise<PaginatedResult<AdminTopic>> {
  const query = new URLSearchParams()
  if (params.q) query.set('q', params.q)
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  return api.get<PaginatedResult<AdminTopic>>(`/api/admin/topics${qs ? `?${qs}` : ''}`)
}

export function getTopicMembers(topicId: string): Promise<AdminTopicMembersResult> {
  return api.get<AdminTopicMembersResult>(`/api/admin/topics/${topicId}/members`)
}

export function addTopicMember(topicId: string, userId: string): Promise<{ userId: string; displayName: string }> {
  return api.post(`/api/admin/topics/${topicId}/members`, { userId })
}

export function removeTopicMember(topicId: string, userId: string): Promise<{ userId: string; displayName: string }> {
  return api.delete(`/api/admin/topics/${topicId}/members/${userId}`)
}

export function createTopic(name: string): Promise<{ topicId: string; name: string }> {
  return api.post('/api/admin/topics', { name })
}

export function deleteTopic(topicId: string): Promise<{ topicId: string; name: string }> {
  return api.delete(`/api/admin/topics/${topicId}`)
}

export function getAuditLog(params: { from?: string; to?: string; page?: number; pageSize?: number } = {}): Promise<PaginatedResult<AuditLogEntry>> {
  const query = new URLSearchParams()
  if (params.from) query.set('from', params.from)
  if (params.to) query.set('to', params.to)
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))
  const qs = query.toString()
  return api.get<PaginatedResult<AuditLogEntry>>(`/api/admin/audit-log${qs ? `?${qs}` : ''}`)
}
