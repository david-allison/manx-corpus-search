import { useEffect } from "react"

/** `<abbr title>` speaks only to a mouse: hover has no finger. One document
 * listener makes every abbreviation tappable instead — a tap opens its title
 * as a bubble (`abbr-open`, drawn in custom.css), a second tap or one
 * anywhere else closes it. Delegated, so the dictionary's many abbr renderers
 * need know nothing about it; desktop keeps the native hover tooltip too. */
export const useTappableAbbrs = () => {
    useEffect(() => {
        const onClick = (event: MouseEvent) => {
            const target = event.target as Element | null
            const abbr = target?.closest("abbr[title]") ?? null
            for (const open of document.querySelectorAll("abbr.abbr-open")) {
                if (open !== abbr) {
                    open.classList.remove("abbr-open")
                }
            }
            abbr?.classList.toggle("abbr-open")
        }
        document.addEventListener("click", onClick)
        return () => document.removeEventListener("click", onClick)
    }, [])
}
