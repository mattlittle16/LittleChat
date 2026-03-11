import { getAccessToken } from './apiClient'

export interface GifSearchResult {
  id: string
  title: string
  previewUrl: string
  gifUrl: string
}

export async function searchGifs(query: string, limit = 20): Promise<GifSearchResult[] | null> {
  const token = getAccessToken()
  const params = new URLSearchParams({ q: query, limit: String(limit) })

  try {
    const response = await fetch(`/api/gif/search?${params}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })

    if (!response.ok) return null

    const data = await response.json() as GifSearchResult[]
    return data
  } catch {
    return null
  }
}
