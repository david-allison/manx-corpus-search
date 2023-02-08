import "bootstrap/dist/css/bootstrap.css"
import React from "react"
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from "react-router-dom"
import App from "./App"
import * as serviceWorkerRegistration from "./serviceWorkerRegistration";

const baseUrl = document.getElementsByTagName("base")[0].getAttribute("href") ?? undefined
const rootElement = document.getElementById("root")

const root = createRoot(rootElement!); // createRoot(container!) if you use TypeScript
root.render(   
    <BrowserRouter basename={baseUrl}>
    <App />
</BrowserRouter>);

serviceWorkerRegistration.unregister()

