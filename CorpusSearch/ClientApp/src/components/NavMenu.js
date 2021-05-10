import React, { Component } from 'react';
import { Container, Navbar } from 'reactstrap'
import './NavMenu.css';
import logo from '../corpus.png'

export class NavMenu extends Component {
  static displayName = NavMenu.name;

  constructor (props) {
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
    
/*            <NavbarBrand tag={Link} to="/">Corpus Search</NavbarBrand>
                <NavbarToggler onClick={this.toggleNavbar} className="mr-2" />
                <Collapse className="d-sm-inline-flex flex-sm-row-reverse" isOpen={!this.state.collapsed} navbar>
                    <ul className="navbar-nav flex-grow">
                        <NavItem>
                            <NavLink tag={Link} className="text-dark" to="/">Home</NavLink>
                        </NavItem>
                    </ul>
                </Collapse>*/
  render () {
    return (
      <header>
        <Navbar className="navbar-expand-sm navbar-toggleable-sm ng-white border-bottom box-shadow mb-3" light>

          <Container>
            <img src={logo} alt="Manx Corpus Search Logo" height="100px" />

          </Container>
        </Navbar>
      </header>
    );
  }
}
