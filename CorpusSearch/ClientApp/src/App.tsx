import React, { Component } from "react"
import {
    Route,
    Routes,
    useLocation, useMatch, useNavigate
} from "react-router-dom"
import { Layout } from "./components/Layout"
import { Home } from "./components/Home"
import { FetchDataDocument } from "./components/FetchDataDocument"
import "./custom.css"

export default class App extends Component {
  static displayName = App.name

  render () {
    return (
          <Layout>
              <Routes>
                  {/* @ts-expect-error TS(2322): Type '{ exact: true; path: string; element: Elemen... Remove this comment to see the full error message */}
                  <Route exact path='/' element={<HomeHOC/>} />
                  <Route path='/docs/:docId' element={<FetchDataDocumentHOC/>} />
              </Routes>
          </Layout>
    )
  }
}

const HomeHOC = () => {
    const location = useLocation()
    const navigation = useNavigate()

    return <Home location={location} navigation={navigation} />
}

const FetchDataDocumentHOC = () => {
    const location = useLocation()
    const match = useMatch("/docs/:docId")

    return <FetchDataDocument location={location} match={match} />
}
