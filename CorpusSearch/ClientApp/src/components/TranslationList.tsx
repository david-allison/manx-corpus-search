import React, { Component } from "react"

export class TranslationList extends Component {

    render() {
        return <>
            {Object.keys((this.props as any).translations).map(langCode => <><strong>{langCode}:</strong> {(this.props as any).translations[langCode].map((x: any) => <>{x}, </>)}
                    </>)}
        </>
    }
}
