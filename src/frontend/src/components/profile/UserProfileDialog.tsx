import { useEffect, useRef, useState } from 'react'
import { getMyProfile, updateDisplayName, uploadAvatar, deleteAvatar } from '../../services/profileService'
import { useUserProfileStore } from '../../stores/userProfileStore'
import { UserAvatar } from '../common/UserAvatar'
import { AvatarCropEditor } from './AvatarCropEditor'
import type { UserProfile } from '../../types'

interface UserProfileDialogProps {
  userId: string
  onClose: () => void
}

export function UserProfileDialog({ userId, onClose }: UserProfileDialogProps) {
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [displayName, setDisplayNameInput] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)
  const [cropFile, setCropFile] = useState<File | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const setProfile_ = useUserProfileStore(s => s.setProfile)
  const updateUser = useUserProfileStore(s => s.updateUser)

  useEffect(() => {
    getMyProfile()
      .then(p => {
        setProfile(p)
        setDisplayNameInput(p.displayName)
        setProfile_(p.id, { displayName: p.displayName, profileImageUrl: p.profileImageUrl })
      })
      .catch(() => setError('Failed to load profile.'))
  }, [setProfile_])

  async function handleSaveName() {
    const trimmed = displayName.trim()
    if (!trimmed || trimmed.length < 1 || trimmed.length > 50) {
      setError('Display name must be 1–50 characters.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await updateDisplayName(trimmed)
      updateUser(userId, { displayName: trimmed })
      setSuccessMsg('Display name updated.')
      setTimeout(() => setSuccessMsg(null), 3000)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to update display name.')
    } finally {
      setSaving(false)
    }
  }

  async function handleCropConfirm(croppedBlob: Blob) {
    setCropFile(null)
    setSaving(true)
    setError(null)
    try {
      const file = new File([croppedBlob], 'avatar.jpg', { type: 'image/jpeg' })
      const result = await uploadAvatar(file, 0, 0, 1)
      // Append timestamp so AuthedImg sees a new src and re-fetches immediately
      const freshUrl = result.profileImageUrl ? `${result.profileImageUrl}?t=${Date.now()}` : null
      if (profile) {
        setProfile({ ...profile, profileImageUrl: freshUrl })
      }
      updateUser(userId, { profileImageUrl: freshUrl })
      setSuccessMsg('Avatar updated.')
      setTimeout(() => setSuccessMsg(null), 3000)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to upload avatar.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteAvatar() {
    setSaving(true)
    setError(null)
    try {
      await deleteAvatar()
      if (profile) {
        setProfile({ ...profile, profileImageUrl: null })
      }
      updateUser(userId, { profileImageUrl: null })
      setSuccessMsg('Avatar removed.')
      setTimeout(() => setSuccessMsg(null), 3000)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Failed to remove avatar.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div
        className="fixed inset-0 z-40 flex items-center justify-center bg-black/40"
        onClick={e => { if (e.target === e.currentTarget) onClose() }}
      >
        <div className="w-96 rounded-lg border bg-background shadow-xl" onClick={e => e.stopPropagation()}>
          {/* Header */}
          <div className="flex items-center justify-between border-b px-4 py-3">
            <span className="font-semibold text-sm">Profile</span>
            <button onClick={onClose} className="text-muted-foreground hover:text-foreground">✕</button>
          </div>

          <div className="px-4 py-4 flex flex-col gap-4">
            {/* Avatar section */}
            <div className="flex flex-col items-center gap-3">
              <button
                className="relative group"
                onClick={() => fileInputRef.current?.click()}
                title="Change photo"
                disabled={saving}
              >
                <UserAvatar
                  userId={userId}
                  displayName={profile?.displayName ?? '?'}
                  profileImageUrl={profile?.profileImageUrl ?? null}
                  avatarUrl={profile?.avatarUrl}
                  size={96}
                />
                <div className="absolute inset-0 rounded-full bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                  <span className="text-white text-xs font-medium">Change</span>
                </div>
              </button>

              {profile?.profileImageUrl && (
                <button
                  onClick={handleDeleteAvatar}
                  disabled={saving}
                  className="text-xs text-muted-foreground hover:text-destructive transition-colors"
                >
                  Remove photo
                </button>
              )}

              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={e => {
                  const file = e.target.files?.[0]
                  if (file) setCropFile(file)
                  e.target.value = ''
                }}
              />
            </div>

            {/* Display name */}
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-muted-foreground">Display Name</label>
              <input
                value={displayName}
                onChange={e => setDisplayNameInput(e.target.value)}
                maxLength={50}
                className="rounded border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-primary/40"
                onKeyDown={e => { if (e.key === 'Enter') handleSaveName() }}
                disabled={saving}
              />
              <span className="text-xs text-muted-foreground text-right">{displayName.length}/50</span>
            </div>

            {/* Email (read-only) */}
            {profile?.email && (
              <div className="flex flex-col gap-1">
                <label className="text-xs font-medium text-muted-foreground">Email</label>
                <input
                  value={profile.email}
                  readOnly
                  className="rounded border bg-muted/50 px-3 py-1.5 text-sm text-muted-foreground outline-none cursor-not-allowed"
                />
              </div>
            )}

            {/* Messages */}
            {error && (
              <p className="text-xs text-destructive">{error}</p>
            )}
            {successMsg && (
              <p className="text-xs text-green-600 dark:text-green-400">{successMsg}</p>
            )}

            {/* Actions */}
            <div className="flex justify-end gap-2 pt-1">
              <button
                onClick={onClose}
                className="rounded border px-3 py-1.5 text-sm hover:bg-muted/60"
              >
                Cancel
              </button>
              <button
                onClick={handleSaveName}
                disabled={saving}
                className="rounded bg-primary px-3 py-1.5 text-sm text-primary-foreground hover:opacity-90 disabled:opacity-50"
              >
                {saving ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Crop sub-modal */}
      {cropFile && (
        <div className="z-50">
          <AvatarCropEditor
            imageFile={cropFile}
            onConfirm={handleCropConfirm}
            onCancel={() => setCropFile(null)}
          />
        </div>
      )}
    </>
  )
}
