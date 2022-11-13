import React, { Component } from 'react';
import { Container } from 'reactstrap';
import { NavMenu } from './NavMenu';

export class Layout extends Component {
  static displayName = Layout.name;

    // avoid container to allow for full width
  render () {
    return (
      <div>
        <NavMenu />
        <Container>
            </Container>
            <div className="new-container">
                {this.props.children}
                </div>
      </div>
    );
  }
}
