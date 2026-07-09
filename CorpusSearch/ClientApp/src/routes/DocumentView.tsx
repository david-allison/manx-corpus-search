/* eslint @typescript-eslint/no-misused-promises: 0 */
import "./DocumentView.css"
import "../components/AdvancedOptions.css"

import { Fragment, ReactNode, useEffect, useState, useTransition } from "react"
import { useLocation, useMatch } from "react-router-dom"
import {
    searchWork,
    SearchWorkResponse,
    SourceLink,
} from "../api/SearchWorkApi"
import { SearchLanguage } from "./Home"
import { CircularProgress } from "@mui/material"
import { ManxEnglishSelector } from "../components/ManxEnglishSelector"
import { metadataLookup } from "../api/MetadataApi"
import { ComparisonTable } from "../components/ComparisonTable"
import { SearchBar } from "../components/SearchBar"
import { BackChevron } from "../components/BackChevron"
import { AccentSensitive, CaseSensitive } from "../components/AdvancedOptions"
import { useLanguageVisibility } from "../hooks/useLanguageVisibility"
import { usePersistedState } from "../hooks/usePersistedState"
import { iMuseumUrl } from "../utils/IMuseum"
import { isUrl } from "../utils/Url"

type Metadata = Record<string, unknown>

const yearOf = (value: unknown): number | null => {
    if (typeof value !== "string") return null
    const date = new Date(value)
    return isNaN(date.getTime()) ? null : date.getFullYear()
}

/** The year-badge label, e.g. "1775" or "1976–1982" */
const getYearLabel = (metadata: Metadata | null): string => {
    if (metadata == null) {
        return ""
    }
    const created = yearOf(metadata["created"])
    if (created != null) {
        return String(created)
    }
    const start = yearOf(metadata["createdCircaStart"])
    const end = yearOf(metadata["createdCircaEnd"])
    if (start != null && end != null) {
        return start == end ? String(start) : `${start}–${end}`
    }
    if (start != null || end != null) {
        return String(start ?? end)
    }
    return ""
}

/** Metadata keys surfaced elsewhere on the page (title, year badge, note, links…) */
const handledMetadataKeys = new Set([
    "name",
    "identifier",
    "ident",
    "notes",
    "source",
    "created",
    "createdCircaStart",
    "createdCircaEnd",
    "gitHubRepo",
    "relativeCsvPath",
    "externalPdfLink",
    "googleBooksId",
    "mnhNewsComponent",
])

type MetaRow = {
    label: string
    value: ReactNode
    length: number
    preserveCase?: boolean
}

/** keys whose brand casing is shown as-is, instead of being split and uppercased */
const brandLabels: Record<string, string> = {
    iMuseum: "iMuseum",
}

const iMuseumLink = (value: string): ReactNode => (
    <a href={iMuseumUrl(value)} target="_blank" rel="noreferrer">
        {value}
    </a>
)

/** Inline label/value pairs for the metadata strip: SOURCE first, then any other scalar metadata */
const buildMetaRows = (
    metadata: Metadata | null,
    source: string | undefined,
    sourceLinks: SourceLink[] | null,
): MetaRow[] => {
    const rows: MetaRow[] = []

    const metadataSource = metadata?.["source"]
    const sourceName =
        source ??
        (typeof metadataSource === "string" ? metadataSource : undefined)
    if (sourceName || sourceLinks?.length) {
        const nameNode =
            sourceName && isUrl(sourceName) ? (
                <a href={sourceName} target="_blank" rel="noreferrer">
                    {sourceName}
                </a>
            ) : (
                sourceName
            )
        rows.push({
            label: "Source",
            value: (
                <>
                    {nameNode}
                    {(sourceLinks ?? []).map((link) => (
                        <Fragment key={link.url}>
                            {" "}
                            ·{" "}
                            <a href={link.url} target="_blank" rel="noreferrer">
                                {link.text}
                            </a>
                        </Fragment>
                    ))}
                </>
            ),
            length: sourceName?.length ?? 0,
        })
    }

    for (const [key, value] of Object.entries(metadata ?? {})) {
        if (handledMetadataKeys.has(key)) {
            continue
        }
        // only scalars fit the label/value strip; everything else is in Metadata (JSON)
        if (
            typeof value !== "string" &&
            typeof value !== "number" &&
            typeof value !== "boolean"
        ) {
            continue
        }
        const text = String(value)
        const brand = brandLabels[key]
        rows.push({
            label: brand ?? key.replace(/([a-z0-9])([A-Z])/g, "$1 $2"),
            value: key == "iMuseum" ? iMuseumLink(text) : text,
            length: text.length,
            preserveCase: brand != null,
        })
    }
    return rows
}

/* collapsed strip: only short values, and only a handful of them */
const META_SHORT_VALUE = 90
const META_COLLAPSED_ROWS = 4

const collapseMetaRows = (rows: MetaRow[]): MetaRow[] =>
    rows
        .filter((row) => row.length <= META_SHORT_VALUE)
        .slice(0, META_COLLAPSED_ROWS)

export const DocumentView = () => {
    const location = useLocation()
    const match = useMatch("/docs/:docId")

    // keep the previous results on screen during a refetch, rather than unmounting them
    const [isPending, startTransition] = useTransition()
    const [title, setTitle] = useState(" ") // use a space to avoid a layout shift

    const docIdent = match?.params.docId

    // the 'q' parameter from the querystring
    const q = new URLSearchParams(location.search).get("q")
    // set when following a result of a hyphen-insensitive corpus search, so the counts match
    const ignoreHyphens =
        new URLSearchParams(location.search).get("ignoreHyphens") === "true"

    const [value, setValue] = useState(q ?? "*")
    // initially set when following a result of a case-sensitive corpus search (#19)
    const [caseSensitive, setCaseSensitive] = useState(
        new URLSearchParams(location.search).get("caseSensitive") === "true",
    )
    // initially set when following a result of an accent-sensitive corpus search
    const [accentSensitive, setAccentSensitive] = useState(
        new URLSearchParams(location.search).get("accentSensitive") === "true",
    )

    const getInitialSearchLanguage = (): SearchLanguage => {
        // eslint-disable-next-line
        switch (location.state?.searchLanguage) {
            case "English":
                return "English"
            case "Manx":
                return "Manx"
            default:
                return "Manx"
        }
    }
    const [searchLanguage, setSearchLanguage] = useState<SearchLanguage>(
        getInitialSearchLanguage,
    )
    const searchManx = searchLanguage == "Manx"
    const searchEnglish = searchLanguage == "English"

    const [searchWorkResponse, setSearchWorkResponse] =
        useState<SearchWorkResponse | null>(null)

    // which language column is displayed (the "Show" toggle)
    const languageVisibility = useLanguageVisibility()

    // the expander rows between the matches can feel busy: let readers hide them
    const [contextEnabled, setContextEnabled] = usePersistedState(
        "showContext",
        (stored) => stored !== "false",
        String,
    )

    // notes distract while reading (#132): hidden by default, each collapsed
    // behind its "[1]" marker; this option shows them all
    const [notesEnabled, setNotesEnabled] = usePersistedState(
        "showNotes",
        (stored) => stored === "true",
        String,
    )

    // load the data
    useEffect(() => {
        if (!docIdent) {
            return
        }

        startTransition(async () => {
            try {
                const data = await searchWork({
                    docIdent,
                    value,
                    searchEnglish,
                    searchManx,
                    ignoreHyphens,
                    caseSensitive,
                    accentSensitive,
                })
                setSearchWorkResponse(data)
                setTitle(data.title)
            } catch (e) {
                console.error(e)
            }
        })
    }, [
        value,
        searchEnglish,
        searchManx,
        docIdent,
        ignoreHyphens,
        caseSensitive,
        accentSensitive,
    ])

    const [metadata, setMetadata] = useState<Metadata | null>(null)
    const [showAllMeta, setShowAllMeta] = useState(false)

    useEffect(() => {
        if (docIdent == null) {
            return
        }

        setShowAllMeta(false)
        metadataLookup(docIdent)
            .then((x) => setMetadata(x as Metadata))
            .catch((e) => console.warn(e))
    }, [docIdent])

    const yearLabel = getYearLabel(metadata)
    const metaRows = buildMetaRows(
        metadata,
        searchWorkResponse?.source,
        searchWorkResponse?.sourceLinks ?? null,
    )
    const collapsedMetaRows = collapseMetaRows(metaRows)
    const visibleMetaRows = showAllMeta ? metaRows : collapsedMetaRows
    const hiddenMetaCount = metaRows.length - collapsedMetaRows.length

    const gitHubLink = searchWorkResponse?.gitHubLink
    // mirrors IDocumentExtensions.GetDownloadTextLink/GetDownloadMetadataLink
    const csvLink = gitHubLink
        ?.replace("https://github.com", "https://raw.githubusercontent.com")
        .replace("/blob/", "/")
    const jsonLink = csvLink?.includes("document.csv")
        ? csvLink.replace("document.csv", "manifest.json.txt")
        : undefined

    const hasQuery = value.trim() != "" && value != "*"
    const matchLabel = (response: SearchWorkResponse) =>
        hasQuery
            ? `${(response.totalMatches ?? 0).toLocaleString()} matches for “${value}” · ${response.numberOfResults.toLocaleString()} lines`
            : `${response.numberOfResults.toLocaleString()} lines`

    return (
        <div>
            {searchWorkResponse == null ? (
                <title>Manx Corpus Search</title>
            ) : (
                <title>{title} | Manx Corpus Search</title>
            )}
            <meta
                name="description"
                content="Search for words &amp; phrases within over 500 translated texts, from 1610 to the present era. Free &amp; Open Source"
            />

            <div className="search-row search-row-hero">
                <SearchBar
                    query={value}
                    onChange={(x) => setValue(x.target.value)}
                    language={searchLanguage}
                />
                <ManxEnglishSelector
                    initialLanguage={searchLanguage}
                    onLanguageChange={setSearchLanguage}
                />
            </div>

            <details className="advanced-options">
                <summary>Advanced options</summary>
                <div className="advanced-options-content">
                    <span className="doc-show-label">Show</span>
                    <div className="seg-control">
                        <button
                            type="button"
                            className={
                                languageVisibility.visibleLanguage == "Manx"
                                    ? "active"
                                    : undefined
                            }
                            onClick={() =>
                                languageVisibility.setVisibleLanguage("Manx")
                            }
                        >
                            Gaelg
                        </button>
                        <button
                            type="button"
                            className={
                                languageVisibility.visibleLanguage == "English"
                                    ? "active"
                                    : undefined
                            }
                            onClick={() =>
                                languageVisibility.setVisibleLanguage("English")
                            }
                        >
                            English
                        </button>
                        <button
                            type="button"
                            className={
                                languageVisibility.visibleLanguage == "Both"
                                    ? "active"
                                    : undefined
                            }
                            onClick={() =>
                                languageVisibility.setVisibleLanguage("Both")
                            }
                        >
                            Gaelg &amp; Baarle
                        </button>
                    </div>
                    <CaseSensitive
                        caseSensitive={caseSensitive}
                        onCaseSensitiveChange={setCaseSensitive}
                    />
                    <AccentSensitive
                        accentSensitive={accentSensitive}
                        onAccentSensitiveChange={setAccentSensitive}
                    />
                    <label
                        className="advanced-options-match"
                        title="The “Show next/previous lines” rows between matches"
                    >
                        <input
                            id="showContext"
                            type="checkbox"
                            checked={contextEnabled}
                            onChange={(e) =>
                                setContextEnabled(e.target.checked)
                            }
                        />
                        Show context
                    </label>
                    <label
                        className="advanced-options-match"
                        title="Shows all annotations. If disabled, tap the [1] marker to show a note."
                    >
                        <input
                            id="showNotes"
                            type="checkbox"
                            checked={notesEnabled}
                            onChange={(e) => setNotesEnabled(e.target.checked)}
                        />
                        Show notes
                    </label>
                </div>
            </details>

            <div className="doc-header">
                <div className="doc-title-row">
                    <BackChevron to={"historyBack"} />
                    <h1 className="page-title" id="tabelLabel">
                        {searchWorkResponse == null ? (
                            "\u00A0" /* keep the line height; no title or year badge until loaded */
                        ) : (
                            <>
                                {title}
                                {yearLabel != "" && (
                                    <span className="year-badge doc-year-badge">
                                        {yearLabel}
                                    </span>
                                )}
                            </>
                        )}
                    </h1>
                </div>
                {searchWorkResponse != null && (
                    <div className="doc-match-label">
                        {matchLabel(searchWorkResponse)}
                    </div>
                )}
            </div>

            {isPending && searchWorkResponse == null && (
                <div
                    style={{
                        marginTop: 40,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                    }}
                >
                    <CircularProgress style={{ alignSelf: "center" }} />
                </div>
            )}
            {searchWorkResponse != null && (
                // During a re-fetch, dim the results
                <div
                    style={{
                        opacity: isPending ? 0.5 : 1,
                        transition: "opacity 150ms ease",
                    }}
                >
                    {(metaRows.length > 0 || gitHubLink) && (
                        <div className="doc-meta-strip">
                            {visibleMetaRows.map((row) => (
                                <span className="doc-meta-pair" key={row.label}>
                                    <span
                                        className={
                                            "doc-meta-key" +
                                            (row.preserveCase
                                                ? " doc-meta-key-brand"
                                                : "")
                                        }
                                    >
                                        {row.label}
                                    </span>
                                    <span className="doc-meta-value">
                                        {row.value}
                                    </span>
                                </span>
                            ))}
                            {hiddenMetaCount > 0 && (
                                <button
                                    type="button"
                                    className="doc-meta-toggle"
                                    onClick={() => setShowAllMeta((x) => !x)}
                                >
                                    {showAllMeta
                                        ? "Show less ▴"
                                        : `Show ${hiddenMetaCount} more ▾`}
                                </button>
                            )}
                            {gitHubLink && (
                                <span className="doc-meta-links">
                                    <a
                                        href={gitHubLink}
                                        target="_blank"
                                        rel="noreferrer"
                                    >
                                        Edit on GitHub
                                    </a>
                                    {csvLink && (
                                        <a
                                            href={csvLink}
                                            target="_blank"
                                            rel="noreferrer"
                                        >
                                            Text (CSV)
                                        </a>
                                    )}
                                    {jsonLink && (
                                        <a
                                            href={jsonLink}
                                            target="_blank"
                                            rel="noreferrer"
                                        >
                                            Metadata (JSON)
                                        </a>
                                    )}
                                </span>
                            )}
                        </div>
                    )}

                    {searchWorkResponse.notes && (
                        <div className="doc-note">
                            {searchWorkResponse.notes}
                        </div>
                    )}

                    <ComparisonTable
                        response={searchWorkResponse}
                        docIdent={docIdent}
                        expandContext={contextEnabled}
                        showNotes={notesEnabled}
                        value={value}
                        highlightManx={searchManx}
                        highlightEnglish={searchEnglish}
                        manxVisible={languageVisibility.manxVisible}
                        englishVisible={languageVisibility.englishVisible}
                        translations={searchWorkResponse.translations}
                    />
                </div>
            )}
        </div>
    )
}
