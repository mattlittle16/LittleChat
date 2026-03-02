import { useRef, useState } from 'react'
import { useOutboxStore } from '../../stores/outboxStore'
import { getConnection } from '../../services/signalrClient'
import { getAccessToken } from '../../services/apiClient'
import { FileUploadProgress } from '../files/FileUploadProgress'

const TYPING_DEBOUNCE_MS = 500
const MAX_LENGTH = 4_000
const MAX_FILE_BYTES = 200 * 1024 * 1024 // 200 MB

interface MessageInputProps {
  roomId: string
  disabled?: boolean
}

export function MessageInput({ roomId, disabled = false }: MessageInputProps) {
  const [content, setContent] = useState('')
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [uploadProgress, setUploadProgress] = useState<number | null>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const typingTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const { enqueue, messages: outboxMessages, retryAll } = useOutboxStore()
  const hasFailed = outboxMessages.some(m => m.status === 'failed')
  const isConnected = getConnection()?.state === 'Connected'
  const isDisabled = disabled || !isConnected
  const isUploading = uploadProgress !== null

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

        <textarea
          ref={textareaRef}
          value={content}
          onChange={e => { setContent(e.target.value); notifyTyping() }}
          onKeyDown={handleKeyDown}
          disabled={isDisabled || isUploading}
          rows={1}
          placeholder={isDisabled ? 'Reconnecting…' : 'Message'}
          className="flex-1 resize-none rounded-md border bg-background px-3 py-2 text-sm
                     placeholder:text-muted-foreground focus:outline-none focus:ring-2
                     focus:ring-ring disabled:opacity-50"
          style={{ minHeight: '2.5rem', maxHeight: '10rem' }}
        />
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
