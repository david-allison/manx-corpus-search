import { expect, Page, test } from "@playwright/test"

// The React app and the server-rendered Razor pages share their chrome via
// public/site-chrome.css (#313 was the two drifting apart on mobile). Assert
// the rendered header really is identical on both, at phone and desktop widths.

const RAZOR_PAGES = ["/Browse", "/Dictionary/Cregeen"]

const headerStyles = (page: Page) =>
    page.evaluate(() => {
        const style = (selector: string, props: string[]) => {
            const el = document.querySelector(selector)
            if (el == null) throw new Error(`missing element: ${selector}`)
            const computed = getComputedStyle(el)
            return Object.fromEntries(
                props.map((p) => [p, computed.getPropertyValue(p)]),
            )
        }
        return {
            header: style("header.site-header", [
                "background-color",
                "border-bottom-color",
                "box-shadow",
            ]),
            inner: style(".site-header-inner", ["max-width", "padding", "gap"]),
            logo: style(".brand img", ["height"]),
            brandName: style(".brand-name", [
                "font-family",
                "font-size",
                "color",
            ]),
            brandSub: style(".brand-sub", [
                "font-size",
                "letter-spacing",
                "color",
            ]),
            nav: style(".site-nav", ["gap", "font-size"]),
        }
    })

for (const viewport of [
    { name: "phone", width: 375, height: 667 },
    { name: "desktop", width: 1280, height: 800 },
]) {
    test(`Razor page headers match the React app's on ${viewport.name}`, async ({
        page,
    }) => {
        await page.setViewportSize(viewport)
        await page.goto("/")
        await expect(page.locator("header.site-header")).toBeVisible()
        const react = await headerStyles(page)

        for (const path of RAZOR_PAGES) {
            await page.goto(path)
            expect(await headerStyles(page), path).toEqual(react)
        }
    })
}
