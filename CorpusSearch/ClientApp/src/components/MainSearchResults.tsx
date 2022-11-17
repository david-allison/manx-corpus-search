import React from "react"
import { Link } from "react-router-dom"
import "./MainSearchResults.css"
import {SearchResultEntry} from "../api/SearchApi"

type SortConfig = {
    key: keyof SearchResultEntry
    direction: "ascending" | "descending"
}
const useSortableData = (items: SearchResultEntry[], config: SortConfig | null = null) => {
    const [sortConfig, setSortConfig] = React.useState(config)

    const sortedItems = React.useMemo(() => {
        const sortableItems = [...items]
        if (sortConfig === null) {
            return sortableItems
        } 
        
        sortableItems.sort((a, b) => {
            if (a[sortConfig.key] < b[sortConfig.key]) {
                return sortConfig.direction === "ascending" ? -1 : 1
            }
            if (a[sortConfig.key] > b[sortConfig.key]) {
                return sortConfig.direction === "ascending" ? 1 : -1
            }
            return 0
        })
        return sortableItems
    }, [items, sortConfig])

    const requestSort = (key: keyof SearchResultEntry) => {
        let direction: "ascending" | "descending" = "ascending"
        if (sortConfig && sortConfig.key === key && sortConfig.direction === "ascending") {
            direction = "descending"
        }
        setSortConfig({ key, direction })
    }

    return { items: sortedItems, requestSort, sortConfig }
}

function getFullYear(date: string, edate: string) {
    if (!date) {
        return "???"
    }

    if (!edate || edate === date) {
        return new Date(date).getFullYear()
    }

    return `${new Date(date).getFullYear()}-${new Date(edate).getFullYear()}`
}

function findFirst(string: string, query: string) {

    if (!string) {
        return null
    }

    // TODO: make this work
    const searchable = " " + string.toLowerCase().replace(/[^\w\s]/gi, " ").replace("\r", " ").replace("\n", " ") + " "

    // assume per-word
    const index = searchable.indexOf(" " + query + " ")

    if (index === -1) {
        return string
    }

    let startIndex = index
    let count = 0
    let lastSpace = false
    while (startIndex > 0 && count < 5) {
        startIndex--
        if (string[startIndex] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }

    let endIndex = index
    count = 0
    lastSpace = false
    while (endIndex < string.length && count < 5) {
        endIndex++
        if (string[endIndex] === " ") {
            if (!lastSpace) {
                count++
            }
            lastSpace = true
        } else {
            lastSpace = false
        }
    }

    return string.substring(startIndex, endIndex)


}

export default function MainSearchResults(props: { query:string, results: SearchResultEntry[], english: boolean, manx : boolean}) {
    const { results, query } = props
    const { items, requestSort, sortConfig } = useSortableData(results)
    const getClassNamesFor = (name: keyof SearchResultEntry) => {
        if (!sortConfig) {
            return
        }
        return sortConfig.key === name ? sortConfig.direction : undefined
    }
    return (
        <table className="full-search-results">
            <thead>
                <tr>
                    <th>
                        <div
                            onClick={() => requestSort("startDate")}
                            className={getClassNamesFor("startDate")}
                        >
                            Date
                        </div>
                    </th>
                    <th>
                        <div
                            onClick={() => requestSort("documentName")}
                            className={getClassNamesFor("documentName")}
                        >
                            Title
                        </div>
                    </th>
                    <th>
                        <div
                            onClick={() => requestSort("count")}
                            className={getClassNamesFor("count")}
                        >
                            Matches
                        </div>
                    </th>
                    <th>
                        Details
                    </th>
                </tr>
            </thead>
            <tbody>
                {items.map(result => (
                    <>
                    <tr>
                        <td>{getFullYear(result.startDate, result.endDate) }</td>
                        <td>{result.documentName}</td>
                        <td>{result.count}</td>
                            <td>
                                <Link to={{
                                    pathname: `/docs/${result.ident}`,
                                    search: `?q=${query}`
                                }} state={{ searchLanguage: props.manx ? "Manx" : "English" }}>Browse</Link>
                            </td>
                    </tr>
                    <tr>
                        <td></td>
                        <td colSpan={2}>
                            <small>{  findFirst(result.sample, query) }</small>
                        </td>
                        <td></td>
                    </tr>
                    </>
                ))}
            </tbody>
        </table>
    )
}