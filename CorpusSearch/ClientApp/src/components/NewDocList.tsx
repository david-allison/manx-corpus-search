/** NewDocList
 * Provides a list of links to the latest and greatest files in the Corpus
 * Which means those document.csv files that git detects as having recent changes
 */
import { use } from "react"

type DocType = { name: string; ident: string; uploaded: string }

// called once per page load - could be changed to occur on mount
const fetchNewDocs = async (): Promise<DocType[]> => {
    const res = await fetch("api/metadata/latest")
    if (!res.ok)
        console.warn(`fetch api/metadata/latest returned: ${res.status}`)
    return (await res.json()) as DocType[]
}

const newDocsPromise = fetchNewDocs().catch((e: Error) => {
    console.warn(`fetchNewDocs returned:" ${e.message}`)
    return [] as DocType[]
})

const formatUploaded = (uploaded: string): string => {
    const date = new Date(uploaded)
    if (isNaN(date.getTime())) return ""
    return date.toLocaleDateString("en-GB", { month: "long", year: "numeric" })
}

/**
 * A row in the list: a link to the document + a muted right-aligned upload date
 * A leading 🎥 emoji in the name denotes video content; keep it, unstyled by the link
 */
const RecentDocRow = ({ doc }: { doc: DocType }) => {
    let name = doc.name
    let emojiPrefix = ""
    if (name.slice(0, 2) == "🎥") {
        name = name.substring(2).trim() // trim the emoji from the name (and any leading spaces)
        emojiPrefix = "🎥 "
    }
    return (
        <div className="recent-doc-row">
            {/* title: desktop clamps long names to one line */}
            <a href={"docs/" + doc.ident} title={name}>
                {emojiPrefix}
                {name}
            </a>
            <span className="recent-doc-date">
                {formatUploaded(doc.uploaded)}
            </span>
        </div>
    )
}

export const NewDocList = () => {
    const newDocs = use(newDocsPromise)

    if (!newDocs.length) return <></>
    return (
        <div className="recent-docs">
            <div className="section-label">Recently added</div>
            <div className="recent-docs-list">
                {newDocs.map((doc) => (
                    <RecentDocRow key={doc.ident} doc={doc} />
                ))}
            </div>
        </div>
    )
}
export default NewDocList
