import { useRef, useState, useEffect } from 'react'
import { InlineMarkdownEditor } from './InlineMarkdownEditor'
import type { InlineMarkdownEditorRef } from './InlineMarkdownEditor'
import { useOutboxStore } from '../../stores/outboxStore'
import { getConnection } from '../../services/signalrClient'
import { getAccessToken, api } from '../../services/apiClient'
import { FileUploadProgress } from '../files/FileUploadProgress'
import type { UserSearchResult } from '../../types'

const TYPING_DEBOUNCE_MS = 500
const MAX_LENGTH = 4_000
const MAX_FILE_COUNT = 15
const MAX_PER_FILE_BYTES = 200 * 1024 * 1024   // 200 MB
const MAX_COMBINED_BYTES = 500 * 1024 * 1024   // 500 MB

interface MessageInputProps {
  roomId: string
  disabled?: boolean
  onArrowUpOnEmpty?: () => void
  onArrowDown?: () => boolean
}

interface StagedFile {
  file: File
  previewUrl: string | null  // object URL for images, null for non-images
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function MessageInput({ roomId, disabled = false, onArrowUpOnEmpty, onArrowDown }: MessageInputProps) {
  const [content, setContent] = useState('')
  const [stagedFiles, setStagedFiles] = useState<StagedFile[]>([])
  const [uploadProgress, setUploadProgress] = useState<number | null>(null)
  const [failedFiles, setFailedFiles] = useState<string[]>([])
  const [mentionQuery, setMentionQuery] = useState<string | null>(null)
  const [mentionUsers, setMentionUsers] = useState<UserSearchResult[]>([])
  const [mentionIndex, setMentionIndex] = useState(0)
  const editorRef = useRef<InlineMarkdownEditorRef>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const typingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const latestContentRef = useRef('')
  const cursorPosRef = useRef(0)
  const { enqueue, messages: outboxMessages, retryAll } = useOutboxStore()
  const hasFailed = outboxMessages.some(m => m.status === 'failed')
  const isConnected = getConnection()?.state === 'Connected'
  const isDisabled = disabled || !isConnected
  const isUploading = uploadProgress !== null

  // Revoke object URLs on unmount
  useEffect(() => {
    return () => {
      stagedFiles.forEach(sf => { if (sf.previewUrl) URL.revokeObjectURL(sf.previewUrl) })
    }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (mentionQuery === null) return
    let cancelled = false
    const timeout = setTimeout(() => {
      const params = mentionQuery ? `?q=${encodeURIComponent(mentionQuery)}` : ''
      api.get<UserSearchResult[]>(`/api/users${params}`)
        .then(users => { if (!cancelled) setMentionUsers(users.slice(0, 6)) })
        .catch(() => { if (!cancelled) setMentionUsers([]) })
    }, 150)
    return () => { cancelled = true; clearTimeout(timeout) }
  }, [mentionQuery])

  function detectMention(text: string, cursorPos: number) {
    const before = text.slice(0, cursorPos)
    const match = before.match(/@(\w*)$/)
    if (match) {
      setMentionQuery(match[1])
      setMentionIndex(0)
    } else {
      setMentionQuery(null)
      setMentionUsers([])
    }
  }

  function completeMention(displayName: string) {
    const cursorPos = cursorPosRef.current
    const text = latestContentRef.current
    const before = text.slice(0, cursorPos)
    const after = text.slice(cursorPos)
    const newBefore = before.replace(/@\w*$/, `@${displayName} `)
    const newContent = newBefore + after
    latestContentRef.current = newContent
    setContent(newContent)
    setMentionQuery(null)
    setMentionUsers([])
    setTimeout(() => editorRef.current?.focus(), 0)
  }

  function notifyTyping() {
    const connection = getConnection()
    if (connection?.state !== 'Connected') return
    if (typingTimerRef.current) return
    connection.invoke('StartTyping', { roomId }).catch(() => {})
    typingTimerRef.current = setTimeout(() => {
      typingTimerRef.current = null
    }, TYPING_DEBOUNCE_MS)
  }

  function handleMentionKeyDown(e: React.KeyboardEvent) {
    if (mentionQuery === null || mentionUsers.length === 0) return false
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setMentionIndex(i => (i + 1) % mentionUsers.length)
      return true
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      setMentionIndex(i => (i - 1 + mentionUsers.length) % mentionUsers.length)
      return true
    }
    if (e.key === 'Enter' || e.key === 'Tab') {
      e.preventDefault()
      completeMention(mentionUsers[mentionIndex].displayName)
      return true
    }
    if (e.key === 'Escape') {
      setMentionQuery(null)
      setMentionUsers([])
      return true
    }
    return false
  }

  function addFiles(newFiles: FileList | null) {
    if (!newFiles || newFiles.length === 0) return

    const combined = [...stagedFiles]
    const errors: string[] = []

    for (const file of Array.from(newFiles)) {
      if (combined.length >= MAX_FILE_COUNT) {
        errors.push(`Max ${MAX_FILE_COUNT} files per message`)
        break
      }
      if (file.size > MAX_PER_FILE_BYTES) {
        errors.push(`"${file.name}" exceeds the 200 MB per-file limit`)
        continue
      }
      const combinedSize = combined.reduce((sum, sf) => sum + sf.file.size, 0) + file.size
      if (combinedSize > MAX_COMBINED_BYTES) {
        errors.push(`Adding "${file.name}" would exceed the 500 MB combined limit`)
        continue
      }
      const isImage = file.type.startsWith('image/')
      const previewUrl = isImage ? URL.createObjectURL(file) : null
      combined.push({ file, previewUrl })
    }

    if (errors.length > 0) alert(errors.join('\n'))
    setStagedFiles(combined)
  }

  function removeFile(index: number) {
    setStagedFiles(prev => {
      const sf = prev[index]
      if (sf.previewUrl) URL.revokeObjectURL(sf.previewUrl)
      return prev.filter((_, i) => i !== index)
    })
  }

  function clearFiles() {
    stagedFiles.forEach(sf => { if (sf.previewUrl) URL.revokeObjectURL(sf.previewUrl) })
    setStagedFiles([])
    setUploadProgress(null)
  }

  async function submitWithFiles() {
    const trimmed = content.trim()
    const hasFiles = stagedFiles.length > 0

    if (!hasFiles) return
    if (trimmed.length > MAX_LENGTH) return

    const token = getAccessToken()
    const formData = new FormData()
    formData.append('id', crypto.randomUUID())
    formData.append('content', trimmed)
    for (const sf of stagedFiles) {
      formData.append('file', sf.file)
    }

    setContent('')
    clearFiles()
    setUploadProgress(0)
    setFailedFiles([])

    await new Promise<void>((resolve, reject) => {
      const xhr = new XMLHttpRequest()
      xhr.open('POST', `/api/rooms/${roomId}/messages`)
      if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`)

      xhr.upload.onprogress = e => {
        if (e.lengthComputable)
          setUploadProgress(Math.round((e.loaded / e.total) * 100))
      }

      xhr.onload = () => {
        setUploadProgress(null)
        if (xhr.status >= 200 && xhr.status < 300) {
          try {
            const data = JSON.parse(xhr.responseText)
            if (data.failedFiles && data.failedFiles.length > 0) {
              setFailedFiles(data.failedFiles)
            }
          } catch { /* ignore JSON parse errors — failedFiles defaults to empty */ }
          resolve()
        } else {
          // Parse the error response to show a meaningful message
          try {
            const data = JSON.parse(xhr.responseText)
            if (data.blockedFiles && data.blockedFiles.length > 0) {
              setFailedFiles([`Blocked file type: ${data.blockedFiles.join(', ')}`])
            } else if (typeof data === 'string') {
              setFailedFiles([data])
            } else if (data.error) {
              setFailedFiles([`${data.error}${data.blockedFiles ? ': ' + data.blockedFiles.join(', ') : ''}`])
            } else {
              setFailedFiles(['Upload failed — please try again.'])
            }
          } catch {
            setFailedFiles([xhr.responseText || 'Upload failed — please try again.'])
          }
          reject(new Error(`Upload failed: ${xhr.status}`))
        }
      }

      xhr.onerror = () => {
        setUploadProgress(null)
        setFailedFiles(['Upload failed — please check your connection and try again.'])
        reject(new Error('Upload error'))
      }

      xhr.send(formData)
    }).catch(() => {})

    editorRef.current?.focus()
  }

  async function submit() {
    if (stagedFiles.length > 0) {
      await submitWithFiles()
      return
    }
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > MAX_LENGTH) return
    setContent('')
    await enqueue(roomId, trimmed)
    editorRef.current?.focus()
  }

  const remaining = MAX_LENGTH - content.length
  const overLimit = remaining < 0
  const canSend = !isDisabled && !isUploading && !overLimit && (content.trim().length > 0 || stagedFiles.length > 0)

  return (
    <div className="border-t p-3 flex flex-col gap-2 bg-white/[0.06]">
      {hasFailed && (
        <div className="flex items-center gap-2 text-sm text-destructive">
          <span>Some messages failed to send.</span>
          <button onClick={retryAll} className="underline hover:no-underline">
            Retry all
          </button>
        </div>
      )}

      {/* Partial upload failure warning */}
      {failedFiles.length > 0 && (
        <div className="flex items-start gap-2 text-xs text-amber-600 dark:text-amber-400">
          <span>⚠️ Some files failed to upload: {failedFiles.join(', ')}</span>
          <button onClick={() => setFailedFiles([])} className="ml-auto flex-shrink-0 underline">
            Dismiss
          </button>
        </div>
      )}

      {/* Staging area */}
      {stagedFiles.length > 0 && uploadProgress === null && (
        <div className="flex flex-wrap gap-2">
          {stagedFiles.map((sf, i) => (
            <div key={i} className="relative group">
              {sf.previewUrl ? (
                <img
                  src={sf.previewUrl}
                  alt={sf.file.name}
                  className="h-16 w-16 rounded-md border object-cover"
                />
              ) : (
                <div className="h-16 w-16 rounded-md border flex flex-col items-center justify-center bg-muted text-xs text-center px-1 gap-0.5">
                  <span>📎</span>
                  <span className="truncate w-full text-center leading-tight">{sf.file.name.split('.').pop()?.toUpperCase()}</span>
                </div>
              )}
              <div className="text-[10px] text-muted-foreground text-center mt-0.5 max-w-[64px] truncate">{sf.file.name}</div>
              <div className="text-[10px] text-muted-foreground text-center">{formatBytes(sf.file.size)}</div>
              <button
                onClick={() => removeFile(i)}
                className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-destructive text-destructive-foreground text-xs leading-none flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                aria-label="Remove file"
              >
                ×
              </button>
            </div>
          ))}
          {stagedFiles.length < MAX_FILE_COUNT && (
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              className="h-16 w-16 rounded-md border border-dashed flex items-center justify-center text-muted-foreground hover:bg-muted/40 text-xl"
              aria-label="Add more files"
            >
              +
            </button>
          )}
        </div>
      )}

      {/* Upload progress bar */}
      {isUploading && (
        <FileUploadProgress
          fileName={stagedFiles.length > 1 ? `${stagedFiles.length} files` : (stagedFiles[0]?.file.name ?? 'file')}
          progress={uploadProgress!}
        />
      )}

      <div className="flex items-end gap-2">
        {/* Hidden file input — multiple */}
        <input
          ref={fileInputRef}
          type="file"
          multiple
          className="hidden"
          onChange={e => { addFiles(e.target.files); e.target.value = '' }}
        />

        {/* Clip button */}
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={isDisabled || isUploading || stagedFiles.length >= MAX_FILE_COUNT}
          title="Attach files"
          className="self-stretch rounded-md px-2 hover:opacity-90
                     disabled:opacity-50 flex-shrink-0 flex items-center"
          style={{ background: 'hsl(var(--secondary))', color: 'hsl(var(--secondary-foreground))' }}
        >
          📎
        </button>

        <div className="flex-1 min-w-0 relative">
          {/* @mention autocomplete dropdown */}
          {mentionQuery !== null && mentionUsers.length > 0 && (
            <div className="absolute bottom-full mb-1 left-0 right-0 rounded-md border bg-background shadow-lg z-20 overflow-hidden">
              {mentionUsers.map((user, i) => (
                <button
                  key={user.id}
                  className={`w-full flex items-center gap-2 px-3 py-1.5 text-sm text-left ${
                    i === mentionIndex ? 'bg-muted' : 'hover:bg-muted/60'
                  }`}
                  onMouseDown={e => { e.preventDefault(); completeMention(user.displayName) }}
                >
                  {user.avatarUrl ? (
                    <img src={user.avatarUrl} alt={user.displayName} className="w-5 h-5 rounded-full flex-shrink-0 object-cover" />
                  ) : (
                    <div className="w-5 h-5 rounded-full bg-primary/20 flex items-center justify-center text-xs font-semibold flex-shrink-0">
                      {user.displayName.charAt(0).toUpperCase()}
                    </div>
                  )}
                  <span>{user.displayName}</span>
                  {user.isOnline && <span className="ml-auto w-2 h-2 rounded-full bg-green-400 flex-shrink-0" />}
                </button>
              ))}
            </div>
          )}

          <div onKeyDown={e => { handleMentionKeyDown(e) }}>
            <InlineMarkdownEditor
              ref={editorRef}
              value={content}
              onChange={(md) => {
                latestContentRef.current = md
                setContent(md)
                if (md.length > 0) notifyTyping()
                detectMention(md, cursorPosRef.current)
              }}
              onCursorChange={(pos) => {
                cursorPosRef.current = pos
                detectMention(latestContentRef.current, pos)
              }}
              onSubmit={submit}
              onArrowUpOnEmpty={onArrowUpOnEmpty}
              onArrowDown={onArrowDown}
              placeholder={isDisabled ? 'Reconnecting…' : 'Message (*bold* _italic_ `code` ~~strike~~)'}
              disabled={isDisabled || isUploading}
            />
          </div>
        </div>

        <button
          onClick={submit}
          disabled={!canSend}
          className="self-stretch rounded-md bg-primary px-4 text-sm text-primary-foreground
                     hover:opacity-90 disabled:opacity-50 flex items-center"
        >
          Send
        </button>
      </div>
      {content.length > MAX_LENGTH * 0.9 && (
        <p className={`text-xs text-right ${overLimit ? 'text-destructive' : 'text-muted-foreground'}`}>
          {remaining} characters remaining
        </p>
      )}
    </div>
  )
}
