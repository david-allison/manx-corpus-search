import React, { Component } from "react"
import { Container, Navbar, NavbarToggler, NavItem, NavLink, Collapse } from "reactstrap"
import { Link } from "react-router-dom"
import "./NavMenu.css"

type State = any;

export class NavMenu extends Component<unknown, State> {
  static displayName = NavMenu.name

  constructor (props: unknown) {
    super(props)

    this.toggleNavbar = this.toggleNavbar.bind(this)
    this.state = {
      collapsed: true
    }
  }

  toggleNavbar () {
    this.setState({
      collapsed: !this.state.collapsed
    })
    }
    
/*<NavbarBrand tag={Link} to="/">Corpus Search</NavbarBrand>*/
    
  render () {
    return (
      <header>

        <Navbar className="navbar-expand-sm navbar-toggleable-sm ng-white border-bottom box-shadow mb-3" light>

          <Container>
              <div>
                <img src={require("../corpus.png")} alt="Manx Corpus Search" className={"corpusImageLarge"} height="100px" />
                <img src={require("../corpus-search-icon.png")} alt="Manx Corpus Search" className={"corpusImageSmall"} height="60px" />
                <span className={"corpusImageSmall titleText"}>Gaelg Corpus Search</span>
              </div>
                <NavbarToggler onClick={this.toggleNavbar} className="mr-2" />
                <Collapse className="d-sm-inline-flex flex-sm-row-reverse" isOpen={!this.state.collapsed} navbar>
                    <ul className="navbar-nav flex-grow">
                        <NavItem>
                            <NavLink tag={Link} className="text-dark" to="/">Home</NavLink>
                        </NavItem>
                        <NavItem>
                            <a className="text-dark nav-link" href="/Dictionary/Cregeen">Dictionary</a>
                        </NavItem>
                        <NavItem>
                            <a className="text-dark nav-link" href="/Browse">Browse All</a>
                        </NavItem>
                        <NavItem>
                            <a className="text-dark nav-link" href="/MailingList">Mailing List</a>
                        </NavItem>
                    </ul>
                    </Collapse>
                </Container>
        </Navbar>
      </header>
    )
  }
}
