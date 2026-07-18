import { useEffect } from "react"

/**
 * Sync the index.html tags (title, description, canonical) imperatively, as
 * React 19 doesn't handle non-static strings in <title> — see
 * useDocumentHeadElements.test.tsx, which characterises the React behaviour.
 */
export const useDocumentHeadElements = (
    docIdent: string | undefined,
    title: string,
    yearLabel: string,
) => {
    useEffect(() => {
        if (docIdent == null || title.trim() == "") {
            return
        }
        document.title = `${title} | Manx Corpus Search`

        const description = document.querySelector('meta[name="description"]')
        const defaultDescription = description?.getAttribute("content") ?? null
        const year = yearLabel ? ` (${yearLabel})` : ""
        description?.setAttribute(
            "content",
            `“${title}”${year}: Manx and English parallel text from the Manx corpus.`,
        )

        const canonical = document.createElement("link")
        canonical.rel = "canonical"
        canonical.href = `${window.location.origin}/docs/${encodeURIComponent(docIdent)}`
        document.head.appendChild(canonical)

        return () => {
            document.title = "Manx Corpus Search"
            if (defaultDescription != null) {
                description?.setAttribute("content", defaultDescription)
            }
            canonical.remove()
        }
    }, [docIdent, title, yearLabel])
}
