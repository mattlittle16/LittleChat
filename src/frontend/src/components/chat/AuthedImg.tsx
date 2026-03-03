import { useEffect, useState } from 'react'
import { getAccessToken } from '../../services/apiClient'

interface AuthedImgProps {
  src: string
  alt: string
  className?: string
}

/**
 * Fetches an authenticated API image URL and renders it via a blob URL.
 * Necessary because <img src> doesn't send Authorization headers.
 */
export function AuthedImg({ src, alt, className }: AuthedImgProps) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null)

  useEffect(() => {
    let objectUrl: string | null = null
    const token = getAccessToken()

    fetch(src, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
      .then(res => {
        if (!res.ok) throw new Error(`${res.status}`)
        return res.blob()
      })
      .then(blob => {
        objectUrl = URL.createObjectURL(blob)
        setBlobUrl(objectUrl)
      })
      .catch(() => {
        // leave blobUrl null — broken image stays hidden
      })

    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [src])

  if (!blobUrl) return null

  return (
    <a href={blobUrl} target="_blank" rel="noreferrer" className="mt-1 block">
      <img src={blobUrl} alt={alt} className={className} />
    </a>
  )
}
