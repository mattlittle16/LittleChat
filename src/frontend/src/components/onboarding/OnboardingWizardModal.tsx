import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { updateDisplayName, uploadAvatar, setOnboardingStatus } from '../../services/profileService'
import { useUserProfileStore } from '../../stores/userProfileStore'
import { AvatarCropEditor } from '../profile/AvatarCropEditor'
import { UserAvatar } from '../common/UserAvatar'

const MAX_FILE_BYTES = 10 * 1024 * 1024 // 10 MB

interface OnboardingWizardModalProps {
  userId: string
  initialDisplayName: string
  initialProfileImageUrl: string | null
  initialAvatarUrl?: string | null
  onDone: () => void
}

type View = 'wizard' | 'skip-confirm'

export function OnboardingWizardModal({
  userId,
  initialDisplayName,
  initialProfileImageUrl,
  initialAvatarUrl,
  onDone,
}: OnboardingWizardModalProps) {
  const [view, setView] = useState<View>('wizard')
  const [displayName, setDisplayName] = useState(initialDisplayName)
  const [nameError, setNameError] = useState<string | null>(null)
  const [profileImageUrl, setProfileImageUrl] = useState<string | null>(initialProfileImageUrl)
  const [cropFile, setCropFile] = useState<File | null>(null)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const updateUser = useUserProfileStore(s => s.updateUser)

  // Block Escape key — wizard cannot be dismissed via keyboard
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') e.preventDefault()
    }
    document.addEventListener('keydown', onKeyDown, true)
    return () => document.removeEventListener('keydown', onKeyDown, true)
  }, [])

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    if (file.size > MAX_FILE_BYTES) {
      setSaveError('Photo must be under 10 MB.')
      return
    }
    if (!file.type.startsWith('image/')) {
      setSaveError('Please select an image file.')
      return
    }
    setSaveError(null)
    setCropFile(file)
  }

  async function handleCropConfirm(croppedBlob: Blob) {
    setCropFile(null)
    setSaving(true)
    setSaveError(null)
    try {
      const file = new File([croppedBlob], 'avatar.jpg', { type: 'image/jpeg' })
      const result = await uploadAvatar(file, 0, 0, 1)
      setProfileImageUrl(result.profileImageUrl)
      updateUser(userId, { profileImageUrl: result.profileImageUrl })
    } catch (e: unknown) {
      setSaveError(e instanceof Error ? e.message : 'Failed to upload photo.')
    } finally {
      setSaving(false)
    }
  }

  async function handleSubmit() {
    const trimmed = displayName.trim()
    if (!trimmed) {
      setNameError('Display name is required.')
      return
    }
    if (trimmed.length > 50) {
      setNameError('Display name must be 50 characters or fewer.')
      return
    }
    setNameError(null)
    setSaving(true)
    setSaveError(null)
    try {
      await updateDisplayName(trimmed)
      updateUser(userId, { displayName: trimmed })
      await setOnboardingStatus('dismissed')
      onDone()
    } catch (e: unknown) {
      setSaveError(e instanceof Error ? e.message : 'Failed to save. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  async function handleRemindLater() {
    setSaving(true)
    setSaveError(null)
    try {
      await setOnboardingStatus('remind_later')
      onDone()
    } catch (e: unknown) {
      setSaveError(e instanceof Error ? e.message : 'Failed to save preference. Please try again.')
      setView('wizard')
    } finally {
      setSaving(false)
    }
  }

  async function handleDontShowAgain() {
    setSaving(true)
    setSaveError(null)
    try {
      await setOnboardingStatus('dismissed')
      onDone()
    } catch (e: unknown) {
      setSaveError(e instanceof Error ? e.message : 'Failed to save preference. Please try again.')
      setView('wizard')
    } finally {
      setSaving(false)
    }
  }

  const modal = (
    <>
      {/* Backdrop — pointer events disabled so clicks pass nowhere */}
      <div
        className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
        style={{ pointerEvents: 'all' }}
        onMouseDown={e => e.stopPropagation()}
        onClick={e => e.stopPropagation()}
      >
        <div
          className="w-[440px] max-w-[calc(100vw-2rem)] rounded-xl border bg-background shadow-2xl"
          onClick={e => e.stopPropagation()}
        >
          {view === 'wizard' ? (
            <>
              <div className="border-b px-6 py-4">
                <h2 className="text-base font-semibold">Welcome to LittleChat</h2>
                <p className="mt-0.5 text-sm text-muted-foreground">
                  Take a moment to set up your profile. You can always change this later.
                </p>
              </div>

              <div className="px-6 py-5 flex flex-col gap-5">
                {/* Avatar */}
                <div className="flex flex-col items-center gap-3">
                  <button
                    className="relative group"
                    onClick={() => fileInputRef.current?.click()}
                    title="Upload photo"
                    disabled={saving}
                  >
                    <UserAvatar
                      userId={userId}
                      displayName={displayName || '?'}
                      profileImageUrl={profileImageUrl}
                      avatarUrl={initialAvatarUrl ?? null}
                      size={80}
                    />
                    <div className="absolute inset-0 rounded-full bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
                      <span className="text-white text-xs font-medium">Upload</span>
                    </div>
                  </button>
                  <p className="text-xs text-muted-foreground">Click to upload a photo (optional)</p>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/*"
                    className="hidden"
                    onChange={handleFileChange}
                  />
                </div>

                {/* Display name */}
                <div className="flex flex-col gap-1">
                  <label className="text-xs font-medium text-muted-foreground">
                    Display Name <span className="text-destructive">*</span>
                  </label>
                  <input
                    value={displayName}
                    onChange={e => {
                      setDisplayName(e.target.value)
                      if (nameError) setNameError(null)
                    }}
                    maxLength={50}
                    placeholder="Your name"
                    className="rounded border bg-background px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-primary/40"
                    disabled={saving}
                    onKeyDown={e => { if (e.key === 'Enter') handleSubmit() }}
                  />
                  <div className="flex items-center justify-between">
                    {nameError
                      ? <span className="text-xs text-destructive">{nameError}</span>
                      : <span />
                    }
                    <span className="text-xs text-muted-foreground">{displayName.length}/50</span>
                  </div>
                </div>

                {saveError && (
                  <p className="text-xs text-destructive">{saveError}</p>
                )}

                <div className="flex items-center justify-between pt-1">
                  <button
                    onClick={() => { setSaveError(null); setView('skip-confirm') }}
                    disabled={saving}
                    className="text-sm text-muted-foreground hover:text-foreground transition-colors"
                  >
                    Skip
                  </button>
                  <button
                    onClick={handleSubmit}
                    disabled={saving}
                    className="rounded bg-primary px-4 py-1.5 text-sm text-primary-foreground hover:opacity-90 disabled:opacity-50"
                  >
                    {saving ? 'Saving…' : 'Save & Continue'}
                  </button>
                </div>
              </div>
            </>
          ) : (
            <>
              <div className="border-b px-6 py-4">
                <h2 className="text-base font-semibold">Skip profile setup?</h2>
              </div>

              <div className="px-6 py-5 flex flex-col gap-4">
                <p className="text-sm text-muted-foreground">
                  Would you like to be reminded to complete your profile next time you open the app?
                </p>

                {saveError && (
                  <p className="text-xs text-destructive">{saveError}</p>
                )}

                <div className="flex flex-col gap-2">
                  <button
                    onClick={handleRemindLater}
                    disabled={saving}
                    className="rounded border px-4 py-2 text-sm hover:bg-muted/60 disabled:opacity-50 text-left"
                  >
                    <span className="font-medium">Remind me later</span>
                    <span className="block text-xs text-muted-foreground mt-0.5">
                      Show this again next time I open the app
                    </span>
                  </button>
                  <button
                    onClick={handleDontShowAgain}
                    disabled={saving}
                    className="rounded border px-4 py-2 text-sm hover:bg-muted/60 disabled:opacity-50 text-left"
                  >
                    <span className="font-medium">Don't show again</span>
                    <span className="block text-xs text-muted-foreground mt-0.5">
                      Permanently dismiss — I'll update my profile from the menu if needed
                    </span>
                  </button>
                </div>

                <div className="flex justify-start pt-1">
                  <button
                    onClick={() => { setSaveError(null); setView('wizard') }}
                    disabled={saving}
                    className="text-sm text-muted-foreground hover:text-foreground transition-colors"
                  >
                    ← Back
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      </div>

      {/* Crop sub-modal — renders on top of wizard (z-[60]) */}
      {cropFile && (
        <AvatarCropEditor
          imageFile={cropFile}
          onConfirm={handleCropConfirm}
          onCancel={() => setCropFile(null)}
        />
      )}
    </>
  )

  return createPortal(modal, document.body)
}
