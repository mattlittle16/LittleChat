/// <reference types="vite/client" />

declare const __APP_VERSION__: string

// Custom remark node type for @mention / @topic highlighting
declare namespace React {
  namespace JSX {
    interface IntrinsicElements {
      mention: { node?: { value?: string }; children?: React.ReactNode }
    }
  }
}
