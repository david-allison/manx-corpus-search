import {Link, To} from "react-router-dom"
import React from "react"
import chevronLeft from "../assets/chevron-left.png"

export const BackChevron = (props: {to: To | "historyBack"}) => {
    const maybeSkipAndGoBack = (e: React.MouseEvent) => {
        if (props.to == "historyBack") {
            history.back()
            e.preventDefault()
        }
    }
    
    return <Link to={props.to != "historyBack" ? props.to : ""} style={{ textDecoration: "none", display: "flex", alignItems: "center"}} onClick={maybeSkipAndGoBack}>
        <img style={{height: "1em", display: "flex", paddingRight: "1ch"}} src={chevronLeft} alt="Back" />
    </Link>
}