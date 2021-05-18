import React, { Component } from 'react';

export class TranslationList extends Component {

    render() {
        return <>
            {
                Object.keys(this.props.translations).map(
                    langCode => <><strong>{langCode}:</strong> {
                        this.props.translations[langCode].map(x => <>{x}, </>)
                    }
                    </>
                )
            }
        </>
    }
}
