import { useEffect, useMemo, useRef, useState } from 'react'
import { useTheme } from '../../hooks/useTheme'
import { Bell, ChevronDown, Menu, Users, Star } from 'lucide-react'
import { Sidebar } from './Sidebar'
import { MobileSidebarDrawer } from './MobileSidebarDrawer'
import { MessageList } from '../chat/MessageList'
import { MessageInput } from '../chat/MessageInput'
import { TypingIndicator } from '../chat/TypingIndicator'
import { SearchModal } from '../search/SearchModal'
import { StatusPicker } from '../status/StatusPicker'
import { MentionToastContainer } from '../chat/MentionToast'
import { NotificationSettingsPage } from '../settings/NotificationSettingsPage'
import { TopicMemberPanel } from '../topics/TopicMemberPanel'
import { HighlightsTab } from '../highlights/HighlightsTab'
import { BookmarksView } from '../bookmarks/BookmarksView'
import { DailyDigestView } from '../digest/DailyDigestView'
import { useRoomStore } from '../../stores/roomStore'
import { useCurrentUserStore } from '../../stores/currentUserStore'
import { useMessageStore, getRoomMessages } from '../../stores/messageStore'
import { useHighlightStore } from '../../stores/highlightStore'
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
import { getBookmarks, getHighlights, clearStatus } from '../../services/enrichedMessagingApiService'
import type { UserStatus } from '../../types'
import { useBookmarkStore } from '../../stores/bookmarkStore'

export function ChatLayout() {
  useTheme()
  const { activeRoomId, rooms } = useRoomStore()
  const { isAdmin } = useAdminAuth()
  const { status } = useSignalR(activeRoomId)
  const [view, setView] = useState<'chat' | 'notifications' | 'bookmarks' | 'digest'>('chat')
  // chatTabByRoom: stores the active tab per roomId. If the room has no entry, defaults to 'messages'.
  const [chatTabByRoom, setChatTabByRoom] = useState<Record<string, 'messages' | 'highlights'>>({})
  const chatTab = (activeRoomId ? chatTabByRoom[activeRoomId] : undefined) ?? 'messages'
  function setChatTab(tab: 'messages' | 'highlights') {
    if (activeRoomId) setChatTabByRoom(prev => ({ ...prev, [activeRoomId]: tab }))
  }
  const [searchOpen, setSearchOpen] = useState(false)
  const [mobileSidebarOpen, setMobileSidebarOpen] = useState(false)
  const [dmMenuOpen, setDmMenuOpen] = useState(false)
  const [dmConfirming, setDmConfirming] = useState(false)
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const [profileOpen, setProfileOpen] = useState(false)
  const [statusPickerOpen, setStatusPickerOpen] = useState(false)
  const [myStatus, setMyStatus] = useState<UserStatus | null>(null)
  const [memberPanelRoomId, setMemberPanelRoomId] = useState<string | null>(null)
  const showMemberPanel = memberPanelRoomId === activeRoomId
  const setShowMemberPanel = (show: boolean) => setMemberPanelRoomId(show ? activeRoomId : null)
  const dmMenuRef = useRef<HTMLDivElement>(null)
  const userMenuRef = useRef<HTMLDivElement>(null)


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
  const setHighlights = useHighlightStore(s => s.setHighlights)
  const setBookmarks = useBookmarkStore(s => s.setBookmarks)

  // Onboarding wizard — initial profile snapshot for pre-population
  const [wizardProfile, setWizardProfile] = useState<{ displayName: string; profileImageUrl: string | null; avatarUrl: string | null } | null>(null)

  // US4: keyboard-selection state
  const [selectedMessageId, setSelectedMessageId] = useState<string | null>(null)
  const [deleteConfirmPending, setDeleteConfirmPending] = useState(false)
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null)
  const [pendingQuote, setPendingQuote] = useState<{ roomId: string; messageId: string; authorDisplayName: string; contentSnapshot: string } | null>(null)
  // Derived: only expose the quote when it belongs to the active room (handles room switches without effects)
  const activePendingQuote = pendingQuote?.roomId === activeRoomId ? pendingQuote : null

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
        setMyStatus(u.status ?? null)
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
    getBookmarks().then(data => setBookmarks(data.folders, data.unfiled)).catch(() => {})

    // Periodically refresh the user list so newly joined users appear without a page reload
    const userRefreshInterval = setInterval(() => fetchAllUsers(), 5 * 60 * 1000)
    return () => clearInterval(userRefreshInterval)
  }, [setCurrentUserId, setOnboardingStatus, setProfile, loadPreferences, loadOverrides, fetchAllUsers, loadNotifications, setBookmarks])

  // Mark notifications as read when navigating to a room
  useEffect(() => {
    if (activeRoomId) {
      markRoomNotifRead(activeRoomId)
    }
  }, [activeRoomId, markRoomNotifRead])

  // Fetch highlights whenever the active room changes (needed to show star badges in chat feed)
  useEffect(() => {
    if (activeRoomId) {
      getHighlights(activeRoomId).then(items => setHighlights(activeRoomId, items)).catch(() => {})
    }
  }, [activeRoomId, setHighlights])

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
    if (!userMenuOpen && !statusPickerOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (userMenuRef.current && !userMenuRef.current.contains(e.target as Node)) {
        setUserMenuOpen(false)
        setStatusPickerOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [userMenuOpen, statusPickerOpen])


  function handleJumpToMessage(roomId: string, messageId: string) {
    useRoomStore.getState().setActiveRoom(roomId)
    useMessageStore.getState().setPendingAround(roomId, messageId)
    useMessageStore.getState().setScrollToMessageId(messageId)
    setView('chat')
    setChatTab('messages')
  }

  function handleJumpToRoomMessage(messageId: string) {
    if (activeRoomId) {
      useMessageStore.getState().setPendingAround(activeRoomId, messageId)
      useMessageStore.getState().setScrollToMessageId(messageId)
      setChatTab('messages')
    }
  }

  async function handleDeleteDm() {
    if (!activeRoomId) return
    setDmMenuOpen(false)
    setDmConfirming(false)
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
      {/* Sidebar — hidden on mobile, visible on md+ */}
      <div className="hidden md:flex">
        <ErrorBoundary name="Sidebar">
          <Sidebar
            onOpenBookmarks={() => setView('bookmarks')}
            onOpenDigest={() => setView('digest')}
          />
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
        ) : view === 'bookmarks' ? (
          <div className="flex flex-col h-full">
            <header className="flex h-12 items-center gap-2 border-b px-4 flex-shrink-0 bg-muted/90 dark:bg-white/[0.06]">
              <button onClick={() => setView('chat')} className="text-sm text-muted-foreground hover:text-foreground">← Back</button>
            </header>
            <div className="flex-1 min-h-0 overflow-hidden">
              <BookmarksView onNavigate={handleJumpToMessage} />
            </div>
          </div>
        ) : view === 'digest' ? (
          <div className="flex flex-col h-full">
            <header className="flex h-12 items-center gap-2 border-b px-4 flex-shrink-0 bg-muted/90 dark:bg-white/[0.06]">
              <button onClick={() => setView('chat')} className="text-sm text-muted-foreground hover:text-foreground">← Back</button>
            </header>
            <div className="flex-1 min-h-0 overflow-hidden">
              <DailyDigestView onNavigate={handleJumpToMessage} />
            </div>
          </div>
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

            {/* Highlights tab toggle — only for topic (non-DM) rooms */}
            {activeRoom && !activeRoom.isDm && (
              <button
                onClick={() => setChatTab(chatTab === 'highlights' ? 'messages' : 'highlights')}
                title="Highlights"
                aria-label="Highlights"
                className="flex items-center gap-1 rounded px-1.5 py-1 text-sm transition-colors"
                style={{
                  color: chatTab === 'highlights' ? 'hsl(var(--foreground))' : 'hsl(var(--foreground) / 0.6)',
                  background: chatTab === 'highlights' ? 'hsl(var(--muted))' : 'transparent',
                }}
              >
                <Star className="w-4 h-4" />
              </button>
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
                {statusPickerOpen && (
                  <div className="absolute right-0 top-full mt-1 z-30">
                    <StatusPicker
                      currentStatus={myStatus}
                      onClose={() => setStatusPickerOpen(false)}
                      onStatusChange={s => setMyStatus(s)}
                    />
                  </div>
                )}
                {userMenuOpen && (
                  <div className="absolute right-0 top-full mt-1 z-20 w-48 rounded border bg-background shadow-md text-sm">
                    {myStatus ? (
                      <button
                        onClick={() => { setUserMenuOpen(false); clearStatus().catch(() => {}); setMyStatus(null) }}
                        className="w-full px-3 py-2 text-left hover:bg-muted/60"
                      >
                        Clear Status
                      </button>
                    ) : (
                      <button
                        onClick={() => { setUserMenuOpen(false); setStatusPickerOpen(true) }}
                        className="w-full px-3 py-2 text-left hover:bg-muted/60"
                      >
                        Set Status
                      </button>
                    )}
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
                {chatTab === 'highlights' ? (
                  <ErrorBoundary name="Highlights">
                    <HighlightsTab
                      roomId={activeRoomId}
                      onJumpTo={handleJumpToRoomMessage}
                    />
                  </ErrorBoundary>
                ) : (
                  <ErrorBoundary name="Message List">
                    <MessageList
                      roomId={activeRoomId}
                      selectedMessageId={selectedMessageId}
                      deleteConfirmPending={deleteConfirmPending}
                      editingMessageId={editingMessageId}
                      onSetPendingQuote={(quoteData) => setPendingQuote({ ...quoteData, roomId: activeRoomId! })}
                    />
                  </ErrorBoundary>
                )}
                {chatTab === 'messages' && <TypingIndicator roomId={activeRoomId} />}
                {chatTab === 'messages' && <MessageInput
                  roomId={activeRoomId}
                  disabled={isDisconnected}
                  pendingQuote={activePendingQuote}
                  onClearQuote={() => setPendingQuote(null)}
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
                />}
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
