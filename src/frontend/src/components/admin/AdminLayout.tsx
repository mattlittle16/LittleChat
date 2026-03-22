import { useState } from 'react'
import { Shield, Users, Hash, ScrollText, Sun, Moon } from 'lucide-react'
import { AdminUsersView } from './AdminUsersView'
import { AdminTopicsView } from './AdminTopicsView'
import { AdminAuditLogView } from './AdminAuditLogView'
import { useThemeStore } from '../../stores/themeStore'
import { useTheme } from '../../hooks/useTheme'

type AdminTab = 'users' | 'topics' | 'audit-log'

export function AdminLayout() {
  useTheme()
  const [activeTab, setActiveTab] = useState<AdminTab>('users')
  const { theme, toggleTheme } = useThemeStore()

  return (
    <div className="flex flex-col h-screen bg-background text-foreground">
      {/* Header */}
      <div
        className="flex items-center gap-3 px-6 py-4 border-b"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <Shield className="w-5 h-5 text-primary" />
        <h1 className="text-lg font-semibold">Admin Panel</h1>
        <div className="ml-auto flex items-center gap-2">
          <button
            onClick={toggleTheme}
            title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
            aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
            className="flex items-center justify-center w-8 h-8 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
          >
            {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
          </button>
          <button
            onClick={() => { window.location.href = '/' }}
            className="text-sm text-muted-foreground hover:text-foreground border border-border rounded-md px-3 py-1.5 bg-muted/90 dark:bg-white/[0.06] hover:brightness-110 transition-colors"
          >
            ← Back to Chat
          </button>
        </div>
      </div>

      {/* Tab Nav */}
      <div
        className="flex gap-0 px-6 border-b"
        style={{ borderColor: 'hsl(var(--border))' }}
      >
        <TabButton
          icon={<Users className="w-4 h-4" />}
          label="Users"
          active={activeTab === 'users'}
          onClick={() => setActiveTab('users')}
        />
        <TabButton
          icon={<Hash className="w-4 h-4" />}
          label="Topics"
          active={activeTab === 'topics'}
          onClick={() => setActiveTab('topics')}
        />
        <TabButton
          icon={<ScrollText className="w-4 h-4" />}
          label="Audit Log"
          active={activeTab === 'audit-log'}
          onClick={() => setActiveTab('audit-log')}
        />
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto p-6">
        {activeTab === 'users' && <AdminUsersView />}
        {activeTab === 'topics' && <AdminTopicsView />}
        {activeTab === 'audit-log' && <AdminAuditLogView />}
      </div>
    </div>
  )
}

function TabButton({
  icon,
  label,
  active,
  onClick,
}: {
  icon: React.ReactNode
  label: string
  active: boolean
  onClick: () => void
}) {
  return (
    <button
      onClick={onClick}
      className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
        active
          ? 'border-primary text-primary'
          : 'border-transparent text-muted-foreground hover:text-foreground hover:border-muted-foreground'
      }`}
    >
      {icon}
      {label}
    </button>
  )
}
