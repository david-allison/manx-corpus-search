import React, { Component } from 'react';

export class DictionaryLink extends Component {

    render() {
        return <>
            {
            Object.keys(this.props.dictionaries).map(dictionaryName =>
                <>
                    <a href={`/Dictionary/${dictionaryName}/${this.props.query}`} target="_blank" rel="noreferrer" style={{"font-weight":"bold"}}>{dictionaryName}</a>
                    : {this.props.dictionaries[dictionaryName]}<br/>
                </>
            )
        }
        </>
    }
}
