import { Component, ErrorInfo, ReactNode } from "react"

type Props = {
    children: ReactNode
    /** A crash belongs to the page it happened on: when this changes, the page
     * being navigated to gets a working boundary rather than the last one's
     * error screen.
     *
     * Told, rather than keyed on. A `key` here would clear the crash by
     * remounting the whole tree beneath it — on every navigation, crash or no —
     * and that costs each page its state and its fetches every time a URL
     * changes. It is why a walk you are meant to click through blinked out of
     * the page and back between steps, and why the pickers had to cache what
     * they knew outside React to paint at all. */
    resetOn?: unknown
}
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

    componentDidUpdate(previous: Props) {
        if (
            this.state.error != null &&
            previous.resetOn !== this.props.resetOn
        ) {
            // the crash was the page we have just left: show this one
            this.setState({ error: null })
        }
    }

    render() {
        if (this.state.error == null) {
            return this.props.children
        }

        return (
            <div className="error-boundary">
                <div className="error-boundary-title">Something went wrong</div>
                <div className="error-boundary-text">
                    Try reloading the page. If it keeps happening, please{" "}
                    <a
                        href="https://github.com/david-allison/manx-corpus-search/issues"
                        target="_blank"
                        rel="noreferrer"
                    >
                        report it on GitHub
                    </a>
                    .
                </div>
                <button
                    type="button"
                    className="error-boundary-reload"
                    onClick={() => window.location.reload()}
                >
                    Reload
                </button>
            </div>
        )
    }
}
