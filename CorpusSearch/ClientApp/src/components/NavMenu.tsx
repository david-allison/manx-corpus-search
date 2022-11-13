import React, { Component } from 'react';
import { Container, Navbar, NavbarToggler, NavItem, NavLink, Collapse } from 'reactstrap'
import { Link } from 'react-router-dom';
import './NavMenu.css';
// @ts-expect-error TS(2307): Cannot find module '../corpus.png' or its correspo... Remove this comment to see the full error message
import logo from '../corpus.png'

type State = any;

export class NavMenu extends Component<{}, State> {
  static displayName = NavMenu.name;

  constructor (props: {}) {
    super(props);

    this.toggleNavbar = this.toggleNavbar.bind(this);
    this.state = {
      collapsed: true
    };
  }

  toggleNavbar () {
    this.setState({
      collapsed: !this.state.collapsed
    });
    }
    
/*<NavbarBrand tag={Link} to="/">Corpus Search</NavbarBrand>*/
    
  render () {
    return (
      <header>

        <Navbar className="navbar-expand-sm navbar-toggleable-sm ng-white border-bottom box-shadow mb-3" light>

          <Container>
            <img src={logo} alt="Manx Corpus Search Logo" height="100px" />

                <NavbarToggler onClick={this.toggleNavbar} className="mr-2" />
                <Collapse className="d-sm-inline-flex flex-sm-row-reverse" isOpen={!this.state.collapsed} navbar>
                    <ul className="navbar-nav flex-grow">
                        <NavItem>
                            <NavLink tag={Link} className="text-dark" to="/">Home</NavLink>
                        </NavItem>
                            <NavItem>
                                <a className="text-dark nav-link"href="/Dictionary/Cregeen">Dictionary</a>

                        </NavItem>
                    </ul>
                    </Collapse>
                </Container>
        </Navbar>
      </header>
    );
  }
}
