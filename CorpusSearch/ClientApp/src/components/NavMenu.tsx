import {MouseEvent} from "react"
import {Link, useLocation, useNavigate} from "react-router-dom"
import corpusIcon from "../assets/corpus-search-icon.png"

export const NavMenu = (props: { onRefreshState: () => void}) => {

    const navigation = useNavigate()
    const location = useLocation()

    const onGoHome = (e: MouseEvent) => {
        if (location.pathname == "/") {
            // reset the state
            navigation("/", { replace: true})
            props.onRefreshState()
            e.preventDefault()
        } else if (location?.state?.previousPage == "/") {
            // If we came from "/" we want to pop the history stack,
            history.back()
        }
        // otherwise, we want to replace in the history.
    }

    return (
        <header className="site-header">
            <div className="site-header-inner">
                <Link replace to={"/"} className="brand" onClick={onGoHome}>
                    <img src={corpusIcon} alt="Gaelg Corpus Search" />
                    <span className="brand-lockup">
                        <span className="brand-name">Gaelg</span>
                        <span className="brand-sub">CORPUS SEARCH</span>
                    </span>
                </Link>
                <nav className="site-nav">
                    {/*Not a NavLink as we want to replace*/}
                    <Link replace className="active" onClick={onGoHome} to="/">Home</Link>
                    <a href="/Dictionary/Cregeen">Dictionary</a>
                    <a href="/Browse">Browse All</a>
                    <a href="https://www.learnmanx.com/resources/translations/" target="_blank" rel="noreferrer">Translations</a>
                </nav>
            </div>
        </header>
    )
}
