import {ChangeEventHandler} from "react"
import {SearchLanguage} from "../routes/Home"

export const SearchBar = (props: {query: string, onChange: ChangeEventHandler<HTMLInputElement>, language?: SearchLanguage}) => {
    const {query, onChange, language} = props
    const placeholder = language == "English" ? "Enter an English search term…" : "Enter a Manx search term…"
    return <input
        size={5}
        id="corpus-search-box"
        className="corpus-search-input"
        style={{flex: 1, minWidth: 100}}
        placeholder={placeholder}
        type="search"
        value={query}
        onChange={onChange} />
}
