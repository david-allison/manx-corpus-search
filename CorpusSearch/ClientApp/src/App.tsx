import React, { Component } from "react"
import {
    Navigate,
    Route,
    Routes,
} from "react-router-dom"
import { Layout } from "./components/Layout"

const HomeFC = React.lazy(() => import("./components/Home"))
const FetchDataDocument = React.lazy(() => import("./components/FetchDataDocument"))
import "./custom.css"

export default class App extends Component {
  static displayName = App.name

  render () {
    return (
          <Layout>
              <React.Suspense fallback={<></>}>
                  <Routes>
                      <Route path='/' element={<HomeFC/>} />
                      <Route path='/docs/:docId' element={<FetchDataDocument/>} />
                      <Route path="*" element={<Navigate to="/" replace />} />
                  </Routes>
              </React.Suspense>
          </Layout>
    )
  }
}
