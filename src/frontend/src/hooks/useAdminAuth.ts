import { useMemo } from 'react'
import { getToken } from '../services/authService'

const CLAIM_FIELD = import.meta.env.VITE_ADMIN_CLAIM_FIELD ?? 'groups'
const CLAIM_VALUES = (import.meta.env.VITE_ADMIN_CLAIM_VALUES ?? 'app-admin')
  .split(',')
  .map((v: string) => v.trim())
  .filter(Boolean)

export function useAdminAuth(): { isAdmin: boolean } {
  return useMemo(() => {
    const token = getToken()
    if (!token) return { isAdmin: false }
    try {
      const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
      const payload = JSON.parse(atob(base64))
      const claimValue = payload[CLAIM_FIELD]
      if (!claimValue) return { isAdmin: false }

      const values: string[] = Array.isArray(claimValue)
        ? claimValue
        : String(claimValue).split(',').map((v: string) => v.trim())

      const isAdmin = values.some(v => CLAIM_VALUES.includes(v))
      return { isAdmin }
    } catch {
      return { isAdmin: false }
    }
  }, [])
}
