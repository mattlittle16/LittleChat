import { useEffect } from 'react'
import { useThemeStore } from '../stores/themeStore'

const STORAGE_KEY = 'littlechat_theme'

export function useTheme() {
  const { theme, setTheme } = useThemeStore()

  // Apply/remove .dark class on <html> whenever theme changes
  useEffect(() => {
    const root = document.documentElement
    if (theme === 'dark') {
      root.classList.add('dark')
    } else {
      root.classList.remove('dark')
    }
  }, [theme])

  // Listen for OS preference changes only when the user has no explicit stored preference
  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) return

    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = (e: MediaQueryListEvent) => {
      setTheme(e.matches ? 'dark' : 'light')
    }
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [setTheme])
}
