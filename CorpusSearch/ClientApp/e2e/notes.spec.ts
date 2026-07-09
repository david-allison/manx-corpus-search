import { test, expect } from "@playwright/test"

// End-to-end coverage of #132: notes are hidden while reading, each collapsed behind
// the "[1]" marker in its line; a note with no marker in the text never hides.

const DOC = "/docs/e2e-notes-fixture"
const linkedNote = "‘headland on the Calf of Man’"
const unlinkedNote = "An editorial note with no marker in the text."
const marker = ".doc-note-marker"

test.describe("note rows (#132)", () => {
    test("notes hide by default; an unlinked note shows as-is", async ({
        page,
    }) => {
        await page.goto(DOC)
        // a note with no marker cannot be revealed, so it is never hidden
        await expect(page.getByText(unlinkedNote)).toBeVisible()
        // the linked note is collapsed behind its marker
        await expect(page.locator(marker)).toBeVisible()
        await expect(page.getByText(linkedNote)).toHaveCount(0)
    })

    test("the [1] marker reveals its note and hides it again", async ({
        page,
    }) => {
        await page.goto(DOC)
        await page.locator(marker).click()
        await expect(page.getByText(linkedNote)).toBeVisible()

        await page.locator(marker).click()
        await expect(page.getByText(linkedNote)).toHaveCount(0)
    })

    test("'Show notes' shows every note, and persists", async ({ page }) => {
        await page.goto(DOC)
        await page.locator("summary", { hasText: "Advanced options" }).click()
        await page.getByLabel("Show notes").check()

        await expect(page.getByText(linkedNote)).toBeVisible()
        await expect(page.locator(".noteRow")).toHaveCount(2)

        // the marker can still collapse an individual note
        await page.locator(marker).click()
        await expect(page.getByText(linkedNote)).toHaveCount(0)

        // the preference survives a reload
        await page.reload()
        await expect(page.getByText(linkedNote)).toBeVisible()
    })
})
