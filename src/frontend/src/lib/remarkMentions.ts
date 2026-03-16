import { visit } from 'unist-util-visit'
import type { Root, Text } from 'mdast'
import type { Plugin } from 'unified'

// Matches @username or @topic (word chars only)
const MENTION_RE = /(@(?:topic|\w+))/g

/**
 * Remark plugin that transforms @mention and @topic tokens inside paragraph
 * text nodes into inline `mention` nodes rendered as <span class="mention">.
 */
const remarkMentions: Plugin<[], Root> = () => {
  return (tree) => {
    visit(tree, 'text', (node: Text, index, parent) => {
      if (!parent || index === undefined) return
      if (!MENTION_RE.test(node.value)) return
      MENTION_RE.lastIndex = 0

      const parts: (Text | { type: 'mention'; value: string })[] = []
      let last = 0
      let match: RegExpExecArray | null

      while ((match = MENTION_RE.exec(node.value)) !== null) {
        if (match.index > last) {
          parts.push({ type: 'text', value: node.value.slice(last, match.index) })
        }
        parts.push({ type: 'mention', value: match[1] })
        last = match.index + match[1].length
      }

      if (last < node.value.length) {
        parts.push({ type: 'text', value: node.value.slice(last) })
      }

      if (parts.length > 1) {
        parent.children.splice(index, 1, ...(parts as never[]))
        return index + parts.length
      }
    })
  }
}

export default remarkMentions
