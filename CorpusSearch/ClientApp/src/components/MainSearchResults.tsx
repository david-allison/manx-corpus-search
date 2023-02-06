import React, {useState} from "react"
import { Link } from "react-router-dom"
import "./MainSearchResults.css"
import {SearchResultEntry} from "../api/SearchApi"
import {GetMatch} from "../api/Matches"
import {floatingPromiseReturn} from "../utils/Promise"
import {useLazyLoader} from "../utils/LazyLoader"

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
    if (!edate || edate === date) {
        return new Date(date).getFullYear()
    }
    
    const first = new Date(date)
    const second = new Date(edate)
    
    if (first.getFullYear() == second.getFullYear()) {
        return "c. " + first.getFullYear().toString()
    }

    return `${new Date(date).getFullYear()}-${new Date(edate).getFullYear()}`
}

const nthIndexOf = (inputString: string, searchString: string, index: number) => {
    let i = -1

    while (index-- && i++ < inputString.length) {
        i = inputString.indexOf(searchString, i)
        if (i < 0) break
    }

    return i
}
function findNth(string: string, query: string, fromIndex: number) {
    // TODO: make this work
    const searchable = " " + string.toLowerCase()
        .replace(" ", " ")
        .replace(/[^\w\s]/gi, " ")
        .replace("\r", " ")
        .replace("\n", " ") + " "

    // assume per-word
    const stringStartIndex = nthIndexOf(searchable, " " + query.toLowerCase() + " ", fromIndex + 1)
    const stringEndIndex = stringStartIndex + query.length
    
    if (stringStartIndex === -1) {
        return null
    }

    let startIndex = stringStartIndex
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

    let endIndex = stringEndIndex
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

    return {
        pre: string.substring(startIndex, stringStartIndex),
        match: string.substring(stringStartIndex, stringEndIndex),
        post: string.substring(stringEndIndex, endIndex),
    }
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
                    {/*We want the date - if we sort on another column we want to be able to go back to the default sort*/}
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
                </tr>
            </thead>
            <tbody>
                {items.map(result => (
                    <ResultView key={result.ident + result.count.toString()} result={result} query={query} manx={props.manx}/>
                ))}
            </tbody>
        </table>
    )
}

const ResultView = (props: { result: SearchResultEntry, query: string, manx: boolean }) => {
    const {result, query, manx} = props
    
    const [matchNumber, setMatchNumber] = useState(1) // 1-based
    const [sample, setSample] = useState(result.sample)
    const [indexInLine, setIndexInLine] = useState(0) // 0-based
    
    const changeLine = async (line: number) => {
        const lineResult = await GetMatch({query: query, match: line, docIdent: result.ident})
        setSample(lineResult.manx)
        setMatchNumber(lineResult.matchNumber)
        setIndexInLine(lineResult.matchIndexInLine)
    } 
    
    const canNext = matchNumber < result.count
    const canPrev = matchNumber > 1
    
    const formattedLineNumber = String(matchNumber).padStart(4, "0")  
    const next = async (e: React.MouseEvent) => {
        e.preventDefault()
        if (!canNext) {
           return   
        }
        await changeLine(matchNumber + 1)
    }
    const prev =  async (e: React.MouseEvent) => {
        e.preventDefault()
        if (!canPrev) {
            return
        }
        await changeLine(matchNumber - 1)
    }
    
    const kwicSample = useLazyLoader(() => findNth(sample, query, indexInLine)
        ,[sample, query, indexInLine])
    
    return  <><tr>
        <td>{getFullYear(result.startDate, result.endDate) }</td>
        <td><strong>{result.documentName}</strong></td>
        <td>
            <Link to={{
                pathname: `/docs/${result.ident}`,
                search: `?q=${query}`
            }} state={{ searchLanguage: manx ? "Manx" : "English", previousPage: "/" }}>Browse&nbsp;({result.count})</Link>
        </td>
    </tr>
    <tr>
        <td colSpan={3}>
            <small style={{fontFamily: "monospace"}}>{formattedLineNumber}</small>&nbsp;
            {canPrev ? <Link to={""} style={{textDecoration: "none"}} onClick={floatingPromiseReturn(prev)}>&uarr;</Link> : <>&uarr;</>}
            &nbsp;
            {canNext ? <Link to={""} style={{textDecoration: "none"}} onClick={floatingPromiseReturn(next)}>&darr;</Link> : <>&darr;</>}
            <small style={{marginLeft: 4}}>
                {!kwicSample && kwicSample == null && sample } {/*Loading - no data to stop layout shift*/}
                {!kwicSample && kwicSample != null && "" } {/*Failed*/}
                {kwicSample && 
                    <>
                        {kwicSample.pre}
                        <strong>{kwicSample.match}</strong>
                        {kwicSample.post}
                    </>
                }
            </small>
        </td>
    </tr></>
}