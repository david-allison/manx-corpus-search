import { useReducer } from "react"
import { Route, Routes, useLocation } from "react-router-dom"
import { Layout } from "./components/Layout"
import { ErrorBoundary } from "./components/ErrorBoundary"
import { Home } from "./routes/Home"
import { DocumentView } from "./routes/DocumentView"
import { BitPlayer } from "./routes/BitPlayer"
import { NotFound } from "./routes/NotFound"
import "./custom.css"

export const App = () => {
    const [k, onRefresh] = useReducer((x) => x + 1, 1)
    const location = useLocation()

    return (
        <Layout onRefresh={onRefresh}>
            {/*keyed by path: navigating away from a crashed page clears the error*/}
            <ErrorBoundary key={location.pathname}>
                <Routes>
                    <Route path="/" element={<Home key={k} />} />
                    <Route path="/docs/:docId" element={<DocumentView />} />
                    <Route path={"/tools/youtube"} element={<BitPlayer />} />
                    <Route path="*" element={<NotFound />} />
                </Routes>
            </ErrorBoundary>
        </Layout>
    )
}

export default App
