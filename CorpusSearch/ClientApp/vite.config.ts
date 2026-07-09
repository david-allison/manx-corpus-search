import { defineConfig } from "vitest/config"
import react from "@vitejs/plugin-react"

// BACKEND=prod (= npm run dev:live): UI-only development against the live
// site's API - no local backend, so no waiting for corpus indexing.
const useProdBackend = process.env.BACKEND === "prod"
const liveSite = "https://corpus.gaelg.im"
// Everything the backend serves: /api and Razor View Pages
const backendPaths = [
    "/api",
    "/Browse",
    "/Dictionary",
    "/MailingList",
    "/Tags"
]

// https://vite.dev/config/
export default defineConfig({
    plugins: [react(), {
        name: "serve-root-path",
        // lets .NET verify a server on :3000 serves THIS checkout, not a stale
        // one from another branch/worktree (see ViteDevServer.cs)
        configureServer(server) {
            server.middlewares.use("/__root", (_req, res) => {
                res.setHeader("Content-Type", "text/plain")
                res.end(server.config.root)
            })
        }
    }],
    base: "/",
    build: {
        // Keep CRA's output dir so the ASP.NET RootPath / .csproj glob are unchanged
        outDir: "build"
    },
    server: {
        // .NET proxies the dev server here (see Startup.cs)
        port: 3000,
        strictPort: true,
        proxy: useProdBackend
            ? Object.fromEntries(
                backendPaths.map((path) => [
                    path,
                    { target: liveSite, changeOrigin: true }
                ])
            )
            : undefined
    },
    test: {
        environment: "happy-dom",
        // don't pick up the Playwright specs in e2e/
        include: ["src/**/*.{test,spec}.{ts,tsx}"]
    }
})
