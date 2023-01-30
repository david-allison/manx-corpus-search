import React from "react"
import { Container } from "reactstrap"
import { NavMenu } from "./NavMenu"

export const Layout = (props: { onRefresh: () => void, children: React.ReactNode}) => {

    return (<div>
        <NavMenu onRefreshState={props.onRefresh} />
        <Container>
            </Container>
            <div className="new-container">
                {props.children}
                </div>
      </div>)
}
