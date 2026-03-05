import { useRef, useState, useEffect } from 'react'
import ReactMarkdown from 'react-markdown'
import { useOutboxStore } from '../../stores/outboxStore'
import { getConnection } from '../../services/signalrClient'
import { getAccessToken, api } from '../../services/apiClient'
import { FileUploadProgress } from '../files/FileUploadProgress'
import type { UserSearchResult } from '../../types'

const TYPING_DEBOUNCE_MS = 500
const MAX_LENGTH = 4_000
const MAX_FILE_BYTES = 200 * 1024 * 1024 // 200 MB

interface MessageInputProps {
  roomId: string
  disabled?: boolean
}

export function MessageInput({ roomId, disabled = false }: MessageInputProps) {
  const [content, setContent] = useState('')
  const [preview, setPreview] = useState(false)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [uploadProgress, setUploadProgress] = useState<number | null>(null)
  const [mentionQuery, setMentionQuery] = useState<string | null>(null)
  const [mentionUsers, setMentionUsers] = useState<UserSearchResult[]>([])
  const [mentionIndex, setMentionIndex] = useState(0)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const typingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const { enqueue, messages: outboxMessages, retryAll } = useOutboxStore()
  const hasFailed = outboxMessages.some(m => m.status === 'failed')
  const isConnected = getConnection()?.state === 'Connected'
  const isDisabled = disabled || !isConnected
  const isUploading = uploadProgress !== null

  // Fetch users when an active mention query changes
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
    const cursorPos = textareaRef.current?.selectionStart ?? content.length
    const before = content.slice(0, cursorPos)
    const after = content.slice(cursorPos)
    const newBefore = before.replace(/@\w*$/, `@${displayName} `)
    setContent(newBefore + after)
    setMentionQuery(null)
    setMentionUsers([])
    setTimeout(() => {
      textareaRef.current?.setSelectionRange(newBefore.length, newBefore.length)
      textareaRef.current?.focus()
    }, 0)
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

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (mentionQuery !== null && mentionUsers.length > 0) {
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        setMentionIndex(i => (i + 1) % mentionUsers.length)
        return
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault()
        setMentionIndex(i => (i - 1 + mentionUsers.length) % mentionUsers.length)
        return
      }
      if (e.key === 'Enter' || e.key === 'Tab') {
        e.preventDefault()
        completeMention(mentionUsers[mentionIndex].displayName)
        return
      }
      if (e.key === 'Escape') {
        setMentionQuery(null)
        setMentionUsers([])
        return
      }
    }
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      submit()
    }
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0] ?? null
    if (!file) return
    if (file.size > MAX_FILE_BYTES) {
      alert(`File exceeds the 200 MB limit.`)
      e.target.value = ''
      return
    }
    setSelectedFile(file)
    e.target.value = ''
  }

  function clearFile() {
    setSelectedFile(null)
    setUploadProgress(null)
  }

  async function submitWithFile(file: File) {
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > MAX_LENGTH) return

    const token = getAccessToken()
    const formData = new FormData()
    formData.append('id', crypto.randomUUID())
    formData.append('content', trimmed)
    formData.append('file', file)

    setContent('')
    clearFile()
    setUploadProgress(0)

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
        if (xhr.status >= 200 && xhr.status < 300) resolve()
        else reject(new Error(`Upload failed: ${xhr.status}`))
      }

      xhr.onerror = () => {
        setUploadProgress(null)
        reject(new Error('Upload error'))
      }

      xhr.send(formData)
    }).catch(() => {
      // Non-fatal — user can retry
    })

    textareaRef.current?.focus()
  }

  async function submit() {
    if (selectedFile) {
      await submitWithFile(selectedFile)
      return
    }
    const trimmed = content.trim()
    if (!trimmed || trimmed.length > MAX_LENGTH) return
    setContent('')
    await enqueue(roomId, trimmed)
    textareaRef.current?.focus()
  }

  const remaining = MAX_LENGTH - content.length
  const overLimit = remaining < 0

  return (
    <div className="border-t p-3 flex flex-col gap-2">
      {hasFailed && (
        <div className="flex items-center gap-2 text-sm text-destructive">
          <span>Some messages failed to send.</span>
          <button onClick={retryAll} className="underline hover:no-underline">
            Retry all
          </button>
        </div>
      )}

      {/* File preview chip */}
      {selectedFile && uploadProgress === null && (
        <div className="flex items-center gap-1 text-xs text-muted-foreground">
          <span className="truncate max-w-[200px]">📎 {selectedFile.name}</span>
          <span className="text-muted-foreground/60">
            ({(selectedFile.size / 1024).toFixed(0)} KB)
          </span>
          <button
            onClick={clearFile}
            className="ml-1 rounded hover:bg-muted/60 px-1"
            aria-label="Remove file"
          >
            ×
          </button>
        </div>
      )}

      {/* Upload progress bar */}
      {isUploading && selectedFile && (
        <FileUploadProgress fileName={selectedFile.name} progress={uploadProgress!} />
      )}

      <div className="flex items-end gap-2">
        {/* Hidden file input */}
        <input
          ref={fileInputRef}
          type="file"
          className="hidden"
          onChange={handleFileChange}
        />

        {/* Clip button */}
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={isDisabled || isUploading}
          title="Attach file"
          className="rounded-md border px-2 py-2 text-muted-foreground hover:bg-muted/60
                     disabled:opacity-50 flex-shrink-0"
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

          {/* Preview/edit toggle */}
          <div className="flex gap-1 mb-1">
            <button
              type="button"
              onClick={() => setPreview(false)}
              className={`text-xs px-2 py-0.5 rounded ${!preview ? 'bg-muted font-medium' : 'text-muted-foreground hover:text-foreground'}`}
            >
              Write
            </button>
            <button
              type="button"
              onClick={() => setPreview(true)}
              disabled={!content.trim()}
              className={`text-xs px-2 py-0.5 rounded disabled:opacity-40 ${preview ? 'bg-muted font-medium' : 'text-muted-foreground hover:text-foreground'}`}
            >
              Preview
            </button>
          </div>

          {preview ? (
            <div
              className="min-h-[2.5rem] max-h-40 overflow-y-auto rounded-md border bg-background
                         px-3 py-2 text-sm prose prose-sm dark:prose-invert max-w-none"
            >
              <ReactMarkdown>{content}</ReactMarkdown>
            </div>
          ) : (
            <textarea
              ref={textareaRef}
              value={content}
              onChange={e => {
                setContent(e.target.value)
                notifyTyping()
                detectMention(e.target.value, e.target.selectionStart ?? e.target.value.length)
              }}
              onKeyDown={handleKeyDown}
              disabled={isDisabled || isUploading}
              rows={1}
              placeholder={isDisabled ? 'Reconnecting…' : 'Message (supports **markdown**)'}
              className="w-full resize-none rounded-md border bg-background px-3 py-2 text-sm
                         placeholder:text-muted-foreground focus:outline-none focus:ring-2
                         focus:ring-ring disabled:opacity-50"
              style={{ minHeight: '2.5rem', maxHeight: '10rem' }}
            />
          )}
        </div>

        <button
          onClick={submit}
          disabled={isDisabled || isUploading || (!content.trim() && !selectedFile) || overLimit}
          className="rounded-md bg-primary px-4 py-2 text-sm text-primary-foreground
                     hover:opacity-90 disabled:opacity-50"
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
