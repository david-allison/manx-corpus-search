/** NewDocList
 * Provides a list of links to the latest and greatest files in the Corpus
 * Which means those document.csv files that git detects as having recent changes
 */
import React, {useEffect, useState} from "react"

/**
 * Produces a link to a document, with an emoji denoting the type of content
 * @example "🎥 Jack as Ned" as a link; no link underline for "🎥 "
 */
function CorpusLink({ident = "", name = ""}): JSX.Element {
    let emojiPrefix = "📖"
    if (name.slice(0, 2) == "🎥") {
        name = name.substring(2).trim() // trim the emoji from the name (and any leading spaces)
        emojiPrefix = "🎥"
    }
    const href = "docs/" + ident
    return <div>
        {/* Better visuals if the prefix is not underlined; maintain the link for UX */}
        <a style={{textDecoration: "none"}} href={href}>{emojiPrefix}&nbsp;</a>
        <a href={href}>{name}</a>
    </div>
}

export const NewDocList = () => {
type DocType = { name: string, ident: string, uploaded: string }
const [newDocs, setNewDocs]
    = useState<DocType[]>([])
useEffect(() => {
        const fetchNewDocs = async () => {
            await fetch("api/metadata/latest").then((res) => {
                if (!res.ok) console.warn(`fetch api/metadata/latest returned: ${res.status}`)
                return res.json()
            }).then((json: DocType[]) => {
                setNewDocs(json)
            })
        }
        fetchNewDocs().catch((e: Error) => console.warn(`fetchNewDocs returned:" ${e.message}`))
    }, []
    )
    const len = newDocs.length
    if (!len) return <></>
    return (<div className="NewDocList">
        <header className="NewDocList-header">
        </header>
        <div>Recently Uploaded:<br/>
            <div>
                <ul>{newDocs.map(doc =>
                    <li key={doc.name}><CorpusLink name={doc.name} ident={doc.ident}/></li>)
                }</ul>
            </div>
        </div>
    </div>)
}
export default NewDocList
