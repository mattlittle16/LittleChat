import { Extension } from '@tiptap/core'
import { Plugin, PluginKey } from '@tiptap/pm/state'
import { Decoration, DecorationSet } from '@tiptap/pm/view'
import type { Node as ProseMirrorNode } from '@tiptap/pm/model'

const DELIMITERS: Record<string, [string, string]> = {
  bold:   ['**', '**'],
  italic: ['_',  '_'],
  code:   ['`',  '`'],
  strike: ['~~', '~~'],
}

const pluginKey = new PluginKey<DecorationSet>('delimiterReveal')

/**
 * For each mark type that the cursor overlaps, finds the full contiguous extent
 * of that mark in the document (adjacent text nodes with the same mark are merged)
 * and adds widget decorations showing the delimiter characters at the mark boundaries.
 */
function buildDecorations(doc: ProseMirrorNode, cursorFrom: number): DecorationSet {
  const decorations: Decoration[] = []
  const processedMarkTypes = new Set<string>()

  doc.descendants((node, pos) => {
    if (!node.isText) return

    const nodeFrom = pos
    const nodeTo   = pos + node.nodeSize

    // Only process nodes that contain the cursor
    if (cursorFrom < nodeFrom || cursorFrom > nodeTo) return

    node.marks.forEach(mark => {
      if (processedMarkTypes.has(mark.type.name)) return
      const delims = DELIMITERS[mark.type.name]
      if (!delims) return

      processedMarkTypes.add(mark.type.name)

      // Expand backward and forward to find the full contiguous mark extent.
      // doc.descendants visits nodes in document order, so adjacent nodes with the
      // same mark will be contiguous in traversal.
      let markFrom = nodeFrom
      let markTo   = nodeTo

      doc.descendants((n, p) => {
        if (!n.isText) return
        if (!n.marks.some(m => m.type.name === mark.type.name)) return
        if (p + n.nodeSize === markFrom) markFrom = p      // adjacent before
        if (p === markTo)               markTo   = p + n.nodeSize  // adjacent after
      })

      const makeWidget = (text: string) => (): HTMLElement => {
        const span = document.createElement('span')
        span.className = 'mk-delim'
        span.textContent = text
        return span
      }

      decorations.push(
        Decoration.widget(markFrom, makeWidget(delims[0]), { side: -1, key: `${mark.type.name}-open` }),
        Decoration.widget(markTo,   makeWidget(delims[1]), { side:  1, key: `${mark.type.name}-close` }),
      )
    })
  })

  return DecorationSet.create(doc, decorations)
}

/**
 * Tiptap extension that adds ProseMirror Decoration widgets showing raw markdown
 * delimiter characters (e.g. ** for bold, ` for code) when the cursor is inside
 * a formatted inline mark region.
 */
export const DelimiterRevealExtension = Extension.create({
  name: 'delimiterReveal',

  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: pluginKey,
        state: {
          init: (_, { doc, selection }) => buildDecorations(doc, selection.from),
          apply(tr, oldDecorations) {
            if (!tr.selectionSet && !tr.docChanged) return oldDecorations
            return buildDecorations(tr.doc, tr.selection.from)
          },
        },
        props: {
          decorations(state) {
            return pluginKey.getState(state)
          },
        },
      }),
    ]
  },
})
