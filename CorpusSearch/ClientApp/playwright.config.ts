import { defineConfig, devices } from "@playwright/test"
import { fileURLToPath } from "node:url"

// End-to-end tests (see e2e/): boot the real ASP.NET server against a small committed
// fixture corpus (e2e/fixtures/corpus - not named 'OpenData': due to .gitignore) and
// assert on highlighting in the real UI.
//
// `dotnet run` in Development auto-spawns the Vite dev server (or reuses one already
// serving this checkout) and proxies to it (see Startup.cs), so one command serves
// both the API and the SPA.
//
// Set E2E_PORT to run alongside a dev server already occupying port 5000.
const port = process.env.E2E_PORT ?? "5000"

export default defineConfig({
    testDir: "./e2e",
    timeout: 30_000,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 1 : 0,
    reporter: process.env.CI ? "github" : "list",
    projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
    use: {
        baseURL: `http://localhost:${port}`,
        trace: "on-first-retry",
    },
    webServer: {
        command: "dotnet run --no-launch-profile",
        // the server spawns Vite relative to its working directory (see ViteDevServer.cs)
        cwd: fileURLToPath(new URL("..", import.meta.url)),
        url: `http://localhost:${port}`,
        reuseExistingServer: !process.env.CI,
        // dotnet build + corpus indexing + first Vite boot
        timeout: 300_000,
        env: {
            ASPNETCORE_ENVIRONMENT: "Development",
            ASPNETCORE_URLS: `http://localhost:${port}`,
            // load only the fixture corpus, quickly
            Loading__OpenDataOnly: "true",
            Loading__OpenDataPath: fileURLToPath(
                new URL("./e2e/fixtures/corpus", import.meta.url),
            ),
        },
    },
})
