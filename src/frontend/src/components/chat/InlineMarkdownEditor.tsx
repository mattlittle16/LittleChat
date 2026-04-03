import { useEffect, useLayoutEffect, useRef, useImperativeHandle, forwardRef } from 'react'
import { useEditor, EditorContent } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Bold } from '@tiptap/extension-bold'
import { Italic } from '@tiptap/extension-italic'
import { markInputRule } from '@tiptap/core'
import { TextSelection } from '@tiptap/pm/state'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'
import { DelimiterRevealExtension } from './DelimiterRevealExtension'
import { EmojiShortcodeExtension } from './EmojiShortcodeExtension'
import { EditorEmojiSizeExtension } from './EditorEmojiSizeExtension'

// Italic: only _text_ (not *text*, which is reserved for bold like Slack)
const SlackItalic = Italic.extend({
  addInputRules() {
    return [
      markInputRule({ find: /(?:^|\s)((?:_)((?:[^_\n]+))_)$/, type: this.type }),
    ]
  },
})

// Bold: **text** (standard) and *text* (Slack-style single asterisk)
const SlackBold = Bold.extend({
  addInputRules() {
    return [
      markInputRule({ find: /(?:^|\s)((?:\*\*)((?:[^*\n]+))\*\*)$/, type: this.type }),
      markInputRule({ find: /(?:^|\s)((?:\*)((?:[^*\n]+))\*)$/, type: this.type }),
    ]
  },
})

export interface InlineMarkdownEditorRef {
  focus: () => void
  insertText: (text: string) => void
  getCursorScreenCoords: () => { top: number; bottom: number; left: number } | null
  completeShortcode: (colonPlusQueryLength: number, emoji: string) => void
}

interface Props {
  value: string
  onChange: (markdown: string) => void
  onSubmit: () => void
  onCursorChange?: (offset: number) => void
  onArrowUpOnEmpty?: () => void
  onArrowDown?: () => boolean
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
      onArrowUpOnEmpty,
      onArrowDown,
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
    const onArrowUpOnEmptyRef = useRef(onArrowUpOnEmpty)
    const onArrowDownRef = useRef(onArrowDown)
    useLayoutEffect(() => {
      onSubmitRef.current = onSubmit
      onCursorChangeRef.current = onCursorChange
      onArrowUpOnEmptyRef.current = onArrowUpOnEmpty
      onArrowDownRef.current = onArrowDown
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
          // Disable default bold/italic — replaced below with Slack-style rules
          bold: false,
          italic: false,
        }),
        SlackBold,
        SlackItalic,
        DelimiterRevealExtension,
        EmojiShortcodeExtension,
        EditorEmojiSizeExtension,
      ],
      content: markdownToHtml(value),
      autofocus: autoFocus ? 'end' : false,
      editorProps: {
        attributes: {
          class: 'outline-none',
          'data-placeholder': placeholder ?? '',
        },
        handleKeyDown(view, event) {
          if (event.key === 'ArrowUp') {
            const { state } = view
            const { selection } = state
            // Only intercept cursor (not range selections — let ProseMirror collapse those)
            if (selection.empty) {
              const $from = selection.$from
              if ($from.pos === 1) {
                // Already at the very start: trigger message navigation
                onArrowUpOnEmptyRef.current?.()
                return true
              }
              // Check if there's a hardBreak before the cursor (i.e. we're on line 2+)
              const offsetInParent = $from.parentOffset
              let offset = 0
              let onFirstLine = true
              for (let i = 0; i < $from.parent.childCount; i++) {
                const child = $from.parent.child(i)
                if (offset >= offsetInParent) break
                if (child.type.name === 'hardBreak') { onFirstLine = false; break }
                offset += child.nodeSize
              }
              if (onFirstLine) {
                // On first line but not at start: jump cursor to start
                view.dispatch(state.tr.setSelection(TextSelection.create(state.doc, 1)))
                return true
              }
            }
            // Cursor on line 2+ (or range selection): let ProseMirror move up naturally
            return false
          }
          if (event.key === 'ArrowDown') {
            // If the callback handles navigation (returns true), consume the event
            // so ProseMirror doesn't also move the cursor.
            if (onArrowDownRef.current?.()) return true
            return false
          }
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
      const wasFocused = editor.isFocused
      editor.commands.setContent(markdownToHtml(value), { emitUpdate: false })
      // setContent replaces all DOM content and blurs the editor — restore focus
      if (wasFocused) editor.commands.focus()
    }, [editor, value])

    // Keep the editor's editable state in sync with the disabled prop
    useEffect(() => {
      if (!editor) return
      editor.setEditable(!disabled)
    }, [editor, disabled])

    // Expose methods to parent components via ref
    useImperativeHandle(ref, () => ({
      focus: () => { editor?.commands.focus('end') },
      insertText: (text: string) => { editor?.chain().focus().insertContent(text).run() },
      getCursorScreenCoords: () => {
        if (!editor) return null
        const { from } = editor.state.selection
        return editor.view.coordsAtPos(from)
      },
      completeShortcode: (colonPlusQueryLength: number, emoji: string) => {
        if (!editor) return
        const { from } = editor.state.selection
        editor.chain()
          .focus()
          .deleteRange({ from: from - colonPlusQueryLength, to: from })
          .insertContent(emoji)
          .run()
      },
    }), [editor])

    const isEmpty = editor?.isEmpty ?? true

    return (
      <div
        className={`relative rounded-md border bg-background focus-within:ring-2 focus-within:ring-ring${disabled ? ' opacity-50 cursor-not-allowed' : ' cursor-text'}`}
        style={{ minHeight, maxHeight, overflowY: 'auto' }}
        onClick={(e) => {
          // Only focus-to-end when clicking empty space (the wrapper div itself),
          // not when clicking on text content — that lets ProseMirror place the cursor naturally.
          if (e.target === e.currentTarget) editor?.commands.focus('end')
        }}
      >
        <EditorContent
          editor={editor}
          className="prose dark:prose-invert max-w-none px-3 py-2"
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
