import { IconButton, Menu, MenuItem } from "@mui/material"
import { SearchWorkResponse, SearchWorkResult } from "../api/SearchWorkApi"

const pdfPageUrl = (pdfLink: string, page: string) => `${pdfLink}#page=${page}`

const googleBooksPageUrl = (googleBooksId: string, page: string) =>
    `https://books.google.im/books?id=${googleBooksId}&pg=PA${page}`

const editUrl = (gitHubLink: string, line: SearchWorkResult) =>
    `${gitHubLink}#L${line.csvLineNumber}`

/** The line has at least one source link to show (page or GitHub edit) */
export const hasLineLinks = (
    line: SearchWorkResult,
    response: SearchWorkResponse,
) =>
    Boolean(
        response.gitHubLink ||
        (line.page != null && (response.pdfLink || response.googleBooksId)),
    )

/** the one menu is shared by every row's hamburger: anchored to whichever
 * button opened it */
export type LineMenuState = {
    anchor: HTMLElement
    line: SearchWorkResult
} | null

/** The Link cell of a line: page/edit links, collapsed on a phone into a
 * hamburger opening [LineLinksMenu] */
export const LineLinkCell = (props: {
    line: SearchWorkResult
    response: SearchWorkResponse
    isMobile: boolean
    onOpenMenu: (menu: LineMenuState) => void
}) => {
    const { line, response, isMobile, onOpenMenu } = props
    return (
        <td className="doc-link-cell">
            {isMobile ? (
                hasLineLinks(line, response) && (
                    <IconButton
                        className="doc-link-menu-btn"
                        size="small"
                        aria-label="Links for this line"
                        aria-haspopup="true"
                        onClick={(e) =>
                            onOpenMenu({ anchor: e.currentTarget, line })
                        }
                    >
                        {/* Material "menu" glyph, inlined rather than pulling in
                            the whole @mui/icons-material package */}
                        <svg
                            viewBox="0 0 24 24"
                            width="18"
                            height="18"
                            fill="currentColor"
                            aria-hidden="true"
                        >
                            <path d="M3 18h18v-2H3v2Zm0-5h18v-2H3v2Zm0-7v2h18V6H3Z" />
                        </svg>
                    </IconButton>
                )
            ) : (
                <>
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
                </>
            )}
        </td>
    )
}

/** The phone links menu for the line whose hamburger opened it */
export const LineLinksMenu = (props: {
    menu: LineMenuState
    response: SearchWorkResponse
    onClose: () => void
}) => {
    const { menu, response, onClose } = props
    return (
        <Menu anchorEl={menu?.anchor} open={menu != null} onClose={onClose}>
            {menu != null && [
                menu.line.page != null && response.pdfLink && (
                    <MenuItem
                        key="pdf"
                        dense
                        component="a"
                        href={pdfPageUrl(response.pdfLink, menu.line.page)}
                        target="_blank"
                        rel="noreferrer"
                        onClick={onClose}
                    >
                        Page {menu.line.page} (PDF)
                    </MenuItem>
                ),
                menu.line.page != null && response.googleBooksId && (
                    <MenuItem
                        key="books"
                        dense
                        component="a"
                        href={googleBooksPageUrl(
                            response.googleBooksId,
                            menu.line.page,
                        )}
                        target="_blank"
                        rel="noreferrer"
                        onClick={onClose}
                    >
                        Page {menu.line.page} (Google Books)
                    </MenuItem>
                ),
                response.gitHubLink && (
                    <MenuItem
                        key="edit"
                        dense
                        component="a"
                        href={editUrl(response.gitHubLink, menu.line)}
                        onClick={onClose}
                    >
                        Edit on GitHub
                    </MenuItem>
                ),
            ]}
        </Menu>
    )
}
