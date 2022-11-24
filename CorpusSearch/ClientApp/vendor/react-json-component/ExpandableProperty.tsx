import * as React from 'react';

interface Props {
    title: string;
    expanded?: boolean;
    children: React.ReactNode
}

interface State {
    isOpen: boolean;
}

export default class ExpandableProperty extends React.Component<Props, State> {
    state = {
        isOpen: !!this.props.expanded
    };

    render() {
        return (
            <React.Fragment>
                <div style={{
                    color: "#008080",
                    fontSize: 14,
                    fontWeight: "bold",
                    cursor: "pointer",
                }} onClick={() => this.setState({ isOpen: !this.state.isOpen })}>
                    {this.props.title}
                    {this.state.isOpen ? '-' : '+'}
                </div>
                {this.state.isOpen ? this.props.children : null}
                {React.Children.count(this.props.children) === 0 && this.state.isOpen ? 'The list is empty!' : null}
            </React.Fragment>
        );
    }
}