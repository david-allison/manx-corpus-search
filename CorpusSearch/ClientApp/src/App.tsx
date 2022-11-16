import React, { Component } from "react"
import {
    Route,
    Routes,
    useLocation, useMatch
} from "react-router-dom"
import { Layout } from "./components/Layout"
import {HomeFC} from "./components/Home"
import { FetchDataDocument } from "./components/FetchDataDocument"
import "./custom.css"

export default class App extends Component {
  static displayName = App.name

  render () {
    return (
          <Layout>
              <Routes>
                  {/* @ts-expect-error TS(2322): Type '{ exact: true; path: string; element: Elemen... Remove this comment to see the full error message */}
                  <Route exact path='/' element={<HomeFC/>} />
                  <Route path='/docs/:docId' element={<FetchDataDocumentHOC/>} />
              </Routes>
          </Layout>
    )
  }
}

const FetchDataDocumentHOC = () => {
    const location = useLocation()
    const match = useMatch("/docs/:docId")

    return <FetchDataDocument location={location} match={match} />
}
