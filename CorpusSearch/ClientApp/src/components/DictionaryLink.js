import React, { Component } from 'react';

export class DictionaryLink extends Component {

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
