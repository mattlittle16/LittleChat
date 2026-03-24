import { useEffect, useMemo, useRef, useState } from 'react'
import { useTheme } from '../../hooks/useTheme'
import { Bell, ChevronDown, Menu, Users } from 'lucide-react'
import { Sidebar } from './Sidebar'
import { MobileSidebarDrawer } from './MobileSidebarDrawer'
import { MessageList } from '../chat/MessageList'
import { MessageInput } from '../chat/MessageInput'
import { TypingIndicator } from '../chat/TypingIndicator'
import { SearchModal } from '../search/SearchModal'
import { MentionToastContainer } from '../chat/MentionToast'
import { NotificationSettingsPage } from '../settings/NotificationSettingsPage'
import { TopicMemberPanel } from '../topics/TopicMemberPanel'
import { useRoomStore } from '../../stores/roomStore'
import { useCurrentUserStore } from '../../stores/currentUserStore'
import { useMessageStore, getRoomMessages } from '../../stores/messageStore'
import { getConnection } from '../../services/signalrClient'
import { useNotificationPreferencesStore } from '../../stores/notificationPreferencesStore'
import { useNotificationStore } from '../../stores/notificationStore'
import { useSignalR } from '../../hooks/useSignalR'
import { getCurrentUserDisplayName, logout } from '../../services/authService'
import { api } from '../../services/apiClient'
import { getMyProfile } from '../../services/profileService'
import { useUserProfileStore } from '../../stores/userProfileStore'
import { UserProfileDialog } from '../profile/UserProfileDialog'
import { OnboardingWizardModal } from '../onboarding/OnboardingWizardModal'
import { NotificationCenter } from '../notifications/NotificationCenter'
import { useAdminAuth } from '../../hooks/useAdminAuth'
import { ErrorBoundary } from '../common/ErrorBoundary'

export function ChatLayout() {
  useTheme()
  const { activeRoomId, rooms } = useRoomStore()
  const { isAdmin } = useAdminAuth()
  const { status } = useSignalR(activeRoomId)
  const [view, setView] = useState<'chat' | 'notifications'>('chat')
  const [searchOpen, setSearchOpen] = useState(false)
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false)
  const [dmMenuOpen, setDmMenuOpen] = useState(false)
  const [dmConfirming, setDmConfirming] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const [roomMenuOpen, setRoomMenuOpen] = useState(false)
  const [roomConfirming, setRoomConfirming] = useState(false)
  const [memberPanelRoomId, setMemberPanelRoomId] = useState<string | null>(null)
  const showMemberPanel = memberPanelRoomId === activeRoomId
  const setShowMemberPanel = (show: boolean) => setMemberPanelRoomId(show ? activeRoomId : null)
  const dmMenuRef = useRef<HTMLDivElement>(null)
  const userMenuRef = useRef<HTMLDivElement>(null)
  const roomMenuRef = useRef<HTMLDivElement>(null)

  const setCurrentUserId = useCurrentUserStore(s => s.setId)
  const currentUserId = useCurrentUserStore(s => s.id)
  const onboardingStatus = useCurrentUserStore(s => s.onboardingStatus)
  const setOnboardingStatus = useCurrentUserStore(s => s.setOnboardingStatus)
  const loadPreferences = useNotificationPreferencesStore(s => s.loadPreferences)
  const loadOverrides = useNotificationPreferencesStore(s => s.loadOverrides)
  const unreadNotifCount = useNotificationStore(s => s.unreadCount)
  const setNotifOpen = useNotificationStore(s => s.setOpen)
  const isNotifOpen = useNotificationStore(s => s.isOpen)
  const loadNotifications = useNotificationStore(s => s.loadNotifications)
  const markRoomNotifRead = useNotificationStore(s => s.markRoomRead)
  const setProfile = useUserProfileStore(s => s.setProfile)
  const fetchAllUsers = useUserProfileStore(s => s.fetchAllUsers)

  // Onboarding wizard — initial profile snapshot for pre-population
  const [wizardProfile, setWizardProfile] = useState<{ displayName: string; profileImageUrl: string | null; avatarUrl: string | null } | null>(null)

  // US4: keyboard-selection state
  const [selectedMessageId, setSelectedMessageId] = useState<string | null>(null)
  const [deleteConfirmPending, setDeleteConfirmPending] = useState(false)
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null)

  const messages = useMessageStore(s => s.messages)
  const myMessages = useMemo(() => {
    if (!activeRoomId || !currentUserId) return []
    return getRoomMessages(messages, activeRoomId).filter(m => m.author.id === currentUserId)
  }, [messages, activeRoomId, currentUserId])

  // Fetch the backend-assigned internal user ID once on mount.
  // The JWT sub claim (used by getCurrentUserId) is Authentik's identifier,
  // not the internal UUID stored on messages — so we need /api/users/me.
  useEffect(() => {
    getMyProfile()
      .then(u => {
        setCurrentUserId(u.id)
        setProfile(u.id, { displayName: u.displayName, profileImageUrl: u.profileImageUrl })
        setOnboardingStatus(u.onboardingStatus)
        if (u.onboardingStatus === 'not_started' || u.onboardingStatus === 'remind_later') {
          setWizardProfile({ displayName: u.displayName, profileImageUrl: u.profileImageUrl, avatarUrl: u.avatarUrl ?? null })
        }
      })
      .catch(() => {
        // Fallback to legacy call that only returns id
        api.get<{ id: string }>('/api/users/me').then(u => setCurrentUserId(u.id)).catch(err => console.error('[ChatLayout] /api/users/me fallback failed', err))
      })
    // Seed profile store for all users so avatars render on first render (TTL-guarded — 60s)
    fetchAllUsers()
    loadPreferences().catch(err => console.error('[ChatLayout] loadPreferences failed', err))
    loadOverrides().catch(err => console.error('[ChatLayout] loadOverrides failed', err))
    loadNotifications().catch(err => console.error('[ChatLayout] loadNotifications failed', err))

    // Periodically refresh the user list so newly joined users appear without a page reload
    const userRefreshInterval = setInterval(() => fetchAllUsers(), 5 * 60 * 1000)
    return () => clearInterval(userRefreshInterval)
  }, [setCurrentUserId, setOnboardingStatus, setProfile, loadPreferences, loadOverrides, fetchAllUsers, loadNotifications])

  // Mark notifications as read when navigating to a room
  useEffect(() => {
    if (activeRoomId) {
      markRoomNotifRead(activeRoomId)
    }
  }, [activeRoomId, markRoomNotifRead])

  // Update document title with total unread count
  useEffect(() => {
    const unsub = useRoomStore.subscribe(s => {
      const total = s.rooms.reduce((sum, r) => sum + r.unreadCount, 0)
      document.title = total > 0 ? `(${total}) LittleChat` : 'LittleChat'
    })
    return unsub
  }, [])

  const activeRoom = rooms.find(r => r.id === activeRoomId)
  const roomName = activeRoom
    ? activeRoom.isDm
      ? activeRoom.otherUserDisplayName ?? activeRoom.name
      : `# ${activeRoom.name}`
    : null

  const isDisconnected = status === 'disconnected' || status === 'reconnecting'
  const displayName = getCurrentUserDisplayName()

  // Close DM menu when clicking outside
  useEffect(() => {
    if (!dmMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (dmMenuRef.current && !dmMenuRef.current.contains(e.target as Node)) {
        setDmMenuOpen(false)
        setDmConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [dmMenuOpen])

  // Close user menu when clicking outside
  useEffect(() => {
    if (!userMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (userMenuRef.current && !userMenuRef.current.contains(e.target as Node)) {
        setUserMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [userMenuOpen])

  // Close room menu when clicking outside
  useEffect(() => {
    if (!roomMenuOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (roomMenuRef.current && !roomMenuRef.current.contains(e.target as Node)) {
        setRoomMenuOpen(false)
        setRoomConfirming(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [roomMenuOpen])

  async function handleDeleteDm() {
    if (!activeRoomId) return
    setDmMenuOpen(false)
    setDmConfirming(false)
    await useRoomStore.getState().deleteRoom(activeRoomId)
  }

  async function handleDeleteRoom() {
    if (!activeRoomId) return
    setRoomMenuOpen(false)
    setRoomConfirming(false)
    await useRoomStore.getState().deleteRoom(activeRoomId)
  }

  // Ctrl+K / Cmd+K shortcut to open search
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault()
        setSearchOpen(true)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  // US4: global keydown handler for keyboard-selected message actions
  useEffect(() => {
    if (selectedMessageId === null) return
    function onKey(e: KeyboardEvent) {
      if (e.key === 'e' || e.key === 'E') {
        e.preventDefault()
        setEditingMessageId(selectedMessageId)
        setSelectedMessageId(null)
        setDeleteConfirmPending(false)
      } else if (e.key === 'Delete' || e.key === 'Backspace') {
        if (deleteConfirmPending) {
          const connection = getConnection()
          if (connection?.state === 'Connected' && selectedMessageId && activeRoomId) {
            connection.invoke('DeleteMessage', {
              messageId: selectedMessageId,
              roomId: activeRoomId,
            }).catch(err => console.error('[ChatLayout] DeleteMessage failed', err))
          }
          setSelectedMessageId(null)
          setDeleteConfirmPending(false)
          setEditingMessageId(null)
        } else {
          setDeleteConfirmPending(true)
        }
      } else if (e.key === 'Escape') {
        if (deleteConfirmPending) {
          setDeleteConfirmPending(false)
        } else {
          setSelectedMessageId(null)
          setEditingMessageId(null)
        }
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [selectedMessageId, deleteConfirmPending, activeRoomId])

  return (
    <div className="flex h-screen overflow-hidden relative">
      {/* Background blobs — outer div owns blur, inner div owns animation and themed color via CSS var */}
      <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
        {/* Blob 1 — purple-indigo, top right */}
        <div style={{ position: 'absolute', top: '-15%', right: '-5%', width: '42%', paddingBottom: '36%', filter: 'blur(52px)' }}>
          <div className="blob-1" style={{ position: 'absolute', inset: 0, borderRadius: '55% 45% 40% 60% / 50% 38% 62% 50%', background: 'radial-gradient(ellipse at 45% 45%, var(--blob-1), transparent 68%)' }} />
        </div>
        {/* Blob 2 — cyan-blue, bottom center-left */}
        <div style={{ position: 'absolute', bottom: '-8%', left: '15%', width: '40%', paddingBottom: '30%', filter: 'blur(56px)' }}>
          <div className="blob-2" style={{ position: 'absolute', inset: 0, borderRadius: '40% 60% 55% 45% / 58% 44% 56% 42%', background: 'radial-gradient(ellipse at 55% 55%, var(--blob-2), transparent 68%)' }} />
        </div>
        {/* Blob 3 — rose-pink, top left */}
        <div style={{ position: 'absolute', top: '-10%', left: '-5%', width: '34%', paddingBottom: '28%', filter: 'blur(58px)' }}>
          <div className="blob-3" style={{ position: 'absolute', inset: 0, borderRadius: '45% 55% 38% 62% / 52% 42% 58% 48%', background: 'radial-gradient(ellipse at 50% 50%, var(--blob-3), transparent 68%)' }} />
        </div>
        {/* Blob 4 — electric blue, mid right */}
        <div style={{ position: 'absolute', top: '28%', right: '-6%', width: '32%', paddingBottom: '26%', filter: 'blur(54px)' }}>
          <div className="blob-4" style={{ position: 'absolute', inset: 0, borderRadius: '60% 40% 52% 48% / 44% 58% 42% 56%', background: 'radial-gradient(ellipse at 48% 52%, var(--blob-4), transparent 68%)' }} />
        </div>
        {/* Blob 5 — teal-green, bottom right */}
        <div style={{ position: 'absolute', bottom: '-6%', right: '5%', width: '30%', paddingBottom: '24%', filter: 'blur(60px)' }}>
          <div className="blob-5" style={{ position: 'absolute', inset: 0, borderRadius: '50% 50% 44% 56% / 56% 44% 60% 40%', background: 'radial-gradient(ellipse at 52% 48%, var(--blob-5), transparent 68%)' }} />
        </div>
        {/* Blob 6 — violet, mid left */}
        <div style={{ position: 'absolute', top: '42%', left: '-8%', width: '28%', paddingBottom: '24%', filter: 'blur(62px)' }}>
          <div className="blob-6" style={{ position: 'absolute', inset: 0, borderRadius: '42% 58% 56% 44% / 48% 54% 46% 52%', background: 'radial-gradient(ellipse at 50% 50%, var(--blob-6), transparent 68%)' }} />
        </div>
      </div>

      {/* Sidebar — hidden on mobile, visible on md+ */}
      <div className="hidden md:flex">
        <ErrorBoundary name="Sidebar">
          <Sidebar />
        </ErrorBoundary>
      </div>

      {/* Mobile slide-out drawer */}
      <MobileSidebarDrawer
        open={mobileSidebarOpen}
        onClose={() => setMobileSidebarOpen(false)}
      />

      <div className="flex flex-1 flex-col min-w-0 relative">
        {view === 'notifications' ? (
          <NotificationSettingsPage onBack={() => setView('chat')} />
        ) : (<>
        {/* Toolbar */}
        <header className="flex h-12 items-center justify-between border-b px-4 flex-shrink-0 bg-muted/90 dark:bg-white/[0.06]">
          <div className="flex items-center gap-2 min-w-0">
            {/* Hamburger — mobile only */}
            <button
              onClick={() => setMobileSidebarOpen(true)}
              className="md:hidden rounded p-1.5 text-foreground/70 hover:text-foreground hover:bg-muted/60 transition-colors flex-shrink-0"
              aria-label="Open sidebar"
            >
              <Menu className="w-5 h-5" />
            </button>
            <span className="font-semibold text-sm truncate">
              {roomName ?? 'LittleChat'}
            </span>
          </div>

          <div className="flex items-center gap-2">
            {isDisconnected && (
              <span className="text-xs text-muted-foreground animate-pulse">
                {status === 'reconnecting' ? 'Reconnecting…' : 'Disconnected'}
              </span>
            )}

            {/* Members button — only shown for topic (non-DM) rooms */}
            {activeRoom && !activeRoom.isDm && (
              <button
                onClick={() => setShowMemberPanel(!showMemberPanel)}
                title="Members"
                aria-label="Members"
                className="flex items-center gap-1 rounded px-1.5 py-1 text-sm transition-colors"
                style={{
                  color: showMemberPanel ? 'hsl(var(--foreground))' : 'hsl(var(--foreground) / 0.6)',
                  background: showMemberPanel ? 'hsl(var(--muted))' : 'transparent',
                }}
              >
                <Users className="w-4 h-4" />
              </button>
            )}

            {/* Group room actions menu — only shown when viewing a non-DM room */}
            {activeRoom && !activeRoom.isDm && (
              <div className="relative" ref={roomMenuRef}>
                <button
                  onClick={() => { setRoomMenuOpen(o => !o); setRoomConfirming(false) }}
                  title="Room options"
                  aria-label="Room options"
                  className="rounded px-1.5 py-1 text-sm text-foreground/80 hover:text-foreground hover:bg-muted/60"
                >
                  ⋯
                </button>
                {roomMenuOpen && (
                  <div className="absolute right-0 top-full mt-1 z-20 w-44 rounded border bg-background shadow-md text-sm">
                    {!roomConfirming ? (
                      <button
                        onClick={() => setRoomConfirming(true)}
                        className="w-full px-3 py-2 text-left text-destructive hover:bg-muted/60"
                      >
                        Delete room
                      </button>
                    ) : (
                      <div className="px-3 py-2 flex flex-col gap-1">
                        <span className="text-xs text-muted-foreground">Are you sure?</span>
                        <div className="flex gap-1 mt-1">
                          <button
                            onClick={handleDeleteRoom}
                            className="flex-1 rounded bg-destructive px-2 py-1 text-xs text-destructive-foreground hover:opacity-90"
                          >
                            Yes, delete
                          </button>
                          <button
                            onClick={() => setRoomConfirming(false)}
                            className="flex-1 rounded bg-muted px-2 py-1 text-xs text-foreground hover:bg-muted/60"
                          >
                            No
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}

            {/* DM actions menu — only shown when viewing a DM */}
            {activeRoom?.isDm && (
              <div className="relative" ref={dmMenuRef}>
                <button
                  onClick={() => { setDmMenuOpen(o => !o); setDmConfirming(false) }}
                  title="DM options"
                  aria-label="DM options"
                  className="rounded px-1.5 py-1 text-sm text-foreground/80 hover:text-foreground hover:bg-muted/60"
                >
                  ⋯
                </button>
                {dmMenuOpen && (
                  <div className="absolute right-0 top-full mt-1 z-20 w-44 rounded border bg-background shadow-md text-sm">
                    {!dmConfirming ? (
                      <button
                        onClick={() => setDmConfirming(true)}
                        className="w-full px-3 py-2 text-left text-destructive hover:bg-muted/60"
                      >
                        Delete conversation
                      </button>
                    ) : (
                      <div className="px-3 py-2 flex flex-col gap-1">
                        <span className="text-xs text-muted-foreground">Are you sure?</span>
                        <div className="flex gap-1 mt-1">
                          <button
                            onClick={handleDeleteDm}
                            className="flex-1 rounded bg-destructive px-2 py-1 text-xs text-destructive-foreground hover:opacity-90"
                          >
                            Yes, delete
                          </button>
                          <button
                            onClick={() => setDmConfirming(false)}
                            className="flex-1 rounded bg-muted px-2 py-1 text-xs text-foreground hover:bg-muted/60"
                          >
                            No
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}

            {/* Notification bell + center */}
            <div className="relative">
              <button
                onClick={() => setNotifOpen(!isNotifOpen)}
                title="Notifications"
                aria-label="Notifications"
                className="relative rounded p-1.5 text-foreground/70 hover:text-foreground hover:bg-muted/60 transition-colors"
              >
                <Bell className="w-4 h-4" />
                {unreadNotifCount > 0 && (
                  <span className="absolute -top-0.5 -right-0.5 flex h-4 w-4 items-center justify-center rounded-full bg-destructive text-[10px] font-bold text-destructive-foreground leading-none">
                    {unreadNotifCount > 9 ? '9+' : unreadNotifCount}
                  </span>
                )}
              </button>
              {isNotifOpen && (
                <ErrorBoundary name="Notifications">
                  <NotificationCenter onClose={() => setNotifOpen(false)} />
                </ErrorBoundary>
              )}
            </div>

            <button
              onClick={() => setSearchOpen(true)}
              title="Search (Ctrl+K)"
              className="flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-xs text-foreground/80 hover:text-foreground hover:bg-muted/60 transition-colors"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M21 21l-4.35-4.35M17 11A6 6 0 1 1 5 11a6 6 0 0 1 12 0z" />
              </svg>
              Search
              <kbd className="ml-1 text-[10px] opacity-60">⌘K</kbd>
            </button>

            {/* User account dropdown */}
            {displayName && (
              <div className="relative" ref={userMenuRef}>
                <button
                  onClick={() => setUserMenuOpen(o => !o)}
                  className="flex items-center gap-1 rounded-md px-2 py-1 text-xs text-foreground/80 hover:text-foreground hover:bg-muted/60 transition-colors"
                  title="Account"
                >
                  <span className="max-w-[120px] truncate">{displayName}</span>
                  <ChevronDown className="w-3 h-3 flex-shrink-0" />
                </button>
                {userMenuOpen && (
                  <div className="absolute right-0 top-full mt-1 z-20 w-48 rounded border bg-background shadow-md text-sm">
                    <button
                      onClick={() => { setUserMenuOpen(false); setProfileOpen(true) }}
                      className="w-full px-3 py-2 text-left hover:bg-muted/60"
                    >
                      Edit Profile
                    </button>
                    <button
                      onClick={() => { setUserMenuOpen(false); setView('notifications') }}
                      className="w-full px-3 py-2 text-left hover:bg-muted/60"
                    >
                      Notification Settings
                    </button>
                    {isAdmin && (
                      <button
                        onClick={() => { window.location.href = '/admin' }}
                        className="w-full px-3 py-2 text-left hover:bg-muted/60"
                      >
                        Admin Panel
                      </button>
                    )}
                    <div className="border-t" />
                    <button
                      onClick={logout}
                      className="w-full px-3 py-2 text-left text-destructive hover:bg-muted/60"
                    >
                      Log Out
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </header>

        {/* Main chat area + optional member panel */}
        <div className="flex flex-1 min-h-0 overflow-hidden">
          <div className="flex flex-1 flex-col min-w-0">
            {activeRoomId ? (
              <>
                <ErrorBoundary name="Message List">
                  <MessageList
                    roomId={activeRoomId}
                    selectedMessageId={selectedMessageId}
                    deleteConfirmPending={deleteConfirmPending}
                    editingMessageId={editingMessageId}
                  />
                </ErrorBoundary>
                <TypingIndicator roomId={activeRoomId} />
                <MessageInput
                  roomId={activeRoomId}
                  disabled={isDisconnected}
                  onArrowUpOnEmpty={() => {
                    if (myMessages.length === 0) return
                    setEditingMessageId(null)
                    if (!selectedMessageId) {
                      setSelectedMessageId(myMessages.at(-1)!.id)
                    } else {
                      const idx = myMessages.findIndex(m => m.id === selectedMessageId)
                      if (idx > 0) setSelectedMessageId(myMessages[idx - 1].id)
                    }
                  }}
                  onArrowDown={() => {
                    if (!selectedMessageId) return false
                    const idx = myMessages.findIndex(m => m.id === selectedMessageId)
                    if (idx < myMessages.length - 1) {
                      setSelectedMessageId(myMessages[idx + 1].id)
                      setEditingMessageId(null)
                    } else {
                      setSelectedMessageId(null)
                      setEditingMessageId(null)
                      setDeleteConfirmPending(false)
                    }
                    return true
                  }}
                />
              </>
            ) : (
              <div className="flex flex-1 items-center justify-center text-muted-foreground text-sm">
                Select a room to start chatting.
              </div>
            )}
          </div>

          {/* Member panel — shown when toggled on a topic room */}
          {showMemberPanel && activeRoomId && activeRoom && !activeRoom.isDm && (
            <TopicMemberPanel
              roomId={activeRoomId}
              onClose={() => setShowMemberPanel(false)}
            />
          )}
        </div>
        </>)}
      </div>

      {searchOpen && <SearchModal onClose={() => setSearchOpen(false)} />}
      {profileOpen && currentUserId && (
        <UserProfileDialog userId={currentUserId} onClose={() => setProfileOpen(false)} />
      )}
      <MentionToastContainer />

      {/* Onboarding wizard — shown when status is not_started or remind_later */}
      {wizardProfile && currentUserId && (onboardingStatus === 'not_started' || onboardingStatus === 'remind_later') && (
        <OnboardingWizardModal
          userId={currentUserId}
          initialDisplayName={wizardProfile.displayName}
          initialProfileImageUrl={wizardProfile.profileImageUrl}
          initialAvatarUrl={wizardProfile.avatarUrl}
          onDone={() => {
            setOnboardingStatus('dismissed')
            setWizardProfile(null)
          }}
        />
      )}
    </div>
  )
}
