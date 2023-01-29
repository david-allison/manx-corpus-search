import React, {ChangeEventHandler} from "react"

export const SearchBar = (props: {query: string, onChange :ChangeEventHandler<HTMLInputElement>}) => {
    const {query, onChange} = props
    return <input size={5} id="corpus-search-box" style={{flexGrow: 1, marginRight: 12}} placeholder="Enter search term" type="text" value={query} onChange={onChange} />

}