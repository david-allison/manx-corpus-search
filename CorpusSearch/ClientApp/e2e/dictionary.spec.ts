import { test, expect } from "@playwright/test"

// The dictionary pages: entries, the letter browse, and the look-up box's
// completions. The dictionaries load from Resources/ regardless of the
// fixture corpus, so the words are real; corpus-dependent counts are not
// asserted here.
test("a word page shows its entries, names the tab, and asks not to be indexed", async ({
    page,
}) => {
    await page.goto("/dictionary/dooinney")
    await expect(
        page.getByRole("heading", { name: /dooinney/ }).first(),
    ).toBeVisible()
    // every book is a scope tab, present even when it has nothing to say
    await expect(page.getByText("All dictionaries")).toBeVisible()
    await expect(page).toHaveTitle("dooinney | Manx Corpus Search")
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute(
        "content",
        "noindex",
    )
})

test("the browse opens a letter in chapters, and can hide the never-said", async ({
    page,
}) => {
    await page.goto("/dictionary/browse/cregeen/f")
    await expect(page.locator(".dict-browse-chapter").first()).toBeVisible()
    await expect(page).toHaveTitle("Cregeen: F | Manx Corpus Search")

    const greyed = page.locator(".dict-browse-words a.dict-unattested")
    const before = await greyed.count()
    expect(before).toBeGreaterThan(0)
    await page.locator(".dict-browse-filter input").check()
    await expect(greyed).toHaveCount(0)
})

test("typing in the look-up box offers completions", async ({ page }) => {
    await page.goto("/dictionary")
    const input = page.getByLabel("Look up a Manx word")
    await input.pressSequentially("dooin", { delay: 30 })
    await expect(page.locator("[role=option]").first()).toBeVisible()
    await page.locator("[role=option]", { hasText: "dooinney" }).first().click()
    await expect(page).toHaveURL(/\/dictionary\/dooinney$/)
})
