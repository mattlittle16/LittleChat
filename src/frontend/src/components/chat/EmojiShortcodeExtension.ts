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
        handler({ state, range, match }) {
          const emoji = emojiMap[match[1]]
          if (!emoji) return null
          const { tr } = state
          tr.replaceWith(range.from, range.to, state.schema.text(emoji + ' '))
        },
      }),
    ]
  },
})
