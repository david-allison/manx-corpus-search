import { ReactNode } from "react"
import { Link } from "react-router-dom"
import { NavMenu } from "./NavMenu"
import { isDictionaryHost } from "../utils/Host"

export const Layout = (props: {
    onRefresh: () => void
    children: ReactNode
}) => {
    return (
        <div>
            <NavMenu onRefreshState={props.onRefresh} />
            <div className="page-main">
                {props.children}
                <SiteFooter />
            </div>
        </div>
    )
}

const SiteFooter = () => (
    <footer className="site-footer">
        <span>
            Maintained by{" "}
            <a
                href="https://github.com/david-allison"
                target="_blank"
                rel="noreferrer"
            >
                David Allison
            </a>
            {/* the project credit belongs to the corpus site's front door */}
            {!isDictionaryHost() && (
                <> · A Manx Language Research Group project</>
            )}
            <br />
            With thanks to <Link to="/contributions">
                all the volunteers
            </Link>{" "}
            who transcribe, translate &amp; contribute texts.
        </span>
        <span>
            <a
                href="https://github.com/david-allison/manx-corpus-search"
                target="_blank"
                rel="noreferrer"
            >
                Free &amp; open source on GitHub
            </a>
        </span>
    </footer>
)
