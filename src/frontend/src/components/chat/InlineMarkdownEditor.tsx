import { useEffect, useLayoutEffect, useRef, useImperativeHandle, forwardRef } from 'react'
import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { DelimiterRevealExtension } from './DelimiterRevealExtension'

export interface InlineMarkdownEditorRef {
  focus: () => void
}

interface Props {
  value: string
  onChange: (markdown: string) => void
  onSubmit: () => void
  onCursorChange?: (offset: number) => void
  placeholder?: string
  disabled?: boolean
  minHeight?: string
  maxHeight?: string
  autoFocus?: boolean
}

// Convert stored raw markdown to HTML for Tiptap to parse marks correctly.
// Only handles the 4 supported inline patterns.
function markdownToHtml(md: string): string {
  if (!md) return ''
  return md
    .replace(/`([^`\n]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>')
    .replace(/~~([^~\n]+)~~/g, '<s>$1</s>')
    .replace(/(?<![_\w])_([^_\n]+)_(?![_\w])/g, '<em>$1</em>')
    .replace(/\n/g, '<br>')
}

// Serialize the ProseMirror document back to raw markdown text for storage/send.
function serializeToMarkdown(doc: ProseMirrorNode): string {
  const parts: string[] = []
  doc.forEach(blockNode => {
    let block = ''
    blockNode.forEach(child => {
      if (child.type.name === 'hardBreak') {
        block += '\n'
      } else if (child.isText) {
        let text = child.text ?? ''
        const marks = child.marks.map(m => m.type.name)
        // Apply innermost-first: code > bold > italic > strike
        if (marks.includes('code'))   text = `\`${text}\``
        if (marks.includes('bold'))   text = `**${text}**`
        if (marks.includes('italic')) text = `_${text}_`
        if (marks.includes('strike')) text = `~~${text}~~`
        block += text
      }
    })
    parts.push(block)
  })
  return parts.join('\n')
}

export const InlineMarkdownEditor = forwardRef<InlineMarkdownEditorRef, Props>(
  function InlineMarkdownEditor(
    {
      value,
      onChange,
      onSubmit,
      onCursorChange,
      placeholder,
      disabled = false,
      minHeight = '4.5rem',
      maxHeight = '15rem',
      autoFocus = false,
    },
    ref,
  ) {
    // Use refs for callbacks to avoid stale closures in editor event handlers
    const onSubmitRef = useRef(onSubmit)
    const onCursorChangeRef = useRef(onCursorChange)
    useLayoutEffect(() => {
      onSubmitRef.current = onSubmit
      onCursorChangeRef.current = onCursorChange
    })

    // Track the last markdown we serialized so the sync effect doesn't loop
    const lastSerializedRef = useRef(value)

    const editor = useEditor({
      extensions: [
        StarterKit.configure({
          // Disable block-level extensions — this is an inline-only compose box
          heading: false,
          blockquote: false,
          codeBlock: false,
          horizontalRule: false,
          bulletList: false,
          orderedList: false,
          listItem: false,
          // hardBreak kept enabled: Shift+Enter inserts a line break within the message
        }),
        DelimiterRevealExtension,
      ],
      content: markdownToHtml(value),
      autofocus: autoFocus ? 'end' : false,
      editorProps: {
        attributes: {
          class: 'outline-none',
          'data-placeholder': placeholder ?? '',
        },
        handleKeyDown(_view, event) {
          if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault()
            onSubmitRef.current()
            return true
          }
          return false
        },
      },
      onUpdate({ editor }) {
        const md = serializeToMarkdown(editor.state.doc)
        lastSerializedRef.current = md
        onChange(md)
        onCursorChangeRef.current?.(editor.state.selection.from - 1)
      },
      onSelectionUpdate({ editor }) {
        onCursorChangeRef.current?.(editor.state.selection.from - 1)
      },
    })

    // Sync external value changes into the editor (e.g. parent clears content after send)
    useEffect(() => {
      if (!editor) return
      if (value === lastSerializedRef.current) return
      lastSerializedRef.current = value
      editor.commands.setContent(markdownToHtml(value), { emitUpdate: false })
    }, [editor, value])

    // Keep the editor's editable state in sync with the disabled prop
    useEffect(() => {
      if (!editor) return
      editor.setEditable(!disabled)
    }, [editor, disabled])

    // Expose a focus() method to parent components via ref
    useImperativeHandle(ref, () => ({
      focus: () => { editor?.commands.focus('end') },
    }), [editor])

    const isEmpty = editor?.isEmpty ?? true

    return (
      <div
        className={`relative rounded-md border bg-background text-sm focus-within:ring-2 focus-within:ring-ring${disabled ? ' opacity-50 cursor-not-allowed' : ''}`}
        style={{ minHeight, maxHeight, overflowY: 'auto' }}
      >
        <EditorContent
          editor={editor}
          className="prose prose-sm dark:prose-invert max-w-none px-3 py-2"
        />
        {isEmpty && placeholder && (
          <span className="pointer-events-none absolute inset-0 px-3 py-2 text-muted-foreground select-none">
            {placeholder}
          </span>
        )}
        <style>{`
          .mk-delim {
            color: hsl(var(--muted-foreground));
            opacity: 0.7;
            font-weight: normal;
            font-style: normal;
            text-decoration: none;
          }
          .ProseMirror code {
            background: hsl(var(--muted));
            border: 1px solid hsl(var(--border));
            border-radius: 4px;
            padding: 0.1em 0.35em;
            font-size: 0.85em;
            font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
            color: hsl(var(--foreground));
          }
        `}</style>
      </div>
    )
  },
)
