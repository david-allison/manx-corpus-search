import { createRoot } from "react-dom/client"
import { BrowserRouter } from "react-router-dom"
import App from "./App"

const baseUrl = import.meta.env.BASE_URL
const rootElement = document.getElementById("root")

const root = createRoot(rootElement!)
root.render(
    <BrowserRouter basename={baseUrl}>
        <App />
    </BrowserRouter>,
)
