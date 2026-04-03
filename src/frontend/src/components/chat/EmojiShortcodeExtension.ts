import { Extension, InputRule } from '@tiptap/core'
import { emojiMap } from './emojiMap'

// Converts :shortcode: followed by a space into the corresponding emoji.
// e.g. typing ":lol: " becomes "😂 "
export const EmojiShortcodeExtension = Extension.create({
  name: 'emojiShortcode',

  addInputRules() {
    return [
      new InputRule({
        // Matches :shortcode: followed by a space at end of input
        // Shortcodes may contain word chars, +, or -  (e.g. :+1:, :-1:)
        find: /:([\w+-]+): $/,
        // Arrow function so `this` refers to the extension (gives us this.editor)
        handler: ({ state, range, match }) => {
          const emoji = emojiMap[match[1]]
          if (!emoji) return null
          const { tr } = state
          tr.replaceWith(range.from, range.to, state.schema.text(emoji + ' '))
          // After the transaction is dispatched, React may re-render and blur the
          // editor. Restore focus in the next task so it runs after any re-render.
          setTimeout(() => this.editor.commands.focus(), 0)
        },
      }),
    ]
  },
})
