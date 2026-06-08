import {Link, To} from "react-router-dom"
import { MouseEvent } from "react"
import chevronLeft from "../assets/chevron-left.png"

export const BackChevron = (props: {to: To | "historyBack"}) => {
    const maybeSkipAndGoBack = (e: MouseEvent) => {
        if (props.to == "historyBack") {
            window.history.back()
            e.preventDefault()
        }
    }
    
    return <Link to={props.to != "historyBack" ? props.to : ""} style={{ textDecoration: "none", display: "flex", alignItems: "center"}} onClick={maybeSkipAndGoBack}>
        <img style={{height: "1em", display: "flex", paddingRight: "1ch"}} src={chevronLeft} alt="Back" />
    </Link>
}