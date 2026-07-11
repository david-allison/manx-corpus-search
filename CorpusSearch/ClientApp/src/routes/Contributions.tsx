import "./Contributions.css"

import { Suspense, use, useEffect, useMemo } from "react"
import { NewDocList } from "../components/NewDocList"

type Contributor = { name: string; documentCount: number }

const fetchContributors = async (): Promise<Contributor[]> => {
    const res = await fetch("/api/contributions")
    if (!res.ok) {
        throw new Error(`fetch /api/contributions returned: ${res.status}`)
    }
    return (await res.json()) as Contributor[]
}

const ContributorList = (props: {
    promise: Promise<Contributor[] | "error">
}) => {
    const contributors = use(props.promise)
    if (contributors == "error") {
        return <div>Something went wrong, please try again.</div>
    }
    if (!contributors.length) {
        return <div>No contributors are credited in the metadata yet.</div>
    }
    return (
        <ol className="contributor-list">
            {contributors.map((contributor) => (
                <li key={contributor.name} className="contributor-row">
                    <div className="contributor-head">
                        <span className="contributor-name">
                            {contributor.name}
                        </span>
                        <span className="contributor-count">
                            {contributor.documentCount}{" "}
                            {contributor.documentCount == 1 ? "text" : "texts"}
                        </span>
                    </div>
                </li>
            ))}
        </ol>
    )
}

export const Contributions = () => {
    useEffect(() => {
        document.title = "Contributions | Manx Corpus Search"
        return () => {
            document.title = "Manx Corpus Search"
        }
    }, [])

    const contributorsPromise = useMemo(
        () => fetchContributors().catch(() => "error" as const),
        [],
    )

    return (
        <div className="contributions-page">
            <h1 className="page-title">Contributions</h1>
            <p className="contributions-intro">
                The corpus grows through volunteers translating, transcribing
                and proofreading texts.{" "}
                <a
                    href="https://github.com/david-allison/manx-search-data/blob/master/CONTRIBUTING.md"
                    target="_blank"
                    rel="noreferrer"
                >
                    Learn how to get involved.
                </a>
            </p>
            <div className="contributions-board">
                <div className="section-label">All-time contributors</div>
                <Suspense fallback={null}>
                    <ContributorList promise={contributorsPromise} />
                </Suspense>
            </div>
            <Suspense fallback={null}>
                <NewDocList />
            </Suspense>
        </div>
    )
}

export default Contributions
