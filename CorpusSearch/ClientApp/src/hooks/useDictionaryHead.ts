import { useEffect } from "react"

/**
 * The dictionary pages' head tags: a title naming the page, and a robots
 * noindex while the dictionary is in beta — it launches for readers first,
 * and asks the indexes in only once its canonical story is settled (the
 * sitemap side lives in SeoController). Imperative, as
 * useDocumentHeadElements is and for the same React 19 reason.
 */
export const useDictionaryHead = (title: string | null | undefined) => {
    useEffect(() => {
        if (title == null || title.trim() === "") {
            return
        }
        document.title = `${title} | Manx Corpus Search`
        return () => {
            document.title = "Manx Corpus Search"
        }
    }, [title])

    useEffect(() => {
        const robots = document.createElement("meta")
        robots.name = "robots"
        robots.content = "noindex"
        document.head.appendChild(robots)
        return () => robots.remove()
    }, [])
}
