import React, { Component } from 'react';
import {
    Route,
    Routes,
    useLocation, useNavigate
} from "react-router-dom";
import { Layout } from './components/Layout';
import { Home } from './components/Home';
import { FetchDataDocument } from './components/FetchDataDocument';
import './custom.css'

export default class App extends Component {
  static displayName = App.name;

  render () {
    return (
          <Layout>
              <Routes>
                  <Route exact path='/' element={<HomeHOC/>} />
                  <Route path='/docs/:docId' element={<FetchDataDocumentHOC/>} />
              </Routes>
          </Layout>
    );
  }
}

const HomeHOC = props => {
    const location = useLocation()
    const navigation = useNavigate()

    return <Home location={location} navigation={navigation} {...props} />
}

const FetchDataDocumentHOC = props => {
    const location = useLocation()

    return <FetchDataDocument location={location}  {...props} />
}
