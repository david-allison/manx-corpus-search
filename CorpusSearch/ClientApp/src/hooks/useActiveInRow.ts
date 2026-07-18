import { RefObject, useEffect } from "react"

/** Scrolls a row's `.active` item into view — the row's own sideways scroll
 * only, never the page's. scrollIntoView drags every ancestor, and a bar
 * repeated at the page's foot would win that fight on each click, yanking
 * the reader down the page they just asked to open at its top.
 *
 * `active` is only a dependency: pass whatever names the current item, and
 * make it change when the row first has one to centre. */
export const useActiveInRow = (
    row: RefObject<HTMLElement | null>,
    active: unknown,
) => {
    useEffect(() => {
        const nav = row.current
        const item = nav?.querySelector(".active")
        if (nav == null || !(item instanceof HTMLElement)) {
            return
        }
        const delta =
            item.getBoundingClientRect().left - nav.getBoundingClientRect().left
        nav.scrollLeft += delta - (nav.clientWidth - item.clientWidth) / 2
    }, [row, active])
}
