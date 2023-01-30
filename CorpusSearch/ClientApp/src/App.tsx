import React, {useReducer} from "react"
import {
    Navigate,
    Route,
    Routes,
} from "react-router-dom"
import { Layout } from "./components/Layout"
import {HomeFC} from "./components/Home"
import { FetchDataDocument } from "./components/FetchDataDocument"
import "./custom.css"

export const App = () => {

    const [k,onRefresh] = useReducer<(num: number) => number>(x => x + 1, 1)

    return (
          <Layout onRefresh={onRefresh}>
              <Routes>
                  <Route path='/' element={<HomeFC key={k}/>} />
                  <Route path='/docs/:docId' element={<FetchDataDocument/>} />
                  <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
          </Layout>
    )
}

export default App
