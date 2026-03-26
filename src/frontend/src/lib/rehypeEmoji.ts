import { visit } from 'unist-util-visit'
import type { Root, Text, Element } from 'hast'
import type { Plugin } from 'unified'

// Matches emoji including ZWJ sequences (e.g. 👨‍👩‍👧) and variation selectors
const EMOJI_RE = /\p{Extended_Pictographic}(?:\uFE0F)?(?:\u20E3)?(?:\u200D\p{Extended_Pictographic}(?:\uFE0F)?(?:\u20E3)?)*/gu

/**
 * Detects whether a markdown string contains only emoji characters (and whitespace).
 * Used to apply a larger font size to emoji-only messages.
 */
export function isEmojiOnly(content: string): boolean {
  const trimmed = content.trim()
  if (!trimmed) return false
  const stripped = trimmed.replace(EMOJI_RE, '').trim()
  return stripped.length === 0
}

/**
 * Rehype plugin that wraps emoji characters in <span class="chat-emoji"> so
 * they can be sized independently via CSS.
 */
const rehypeEmoji: Plugin<[], Root> = () => {
  return (tree) => {
    visit(tree, 'text', (node: Text, index, parent) => {
      if (!parent || index === undefined) return

      EMOJI_RE.lastIndex = 0
      if (!EMOJI_RE.test(node.value)) return
      EMOJI_RE.lastIndex = 0

      const parts: (Text | Element)[] = []
      let last = 0
      let match: RegExpExecArray | null

      while ((match = EMOJI_RE.exec(node.value)) !== null) {
        if (match.index > last) {
          parts.push({ type: 'text', value: node.value.slice(last, match.index) })
        }
        parts.push({
          type: 'element',
          tagName: 'span',
          properties: { className: ['chat-emoji'] },
          children: [{ type: 'text', value: match[0] }],
        } as Element)
        last = match.index + match[0].length
      }

      if (last < node.value.length) {
        parts.push({ type: 'text', value: node.value.slice(last) })
      }

      // Always splice when we found emoji — even a single-emoji node needs the
      // text node replaced with a span element node.
      parent.children.splice(index, 1, ...(parts as never[]))
      return index + parts.length
    })
  }
}

export default rehypeEmoji
