import { useReducer } from "react"
import { Route, Routes, useLocation } from "react-router-dom"
import { Layout } from "./components/Layout"
import { ErrorBoundary } from "./components/ErrorBoundary"
import { Home } from "./routes/Home"
import { Dictionary } from "./routes/Dictionary"
import { DictionaryBrowse } from "./routes/DictionaryBrowse"
import { DictionaryLemma } from "./routes/DictionaryLemma"
import { DocumentView } from "./routes/DocumentView"
import { BitPlayer } from "./routes/BitPlayer"
import { Contributions } from "./routes/Contributions"
import { NotFound } from "./routes/NotFound"
import { useTappableAbbrs } from "./hooks/useTappableAbbrs"
import "./custom.css"

export const App = () => {
    const [k, onRefresh] = useReducer((x) => x + 1, 1)
    const location = useLocation()
    // a printed abbreviation explains itself on hover; a tap does it for hands
    useTappableAbbrs()

    return (
        <Layout onRefresh={onRefresh}>
            {/*told the path, not keyed by it: navigating away from a crashed
               page clears the error, without remounting every page each time a
               URL changes*/}
            <ErrorBoundary resetOn={location.pathname}>
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
                    {/*:lemma is optional: bare it is the lemma index, whose
                       letter rides on ?at= — 'e' is a lemma, so a path letter
                       would shadow that word's tree*/}
                    <Route
                        path="/dictionary/lemma/:lemma?"
                        element={<DictionaryLemma />}
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
