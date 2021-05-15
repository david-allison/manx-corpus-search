import React, { Component } from 'react';

export class DictionaryLink extends Component {

    constructor(props) {
        super(props);
    }


    render() {
        return <>
            <strong>Defined in</strong>: {
            this.props.dictionaries.map(dictionaryName =>
                <><a href={`/Dictionary/${dictionaryName}/${this.props.query}`} target="_blank" rel="noreferrer">{ dictionaryName }</a>, </>
            )
        }
        </>
    }
}
