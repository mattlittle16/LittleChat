import { useRef, useState } from 'react'
import { X } from 'lucide-react'
import { updateUserDisplayName, removeUserAvatar, updateUserAvatar, type AdminUser } from '../../services/adminApiService'
import { AvatarCropEditor } from '../profile/AvatarCropEditor'
import { AuthedImg } from '../chat/AuthedImg'

interface Props {
  user: AdminUser
  onClose: () => void
  onSuccess: (updated: Partial<AdminUser>) => void
}

export function AdminUserEditPanel({ user, onClose, onSuccess }: Props) {
  const [displayName, setDisplayName] = useState(user.displayName)
  const [savingName, setSavingName] = useState(false)
  const [removingAvatar, setRemovingAvatar] = useState(false)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [nameSuccess, setNameSuccess] = useState(false)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const [cropFile, setCropFile] = useState<File | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const profileImageUrl = user.profileImageUrl
  const avatarUrl = user.avatarUrl

  async function handleSaveName() {
    const trimmed = displayName.trim()
    if (!trimmed || trimmed.length > 50) {
      setError('Display name must be 1–50 characters.')
      return
    }
    if (trimmed.includes('@')) {
      setError("Display name cannot contain '@'. Use a name, not an email address.")
      return
    }
    setSavingName(true)
    setError(null)
    setNameSuccess(false)
    try {
      const res = await updateUserDisplayName(user.id, trimmed)
      onSuccess({ displayName: res.displayName })
      setNameSuccess(true)
    } catch {
      setError('Failed to update display name.')
    } finally {
      setSavingName(false)
    }
  }

  async function handleCropConfirm(croppedBlob: Blob) {
    setCropFile(null)
    setUploadingAvatar(true)
    setError(null)
    try {
      const file = new File([croppedBlob], 'avatar.jpg', { type: 'image/jpeg' })
      const res = await updateUserAvatar(user.id, file)
      onSuccess({ profileImageUrl: res.profileImageUrl })
    } catch {
      setError('Failed to upload avatar.')
    } finally {
      setUploadingAvatar(false)
    }
  }

  async function handleRemoveAvatar() {
    setConfirmRemove(false)
    setRemovingAvatar(true)
    setError(null)
    try {
      await removeUserAvatar(user.id)
      onSuccess({ profileImageUrl: null, avatarUrl: null })
    } catch {
      setError('Failed to remove avatar.')
    } finally {
      setRemovingAvatar(false)
    }
  }

  const isBusy = savingName || removingAvatar || uploadingAvatar

  return (
    <>
      <div
        className="fixed inset-0 z-40 flex items-center justify-center bg-black/40"
        onClick={e => { if (e.target === e.currentTarget) onClose() }}
      >
        <div className="w-96 rounded-lg border bg-background shadow-xl" onClick={e => e.stopPropagation()}>
          {/* Header */}
          <div className="flex items-center justify-between border-b px-4 py-3">
            <span className="font-semibold text-sm">Edit User: {user.displayName}</span>
            <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
              <X className="w-4 h-4" />
            </button>
          </div>

          <div className="px-4 py-4 flex flex-col gap-4">
            {/* Avatar section */}
            <div className="flex flex-col items-center gap-3">
              <button
                className="relative group"
                onClick={() => fileInputRef.current?.click()}
                title="Change photo"
                disabled={isBusy}
              >
                <div className="w-24 h-24 rounded-full overflow-hidden bg-muted border border-border flex items-center justify-center">
                  {profileImageUrl ? (
                    <AuthedImg
                      src={profileImageUrl}
                      alt={user.displayName}
                      className="w-full h-full object-cover"
                    />
                  ) : avatarUrl ? (
                    <img src={avatarUrl} alt={user.displayName} className="w-full h-full object-cover" />
                  ) : (
                    <span className="text-3xl font-semibold text-muted-foreground">
                      {user.displayName.charAt(0).toUpperCase()}
                    </span>
                  )}
                </div>
                <div className="absolute inset-0 rounded-full bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                  <span className="text-white text-xs font-medium">Change</span>
                </div>
              </button>

              {profileImageUrl && !confirmRemove && (
                <button
                  onClick={() => setConfirmRemove(true)}
                  disabled={isBusy}
                  className="rounded border border-destructive/40 px-3 py-1 text-xs text-destructive hover:bg-destructive/10 transition-colors"
                >
                  Remove photo
                </button>
              )}

              {profileImageUrl && confirmRemove && (
                <div className="flex items-center gap-2 rounded border border-destructive/40 px-3 py-1.5 bg-destructive/5">
                  <span className="text-xs text-muted-foreground">Remove photo?</span>
                  <button
                    onClick={handleRemoveAvatar}
                    disabled={isBusy}
                    className="rounded bg-destructive px-2 py-0.5 text-xs text-destructive-foreground hover:opacity-90"
                  >
                    Yes
                  </button>
                  <button
                    onClick={() => setConfirmRemove(false)}
                    className="rounded border px-2 py-0.5 text-xs hover:bg-muted/60"
                  >
                    No
                  </button>
                </div>
              )}

              {uploadingAvatar && (
                <span className="text-xs text-muted-foreground">Uploading…</span>
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
                onChange={e => { setDisplayName(e.target.value); setNameSuccess(false) }}
                maxLength={50}
                disabled={isBusy}
                className="rounded border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-primary/40"
                onKeyDown={e => { if (e.key === 'Enter') handleSaveName() }}
              />
              <span className="text-xs text-muted-foreground text-right">{displayName.length}/50</span>
            </div>

            {error && <p className="text-xs text-destructive">{error}</p>}
            {nameSuccess && <p className="text-xs text-green-600 dark:text-green-400">Display name updated.</p>}

            {/* Actions */}
            <div className="flex justify-end gap-2 pt-1">
              <button
                onClick={onClose}
                className="rounded border px-3 py-1.5 text-sm hover:bg-muted/60"
              >
                Close
              </button>
              <button
                onClick={handleSaveName}
                disabled={isBusy || displayName.trim() === user.displayName}
                className="rounded bg-primary px-3 py-1.5 text-sm text-primary-foreground hover:opacity-90 disabled:opacity-50"
              >
                {savingName ? 'Saving…' : 'Save Name'}
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
