import React, {ChangeEventHandler} from "react"

export const SearchBar = (props: {query: string, onChange :ChangeEventHandler<HTMLInputElement>}) => {
    const {query, onChange} = props
    return <input size={5} id="corpus-search-box" style={{flexGrow: 1, marginLeft: 12}} placeholder="Enter search term" type="search" value={query} onChange={onChange} />

}