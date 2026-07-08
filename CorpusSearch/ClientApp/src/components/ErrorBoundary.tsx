import {Component, ErrorInfo, ReactNode} from "react"

type Props = { children: ReactNode }
type State = { error: Error | null }

/**
 * Displays the header and a readable crash message
 */
export class ErrorBoundary extends Component<Props, State> {
    state: State = { error: null }

    static getDerivedStateFromError(error: Error): State {
        return { error }
    }

    componentDidCatch(error: Error, errorInfo: ErrorInfo) {
        console.error("Unhandled error in component tree", error, errorInfo)
    }

    render() {
        if (this.state.error == null) {
            return this.props.children
        }

        return <div className="error-boundary">
            <div className="error-boundary-title">Something went wrong</div>
            <div className="error-boundary-text">
                Try reloading the page. If it keeps happening, please{" "}
                <a href="https://github.com/david-allison/manx-corpus-search/issues" target="_blank" rel="noreferrer">report it on GitHub</a>.
            </div>
            <button type="button" className="error-boundary-reload" onClick={() => window.location.reload()}>
                Reload
            </button>
        </div>
    }
}
