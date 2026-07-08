import {Link, To} from "react-router-dom"
import { MouseEvent } from "react"

export const BackChevron = (props: {to: To | "historyBack"}) => {
    const maybeSkipAndGoBack = (e: MouseEvent) => {
        if (props.to == "historyBack") {
            window.history.back()
            e.preventDefault()
        }
    }

    return <Link className="doc-back" to={props.to != "historyBack" ? props.to : ""} onClick={maybeSkipAndGoBack}>
        <span aria-hidden="true" className="doc-back-chevron">‹</span>
        Back
    </Link>
}
