import { Link } from "react-router-dom"

// The 404 page. In production, unknown URLs get this via SpaRouteGuard.cs, which
// serves the app shell with a 404 status; in-app navigation and Development reach
// it through the "*" route directly.
export const NotFound = () => (
    <>
        <h1 className="page-title">Page not found</h1>
        <p>
            This page doesn&apos;t exist. Try the{" "}
            <Link to="/">search page</Link> or the{" "}
            <a href="/Browse">full document listing</a>.
        </p>
    </>
)
