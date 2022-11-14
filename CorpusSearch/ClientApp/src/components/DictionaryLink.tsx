import React, { Component } from "react"

export class DictionaryLink extends Component {

    render() {
        return <>
            {Object.keys((this.props as any).dictionaries).map(dictionaryName => <>
                    <a href={`/Dictionary/${dictionaryName}/${(this.props as any).query}`} target="_blank" rel="noreferrer" style={{ "fontWeight": "bold" }}>{dictionaryName}</a>
                    : {(this.props as any).dictionaries[dictionaryName]}<br />
                </>)}
        </>
    }
}
