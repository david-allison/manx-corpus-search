import { test, expect } from "@playwright/test"

// End-to-end coverage of #40: matching happens on the normalized Lucene index, so the
// query often does not occur verbatim in the displayed text. The server returns offsets
// into the raw text; these tests assert the visible result.

const DOC = "/docs/e2e-highlight-fixture"
const mark = "mark.textHighlight"

test.describe("document view", () => {
    test("diacritic-folded query highlights the raw text", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=chengey`)
        await expect(page.locator(mark)).toHaveText("çhengey")
    })

    test("straight-apostrophe query matches the curly apostrophe", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=va'n`)
        await expect(page.locator(mark)).toHaveText("Va’n")
    })

    test("a query matching a longer hyphenated word highlights all of it", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=cre`)
        await expect(page.locator(mark)).toHaveText(["cre-erbee", "cre"])
    })

    test("wildcards highlight each matched form", async ({ page }) => {
        await page.goto(`${DOC}?q=cab*`)
        await expect(page.locator(mark)).toHaveText(["cabbil", "cabbyl"])
    })

    test("a phrase is a single highlight", async ({ page }) => {
        await page.goto(`${DOC}?q=moghrey%20mie`)
        await expect(page.locator(mark)).toHaveText(["moghrey mie"])
    })

    test("'and' highlights the terms, not the text between them", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=hi%20and%20world`)
        await expect(page.locator(mark)).toHaveText(["hi", "world"])
    })

    test("punctuation next to a match is not highlighted", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=jee`)
        // the raw line is "jee." - the trailing full stop is outside the match
        await expect(page.locator(mark)).toHaveText("jee")
    })
})

// #19
test.describe("case-sensitive search", () => {
    test("a case-matching query still matches with 'Match case' on", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=Va'n&caseSensitive=true`)
        await expect(page.locator(mark)).toHaveText("Va’n")
    })

    test("toggling 'Match case' on the document page filters by case", async ({
        page,
    }) => {
        await page.goto(`${DOC}?q=va'n`)
        await expect(page.locator(mark)).toHaveText("Va’n")

        await page.locator("summary", { hasText: "Advanced options" }).click()
        await page.getByLabel("Match case").check()

        // the raw text is "Va’n": the lowercase query no longer matches
        await expect(page.locator(mark)).toHaveCount(0)
        await expect(page.getByText(/0 matches/)).toBeVisible()
    })

    test("toggling 'Match case' on the home page filters results", async ({
        page,
    }) => {
        await page.goto("/?q=va'n")
        await expect(page.locator("strong.kwic-match")).toHaveText("Va’n")

        await page.locator("summary", { hasText: "Advanced options" }).click()
        await page.getByLabel("Match case").check()

        await expect(page.getByText(/No matches/)).toBeVisible()
    })
})

test.describe("home page", () => {
    test("diacritic-folded query highlights the KWIC sample", async ({
        page,
    }) => {
        await page.goto("/?q=chengey")
        await expect(page.locator("strong.kwic-match")).toHaveText("çhengey")
    })

    test("typing a query highlights the KWIC sample", async ({ page }) => {
        await page.goto("/")
        await page.fill("#corpus-search-box", "chengey")
        await expect(page.locator("strong.kwic-match")).toHaveText("çhengey")
    })

    test("stepping to the next match updates the highlight", async ({
        page,
    }) => {
        // 'cre' matches 'cre-erbee' (line 3) and 'cre' (line 4) of the fixture
        await page.goto("/?q=cre")
        await expect(page.locator("strong.kwic-match")).toHaveText("cre-erbee")

        await page.getByRole("button", { name: "›" }).click()
        await expect(page.locator("strong.kwic-match")).toHaveText("cre")
    })
})
