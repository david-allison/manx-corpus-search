import React, {useState} from "react"
import { Container, Navbar, NavbarToggler, NavItem, NavLink, Collapse } from "reactstrap"
import {Link, useLocation, useNavigate} from "react-router-dom"
import "./NavMenu.css"

export const NavMenu = (props: { onRefreshState: () => void}) => {

    const [collapsed, setCollapsed] = useState(true)
    const toggleNavbar = () => setCollapsed(x => !x)

    const navigation = useNavigate()
    const location = useLocation()
    
    const onGoHome = (e: React.MouseEvent) => {
        if (location.pathname == "/") {
            // reset the state
            navigation("/", { replace: true})
            props.onRefreshState()
            e.preventDefault()
        } else if (location?.state?.previousPage == "/") {
            // If we came from "/" we want to pop the history stack,
            history.back()
        }
        // otherwise, we want to replace in the history.
    }
    return (
      <header>

        <Navbar className="navbar-expand-sm navbar-toggleable-sm ng-white border-bottom box-shadow mb-3" light>

            <Container fluid={true} className={"container"}>
                <Link replace to={"/"} className={"noLinkColor"} onClick={onGoHome}>
                    <div>
                        <img src={require("../corpus.png")} alt="Manx Corpus Search" className={"corpusImageLarge"} height="100px" />
                        <img src={require("../corpus-search-icon.png")} alt="Manx Corpus Search" className={"corpusImageSmall"} height="60px" />
                        <span className={"corpusImageSmall titleText"}>Gaelg Corpus Search</span>
                    </div>
                </Link>
                <NavbarToggler onClick={toggleNavbar} className="mr-2" />
                <Collapse className="d-sm-inline-flex flex-sm-row-reverse" isOpen={!collapsed} navbar>
                    <ul className="navbar-nav flex-grow">
                        <NavItem>
                            {/*Not a NavLink as we want to replace*/}
                            <Link replace className="text-dark nav-link" onClick={onGoHome} to="/">Home</Link>
                        </NavItem>
                        <NavItem>
                            <a className="text-dark nav-link" href="/Dictionary/Cregeen" target="_blank">Dictionary</a>
                        </NavItem>
                        <NavItem>
                            <a className="text-dark nav-link" href="/Browse" target="_blank">Browse All</a>
                        </NavItem>
                    </ul>
                </Collapse>
            </Container>
            </Navbar>
        </header>
    )
}
