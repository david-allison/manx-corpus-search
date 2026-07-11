import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"

const pdfPageUrl = (pdfLink: string, page: string) => `${pdfLink}#page=${page}`

const googleBooksPageUrl = (googleBooksId: string, page: string) =>
    `https://books.google.im/books?id=${googleBooksId}&pg=PA${page}`

const editUrl = (gitHubLink: string, line: SearchWorkResult) =>
    `${gitHubLink}#L${line.csvLineNumber}`

/** The Link cell of a line: page links and the GitHub edit link.
 * Desktop-only: on a phone the column is dropped (the "Edit on GitHub" link
 * above the document covers it, and the text needs the width). */
export const LineLinkCell = (props: {
    line: SearchWorkResult
    response: SearchWorkResponse
}) => {
    const { line, response } = props
    return (
        <td className="doc-link-cell">
            {line.page != null && response.pdfLink && (
                <>
                    <a
                        href={pdfPageUrl(response.pdfLink, line.page)}
                        target="_blank"
                        rel="noreferrer"
                    >
                        p.{line.page}
                    </a>{" "}
                </>
            )}
            {line.page != null && response.googleBooksId && (
                <>
                    <a
                        href={googleBooksPageUrl(
                            response.googleBooksId,
                            line.page,
                        )}
                        target="_blank"
                        rel="noreferrer"
                    >
                        p.{line.page}
                    </a>{" "}
                </>
            )}
            {response.gitHubLink && (
                <a href={editUrl(response.gitHubLink, line)}>edit</a>
            )}
        </td>
    )
}
