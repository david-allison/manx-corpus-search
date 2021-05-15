import React from 'react';
import { Link } from 'react-router-dom';
import './MainSearchResults.css'

const useSortableData = (items, config = null) => {
    const [sortConfig, setSortConfig] = React.useState(config);

    const sortedItems = React.useMemo(() => {
        let sortableItems = [...items];
        if (sortConfig !== null) {
            sortableItems.sort((a, b) => {
                if (a[sortConfig.key] < b[sortConfig.key]) {
                    return sortConfig.direction === 'ascending' ? -1 : 1;
                }
                if (a[sortConfig.key] > b[sortConfig.key]) {
                    return sortConfig.direction === 'ascending' ? 1 : -1;
                }
                return 0;
            });
        }
        return sortableItems;
    }, [items, sortConfig]);

    const requestSort = (key) => {
        let direction = 'ascending';
        if (
            sortConfig &&
            sortConfig.key === key &&
            sortConfig.direction === 'ascending'
        ) {
            direction = 'descending';
        }
        setSortConfig({ key, direction });
    };

    return { items: sortedItems, requestSort, sortConfig };
};

function getFullYear(date, edate) {
    if (!date) {
        return "???";
    }

    if (!edate || edate === date) {
        return new Date(date).getFullYear();
    }

    return new Date(date).getFullYear() + "–" + new Date(edate).getFullYear();
}

function findFirst(string, query) {

    if (!string) {
        return null;
    }

    // TODO: make this work
    let searchable = " " + string.toLowerCase().replace(/[^\w\s]/gi, " ").replace("\r", " ").replace("\n", " ") + " ";

    // assume per-word
    let index = searchable.indexOf(" " + query + " ");

    if (index === -1) {
        return string;
    }

    var startIndex = index;
    var count = 0;
    var lastSpace = false;
    while (startIndex > 0 && count < 5) {
        startIndex--;
        if (string[startIndex] === ' ') {
            if (!lastSpace) {
                count++;
            }
            lastSpace = true;
        } else {
            lastSpace = false;
        }
    }

    var endIndex = index;
    count = 0;
    lastSpace = false;
    while (endIndex < string.length && count < 5) {
        endIndex++;
        if (string[endIndex] === ' ') {
            if (!lastSpace) {
                count++;
            }
            lastSpace = true;
        } else {
            lastSpace = false;
        }
    }

    return string.substring(startIndex, endIndex);


}

export default function MainSearchResults(props) {
    const { results, query } = props;
    const { items, requestSort, sortConfig } = useSortableData(results);
    const getClassNamesFor = (name) => {
        if (!sortConfig) {
            return;
        }
        return sortConfig.key === name ? sortConfig.direction : undefined;
    };
    return (
        <table className="full-search-results">
            <thead>
                <tr>
                    <th>
                        <div
                            type="button"
                            onClick={() => requestSort('startDate')}
                            className={getClassNamesFor('startDate')}
                        >
                            Date
                        </div>
                    </th>
                    <th>
                        <div
                            type="button"
                            onClick={() => requestSort('documentName')}
                            className={getClassNamesFor('documentName')}
                        >
                            Title
                        </div>
                    </th>
                    <th>
                        <div
                            type="button"
                            onClick={() => requestSort('count')}
                            className={getClassNamesFor('count')}
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
                                    search: `?q=${query}`,
                                    state: { searchManx: props.manx, searchEnglish: props.english },
                                }}>Browse</Link>
                            </td>
                    </tr>
                    <tr>
                        <td></td>
                        <td colSpan="2">
                            <small>{  findFirst(result.sample, query) }</small>
                        </td>
                        <td></td>
                    </tr>
                    </>
                ))}
            </tbody>
        </table>
    );
}