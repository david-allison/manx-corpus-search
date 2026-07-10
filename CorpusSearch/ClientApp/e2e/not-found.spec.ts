import { test, expect } from "@playwright/test"

// The SPA's catch-all renders a brief 404 instead of redirecting home.
// In production the server serves the shell for unknown URLs with a 404 status
// (SpaRouteGuard.cs), so this same page renders there too.
test("an unknown route shows the not-found page", async ({ page }) => {
    await page.goto("/this-page-does-not-exist")
    await expect(
        page.getByRole("heading", { name: "Page not found" }),
    ).toBeVisible()
    await expect(page.getByRole("link", { name: "search page" })).toBeVisible()
    await expect(
        page.getByRole("link", { name: "full document listing" }),
    ).toBeVisible()
})
