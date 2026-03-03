import { Sun, Moon } from 'lucide-react'
import { useThemeStore } from '../stores/themeStore'

export function ThemeToggle() {
  const { theme, toggleTheme } = useThemeStore()

  return (
    <button
      onClick={toggleTheme}
      title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
      aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
      className="flex items-center justify-center w-8 h-8 rounded-md transition-colors"
      style={{ color: 'hsl(var(--sidebar-muted-fg))' }}
      onMouseEnter={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-fg))')}
      onMouseLeave={e => (e.currentTarget.style.color = 'hsl(var(--sidebar-muted-fg))')}
    >
      {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
    </button>
  )
}
