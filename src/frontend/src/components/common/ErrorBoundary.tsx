import { Component } from 'react'
import type { ReactNode, ErrorInfo } from 'react'

interface Props {
  children: ReactNode
  name?: string
  fallback?: ReactNode
}

interface State {
  hasError: boolean
  error: Error | null
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props)
    this.state = { hasError: false, error: null }
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(`[ErrorBoundary${this.props.name ? ` — ${this.props.name}` : ''}]`, error, info.componentStack)
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null })
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback

      return (
        <div className="flex flex-col items-center justify-center gap-3 p-6 text-center text-sm text-muted-foreground">
          <span className="text-base font-medium text-foreground">
            {this.props.name ? `${this.props.name} failed to load` : 'Something went wrong'}
          </span>
          <span className="text-xs max-w-xs">
            {this.state.error?.message ?? 'An unexpected error occurred.'}
          </span>
          <button
            onClick={this.handleReset}
            className="rounded-md border px-3 py-1.5 text-xs hover:bg-muted/60 transition-colors"
          >
            Try again
          </button>
        </div>
      )
    }

    return this.props.children
  }
}
