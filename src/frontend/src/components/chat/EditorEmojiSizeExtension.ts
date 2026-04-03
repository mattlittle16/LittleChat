import { Extension } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { Decoration, DecorationSet } from '@tiptap/pm/view'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'

// Matches a single Extended_Pictographic code point (covers essentially all emoji).
// Followed by an optional variation selector (FE0F) or skin-tone modifier.
const EMOJI_RE = /\p{Extended_Pictographic}[\u{1F3FB}-\u{1F3FF}]?(?:\uFE0F)?/gu

const pluginKey = new PluginKey<DecorationSet>('editorEmojiSize')

function buildDecorations(doc: ProseMirrorNode): DecorationSet {
  const decorations: Decoration[] = []

  doc.descendants((node, pos) => {
    if (!node.isText || !node.text) return
    EMOJI_RE.lastIndex = 0
    let match: RegExpExecArray | null
    while ((match = EMOJI_RE.exec(node.text)) !== null) {
      const from = pos + match.index
      const to = from + match[0].length
      decorations.push(Decoration.inline(from, to, { class: 'editor-emoji' }))
    }
  })

  return DecorationSet.create(doc, decorations)
}

/**
 * Wraps each emoji character in the editor with a span that bumps its font size,
 * so emoji are visually larger than the surrounding text without changing the
 * overall editor font size.
 */
export const EditorEmojiSizeExtension = Extension.create({
  name: 'editorEmojiSize',

  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: pluginKey,
        state: {
          init: (_, { doc }) => buildDecorations(doc),
          apply(tr, old) {
            return tr.docChanged ? buildDecorations(tr.doc) : old
          },
        },
        props: {
          decorations(state) { return pluginKey.getState(state) },
        },
      }),
    ]
  },
})
