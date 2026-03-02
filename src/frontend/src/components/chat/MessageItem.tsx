import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter'
import { oneDark } from 'react-syntax-highlighter/dist/esm/styles/prism'
import type { Message } from '../../types'
import type { OutboxMessage } from '../../types'

interface MessageItemProps {
  message: Message | OutboxMessage
  isPending?: boolean
}

function isOutbox(m: Message | OutboxMessage): m is OutboxMessage {
  return 'clientId' in m
}

function formatTime(isoOrTs: string | number): string {
  const date = typeof isoOrTs === 'number' ? new Date(isoOrTs) : new Date(isoOrTs)
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

export function MessageItem({ message, isPending = false }: MessageItemProps) {
  if (isOutbox(message)) {
    return (
      <div className={`flex gap-3 px-4 py-1 ${message.status === 'failed' ? 'opacity-60' : 'opacity-50'}`}>
        <div className="w-8 h-8 rounded-full bg-muted flex-shrink-0" />
        <div className="flex-1 min-w-0">
          <div className="flex items-baseline gap-2">
            <span className="text-sm font-semibold">You</span>
            <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
            <span className="text-xs text-muted-foreground">
              {message.status === 'sending' ? '· Sending…' : message.status === 'failed' ? '· Failed' : '· Pending'}
            </span>
          </div>
          <p className="text-sm mt-0.5 whitespace-pre-wrap break-words">{message.content}</p>
        </div>
      </div>
    )
  }

  return (
    <div className={`flex gap-3 px-4 py-1 hover:bg-muted/40 ${isPending ? 'opacity-60' : ''}`}>
      <div className="flex-shrink-0">
        {message.author.avatarUrl ? (
          <img
            src={message.author.avatarUrl}
            alt={message.author.displayName}
            className="w-8 h-8 rounded-full object-cover"
          />
        ) : (
          <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center text-sm font-semibold">
            {message.author.displayName.charAt(0).toUpperCase()}
          </div>
        )}
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          <span className="text-sm font-semibold">{message.author.displayName}</span>
          <span className="text-xs text-muted-foreground">{formatTime(message.createdAt)}</span>
          {message.editedAt && (
            <span className="text-xs text-muted-foreground">(edited)</span>
          )}
        </div>
        <div className="prose prose-sm dark:prose-invert max-w-none mt-0.5">
          <ReactMarkdown
            remarkPlugins={[remarkGfm]}
            components={{
              code({ className, children, ...props }) {
                const match = /language-(\w+)/.exec(className ?? '')
                const isBlock = !props.ref && match
                if (isBlock) {
                  return (
                    <SyntaxHighlighter
                      style={oneDark}
                      language={match[1]}
                      PreTag="div"
                    >
                      {String(children).replace(/\n$/, '')}
                    </SyntaxHighlighter>
                  )
                }
                return (
                  <code className={className} {...props}>
                    {children}
                  </code>
                )
              },
            }}
          >
            {message.content}
          </ReactMarkdown>
        </div>
        {message.attachment && (
          <a
            href={message.attachment.url}
            download={message.attachment.fileName}
            className="mt-1 inline-flex items-center gap-1 text-xs text-primary hover:underline"
          >
            📎 {message.attachment.fileName}
            <span className="text-muted-foreground">
              ({(message.attachment.fileSize / 1024).toFixed(1)} KB)
            </span>
          </a>
        )}
      </div>
    </div>
  )
}
