import React, {useReducer} from "react"
import {
    Navigate,
    Route,
    Routes,
} from "react-router-dom"
import { Layout } from "./components/Layout"
import {Home} from "./routes/Home"
import { DocumentView } from "./routes/DocumentView"
import "./custom.css"

export const App = () => {

    const [k,onRefresh] = useReducer<(num: number) => number>(x => x + 1, 1)

    return (
          <Layout onRefresh={onRefresh}>
              <Routes>
                  <Route path='/' element={<Home key={k}/>} />
                  <Route path='/docs/:docId' element={<DocumentView/>} />
                  <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
          </Layout>
    )
}

export default App
