import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"

// https://vite.dev/config/
export default defineConfig({
    plugins: [react()],
    base: "/",
    build: {
        // Keep CRA's output dir so the ASP.NET RootPath / .csproj glob are unchanged
        outDir: "build"
    },
    server: {
        // .NET proxies the dev server here (see Startup.cs)
        port: 3000,
        strictPort: true
    }
})
