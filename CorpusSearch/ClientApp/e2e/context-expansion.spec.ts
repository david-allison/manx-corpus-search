import { test, expect } from "@playwright/test"

// End-to-end coverage of #286: a search inside a document only returns the matching
// lines; expanders between them reveal the hidden neighbours (like GitHub's
// 'expand hunk' button).

const DOC = "/docs/e2e-highlight-fixture"
const expander = "tr.doc-expand-row"

test.describe("context expansion", () => {
    test("expands the hidden lines around the match", async ({ page }) => {
        // 'moghrey' matches only line 6 of 8: the rest of the document is hidden
        await page.goto(`${DOC}?q=moghrey`)
        await expect(page.locator("mark.textHighlight")).toHaveText("moghrey")
        await expect(page.locator(expander)).toHaveCount(2)
        await expect(page.getByText("Va’n dooinney")).toHaveCount(0)

        // above the match
        await page.getByRole("button", { name: /Show previous/ }).click()
        await expect(page.getByText("Va’n dooinney")).toBeVisible()

        // below the match; the whole document is then visible
        await page.getByRole("button", { name: /Show next/ }).click()
        await expect(page.getByText("jee.")).toBeVisible()
        await expect(page.locator(expander)).toHaveCount(0)
    })

    test("a '*' search shows every line with nothing to expand", async ({
        page,
    }) => {
        await page.goto(DOC)
        await expect(page.getByText("Va’n dooinney")).toBeVisible()
        await expect(page.locator(expander)).toHaveCount(0)
    })
})
