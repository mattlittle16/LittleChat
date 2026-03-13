import { api, getAccessToken } from './apiClient'
import type { UserProfile } from '../types'

export function getMyProfile(): Promise<UserProfile> {
  return api.get<UserProfile>('/api/users/me')
}

export async function updateDisplayName(name: string): Promise<void> {
  await api.put('/api/users/me', { displayName: name })
}

export async function uploadAvatar(
  file: File,
  cropX: number,
  cropY: number,
  cropZoom: number,
): Promise<{ profileImageUrl: string }> {
  const token = getAccessToken()
  const formData = new FormData()
  formData.append('file', file)
  formData.append('cropX', String(cropX))
  formData.append('cropY', String(cropY))
  formData.append('cropZoom', String(cropZoom))

  const response = await fetch('/api/users/me/avatar', {
    method: 'PUT',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    body: formData,
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new Error(problem?.error ?? `Upload failed: HTTP ${response.status}`)
  }

  return response.json()
}

export async function deleteAvatar(): Promise<void> {
  await api.delete('/api/users/me/avatar')
}
