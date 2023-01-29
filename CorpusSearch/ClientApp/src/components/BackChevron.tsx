import {Link, To} from "react-router-dom"
import React from "react"
import chevronLeft from "../assets/chevron-left.png"

export const BackChevron = (props: {to: To}) => {
    return <Link to={props.to} style={{ textDecoration: "none", display: "flex", alignItems: "center"}}>
        <img style={{height: "1em", display: "flex", paddingRight: "1ch"}} src={chevronLeft} alt="Back" />
    </Link>
}