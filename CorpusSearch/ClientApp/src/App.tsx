import { useReducer } from "react"
import { Route, Routes, useLocation } from "react-router-dom"
import { Layout } from "./components/Layout"
import { ErrorBoundary } from "./components/ErrorBoundary"
import { Home } from "./routes/Home"
import { Dictionary } from "./routes/Dictionary"
import { DictionaryBrowse } from "./routes/DictionaryBrowse"
import { DocumentView } from "./routes/DocumentView"
import { BitPlayer } from "./routes/BitPlayer"
import { Contributions } from "./routes/Contributions"
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
                    {/*experimental: the teanglann-style dictionary page. Any
                       sub-route added here must also be added to
                       Infrastructure/SpaRouteGuard.cs, or it 404s in production*/}
                    <Route path="/dictionary/:word?" element={<Dictionary />} />
                    <Route
                        path="/dictionary/in/:dict/:word"
                        element={<Dictionary />}
                    />
                    {/*:dict is required: an optional one makes
                       /dictionary/browse/aa ambiguous with the letter*/}
                    <Route
                        path="/dictionary/browse/:dict/:at?"
                        element={<DictionaryBrowse />}
                    />
                    <Route path="/docs/:docId" element={<DocumentView />} />
                    <Route path={"/tools/youtube"} element={<BitPlayer />} />
                    <Route path="/contributions" element={<Contributions />} />
                    <Route path="*" element={<NotFound />} />
                </Routes>
            </ErrorBoundary>
        </Layout>
    )
}

export default App
