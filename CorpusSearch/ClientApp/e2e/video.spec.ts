import { test, expect } from "@playwright/test"

// The video document layout: the player docks to the top of the viewport and the
// transcript scrolls beneath it as normal page content (no nested scrollbox).
// These assertions are about our layout, not YouTube's iframe, so they hold even
// when the embed cannot load.

const DOC = "/docs/e2e-video-fixture"

test.describe("video document", () => {
    test("the player docks while the transcript scrolls", async ({ page }) => {
        await page.goto(DOC)

        const dock = page.locator(".video-dock")
        await expect(dock).toBeVisible()
        await expect(dock).toHaveCSS("position", "sticky")

        // the transcript scrolls with the page, not inside its own scrollbox
        const table = page.locator(".doc-table")
        await expect(table).not.toHaveCSS("overflow-y", "scroll")

        // scrolled deep into the transcript, the player is still on screen,
        // pinned to the top of the viewport
        await page.evaluate(() =>
            window.scrollTo(0, document.body.scrollHeight),
        )
        await expect
            .poll(async () => Math.abs((await dock.boundingBox())?.y ?? 999))
            .toBeLessThan(1)
    })

    test("mobile: the per-line edit column makes way for the text", async ({
        page,
    }) => {
        await page.setViewportSize({ width: 390, height: 844 })
        await page.goto(DOC)

        await expect(page.locator(".doc-table")).toBeVisible()
        // the fixture has speakers, so the column set is play/speaker/gv/en
        await expect(page.locator(".doc-th-speaker")).toBeVisible()
        await expect(page.locator(".doc-th-link")).toBeHidden()
    })
})
